// -----------------------------------------------------------------------------
// 文件名: INotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Services
// 功能描述: 通知服务接口 —— 全项目唯一的通知触发入口
// -----------------------------------------------------------------------------

using io.NET.ZTR_OS.Features.Notifications.Models;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

public interface INotificationService
{
    Task<NotificationDispatchResult> DispatchAsync(NotificationEvent evt, CancellationToken ct = default);
}
