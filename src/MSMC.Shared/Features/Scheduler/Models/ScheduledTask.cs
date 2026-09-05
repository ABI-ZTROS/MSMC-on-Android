// -----------------------------------------------------------------------------
// 文件名: ScheduledTask.cs
// 命名空间: io.NET.ZTR_OS.Features.Scheduler.Models
// 功能描述: 计划任务模型 —— 因果链的“因”（Trigger）与“果”（Action）
// 设计模式: 三链原则 - 因果链：Trigger（因）→ Action（果）
// -----------------------------------------------------------------------------

namespace io.NET.ZTR_OS.Features.Scheduler.Models;

/// <summary>
/// 触发器类型
/// </summary>
public enum TriggerType
{
    Cron,
    Interval,
    OneTime
}

/// <summary>
/// 任务动作类型
/// </summary>
public enum ActionType
{
    ServerStart,
    ServerStop,
    ServerRestart,
    RunCommand,
    RunBackup,
    RunScript,
    SendNotification,
    PowerOff
}

/// <summary>
/// 任务状态
/// </summary>
public enum TaskStatus
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// 触发器配置
/// </summary>
public class TriggerConfig
{
    public TriggerType Type { get; set; } = TriggerType.Cron;
    public string? CronExpression { get; set; }
    public TimeSpan? Interval { get; set; }
    public DateTimeOffset? OneTimeAt { get; set; }
}

/// <summary>
/// 任务动作配置
/// </summary>
public class ActionConfig
{
    public ActionType Type { get; set; }
    public string? TargetServerId { get; set; }
    public string? CommandOrPath { get; set; }
    public string? Arguments { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// 计划任务
/// </summary>
public class ScheduledTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public TriggerConfig Trigger { get; set; } = new();
    public ActionConfig Action { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextRunTime { get; set; }
    public DateTimeOffset? LastRunTime { get; set; }
    public TaskStatus LastStatus { get; set; } = TaskStatus.Idle;
    public string? LastErrorMessage { get; set; }
    public int TotalRunCount { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int MaxConsecutiveFailures { get; set; } = 10;
}

/// <summary>
/// 单次执行记录
/// </summary>
public class ExecutionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}
