// -----------------------------------------------------------------------------
// 文件名: ISchedulerService.cs
// 命名空间: io.NET.ZTR_OS.Features.Scheduler.Services
// 功能描述: 计划任务调度服务接口
// -----------------------------------------------------------------------------

using io.NET.ZTR_OS.Features.Scheduler.Models;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

public interface ISchedulerService
{
    void Start();
    void Stop();
    IReadOnlyList<ScheduledTask> GetAllTasks();
    ScheduledTask? GetTask(Guid taskId);
    void AddTask(ScheduledTask task);
    void UpdateTask(ScheduledTask task);
    bool DeleteTask(Guid taskId);
    Task<bool> RunNowAsync(Guid taskId);
    IReadOnlyList<ExecutionRecord> GetExecutionHistory(int maxRecords = 100);
}
