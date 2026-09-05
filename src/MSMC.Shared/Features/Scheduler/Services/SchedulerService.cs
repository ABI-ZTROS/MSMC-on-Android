// -----------------------------------------------------------------------------
// 文件名: SchedulerService.cs (重构版)
// 命名空间: io.NET.ZTR_OS.Features.Scheduler.Services
// 功能描述: 计划任务调度服务 —— 增加 Start/Stop 实现，集成定时扫描
// 设计模式: 三链原则 - 因果链：定时器扫描触发任务；执行链：SemaphoreSlim防重入；返回链：全链路日志
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using Microsoft.Extensions.Logging;
using TaskStatus = io.NET.ZTR_OS.Features.Scheduler.Models.TaskStatus;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

/// <summary>
/// 计划任务调度服务
/// </summary>
public class SchedulerService : ISchedulerService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, ScheduledTask> _tasks = new();
    private readonly List<ExecutionRecord> _executionHistory = new();
    private readonly object _historyLock = new();
    private const int MaxExecutionHistory = 500;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _taskSemaphores = new();
    private readonly ILogger<SchedulerService> _logger;
    private readonly INotificationService _notificationService;
    private readonly ISchedulerStorageService _storage;
    private Timer? _timer;
    private bool _isRunning;
    private bool _disposed;

    public SchedulerService(
        ILogger<SchedulerService> logger, 
        INotificationService notificationService,
        ISchedulerStorageService storage)
    {
        _logger = logger;
        _notificationService = notificationService;
        _storage = storage;
    }

    /// <summary>
    /// 启动调度器
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            _logger.LogDebug("[Scheduler] Already running");
            return;
        }

        _logger.LogInformation("[Scheduler] Starting scheduler...");
        
        try
        {
            // 加载持久化任务
            var savedTasks = _storage.LoadAll();
            foreach (var task in savedTasks)
            {
                AddTaskInternal(task, fromStorage: true);
            }
            _logger.LogInformation("[Scheduler] Loaded {Count} tasks from storage", savedTasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Failed to load saved tasks");
        }

        // 计算所有任务的 NextRunTime
        RecalculateAllNextRunTimes();

        // 启动定时器（每 30 秒扫描一次）
        _timer = new Timer(ScanAndExecute, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        _isRunning = true;
        _logger.LogInformation("[Scheduler] Scheduler started successfully");
    }

    /// <summary>
    /// 停止调度器
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _logger.LogInformation("[Scheduler] Stopping scheduler...");
        _timer?.Dispose();
        _isRunning = false;
        
        // 保存任务状态
        try
        {
            _storage.SaveAll(_tasks.Values);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Failed to save tasks on stop");
        }
        
        _logger.LogInformation("[Scheduler] Scheduler stopped");
    }

    private void ScanAndExecute(object? state)
    {
        if (!_isRunning || _disposed) return;

        try
        {
            var now = DateTimeOffset.UtcNow;
            var dueTasks = _tasks.Values
                .Where(t => t.Enabled && t.NextRunTime.HasValue && t.NextRunTime.Value <= now)
                .ToList();

            if (!dueTasks.Any()) return;

            _logger.LogDebug("[Scheduler] Found {Count} tasks due", dueTasks.Count);
            
            foreach (var task in dueTasks)
            {
                _ = ExecuteTaskAsync(task);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Error during scan cycle");
        }
    }

    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        var semaphore = _taskSemaphores.GetOrAdd(task.Id, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0))
        {
            _logger.LogWarning("[Scheduler] Task {Name} already running, skipping", task.Name);
            return;
        }

        var record = new ExecutionRecord
        {
            TaskId = task.Id,
            TaskName = task.Name,
            Status = TaskStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogInformation("[Scheduler] Executing task: {Name}", task.Name);
            
            switch (task.Action.Type)
            {
                case ActionType.SendNotification:
                    await ExecuteNotificationAction(task);
                    break;
                    
                case ActionType.RunCommand:
                    await ExecuteCommandAction(task);
                    break;
                    
                case ActionType.RunBackup:
                    await ExecuteBackupAction(task);
                    break;
                    
                default:
                    _logger.LogWarning("[Scheduler] Unknown action type: {Type}", task.Action.Type);
                    break;
            }

            // 成功
            record.Status = TaskStatus.Completed;
            record.CompletedAt = DateTimeOffset.UtcNow;
            record.Duration = record.CompletedAt.Value - record.StartedAt;
            task.LastStatus = TaskStatus.Completed;
            task.LastRunTime = record.CompletedAt.Value;
            task.ConsecutiveFailures = 0;
            task.TotalRunCount++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Task {Name} failed", task.Name);
            record.Status = TaskStatus.Failed;
            record.CompletedAt = DateTimeOffset.UtcNow;
            record.Duration = record.CompletedAt.Value - record.StartedAt;
            record.ErrorMessage = ex.Message;
            task.LastStatus = TaskStatus.Failed;
            task.ConsecutiveFailures++;
            
            // 失败次数超阈值则自动禁用
            if (task.ConsecutiveFailures >= task.MaxConsecutiveFailures)
            {
                _logger.LogWarning("[Scheduler] Task {Name} auto-disabled after {Failures} failures", 
                    task.Name, task.ConsecutiveFailures);
                task.Enabled = false;
                
                // 发送告警通知（失败时通知本身的失败不应阻断任务状态更新）
                try
                {
                    await _notificationService.DispatchAsync(new NotificationEvent
                    {
                        EventType = NotificationEventType.SystemAlert,
                        Title = $"Task Auto-Disabled: {task.Name}",
                        Message = $"Task has been disabled after {task.ConsecutiveFailures} consecutive failures.",
                        SourceModule = "Scheduler"
                    });
                }
                catch (Exception notifEx)
                {
                    _logger.LogError(notifEx, "[Scheduler] Failed to send auto-disable notification for task {Name}", task.Name);
                }
            }
        }
        finally
        {
            lock (_historyLock)
            {
                _executionHistory.Add(record);

                // 限制历史记录数量，超过上限后移除最早的记录
                if (_executionHistory.Count > MaxExecutionHistory)
                {
                    // 按时间排序后移除最早的记录，只保留最新的 MaxExecutionHistory 条
                    _executionHistory.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));
                    _executionHistory.RemoveRange(MaxExecutionHistory, _executionHistory.Count - MaxExecutionHistory);
                }
            }
            
            // 计算下次运行时间
            task.NextRunTime = CalculateNextRunTime(task);
            
            // 持久化状态
            try
            {
                _storage.SaveAll(_tasks.Values);
            }
            catch { /* 持久化失败不影响执行 */ }
            
            semaphore.Release();
        }
    }

    private async Task ExecuteNotificationAction(ScheduledTask task)
    {
        var evt = new NotificationEvent
        {
            EventType = NotificationEventType.ScheduleCompleted,
            Title = $"Scheduled Task: {task.Name}",
            Message = task.Action.CommandOrPath ?? "Scheduled task executed",
            SourceModule = "Scheduler"
        };
        
        var result = await _notificationService.DispatchAsync(evt);
        if (!result.IsSuccess || result.SuccessfulChannels == 0)
        {
            throw new InvalidOperationException($"Notification dispatch failed: {result.SuccessfulChannels}/{result.TotalChannels} channels succeeded");
        }
    }

    private Task ExecuteCommandAction(ScheduledTask task)
    {
        // TODO: 实现命令执行
        _logger.LogInformation("[Scheduler] Command execution not yet implemented: {Command}", 
            task.Action.CommandOrPath);
        return Task.CompletedTask;
    }

    private Task ExecuteBackupAction(ScheduledTask task)
    {
        // TODO: 实现备份逻辑
        _logger.LogInformation("[Scheduler] Backup not yet implemented for task: {Name}", task.Name);
        return Task.CompletedTask;
    }

    private DateTimeOffset? CalculateNextRunTime(ScheduledTask task)
    {
        if (!task.Enabled || task.Trigger == null) return null;

        var now = DateTimeOffset.UtcNow;
        
        switch (task.Trigger.Type)
        {
            case TriggerType.Interval:
                return now + task.Trigger.Interval;
                
            case TriggerType.Cron:
                if (!string.IsNullOrEmpty(task.Trigger.CronExpression))
                {
                    return CronParser.GetNextRunTime(task.Trigger.CronExpression, now);
                }
                break;
                
            case TriggerType.OneTime:
                // 一次性任务执行后不再触发
                return null;
        }

        return null;
    }

    private void RecalculateAllNextRunTimes()
    {
        foreach (var task in _tasks.Values)
        {
            task.NextRunTime = CalculateNextRunTime(task);
        }
    }

    public IReadOnlyList<ScheduledTask> GetAllTasks() => _tasks.Values.ToList();
    
    public ScheduledTask? GetTask(Guid taskId) => _tasks.TryGetValue(taskId, out var task) ? task : null;

    public void AddTask(ScheduledTask task)
    {
        AddTaskInternal(task);
    }

    private void AddTaskInternal(ScheduledTask task, bool fromStorage = false)
    {
        if (task.Id == Guid.Empty)
        {
            task.Id = Guid.NewGuid();
        }
        
        task.NextRunTime = CalculateNextRunTime(task);
        _tasks[task.Id] = task;
        
        _logger.LogInformation("[Scheduler] Task added: {Name} (Id={Id}, NextRun={Next})", 
            task.Name, task.Id, task.NextRunTime);
        
        if (!fromStorage)
        {
            _storage.SaveAll(_tasks.Values);
        }
    }

    public void UpdateTask(ScheduledTask task)
    {
        if (!_tasks.ContainsKey(task.Id))
        {
            _logger.LogWarning("[Scheduler] Task not found for update: {Id}", task.Id);
            return;
        }
        
        task.NextRunTime = CalculateNextRunTime(task);
        _tasks[task.Id] = task;
        _storage.SaveAll(_tasks.Values);
        
        _logger.LogInformation("[Scheduler] Task updated: {Name}", task.Name);
    }

    public bool DeleteTask(Guid taskId)
    {
        if (_tasks.TryRemove(taskId, out var task))
        {
            _storage.SaveAll(_tasks.Values);
            _logger.LogInformation("[Scheduler] Task deleted: {Name}", task.Name);
            return true;
        }
        return false;
    }

    public async Task<bool> RunNowAsync(Guid taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            _logger.LogWarning("[Scheduler] Task not found: {Id}", taskId);
            return false;
        }

        if (!task.Enabled)
        {
            _logger.LogWarning("[Scheduler] Task is disabled: {Name}", task.Name);
            return false;
        }

        await ExecuteTaskAsync(task);
        return true;
    }

    public IReadOnlyList<ExecutionRecord> GetExecutionHistory(int maxRecords = 100)
    {
        lock (_historyLock)
        {
            return _executionHistory
                .OrderByDescending(r => r.StartedAt)
                .Take(maxRecords)
                .ToList();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
    }
}
