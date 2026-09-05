// -----------------------------------------------------------------------------
// 文件名: NotificationEvent.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Models
// 功能描述: 通知事件模型 —— 因果链的“因”
// 设计模式: 三链原则 - 因果链：明确事件类型与触发源
// -----------------------------------------------------------------------------

namespace io.NET.ZTR_OS.Features.Notifications.Models;

/// <summary>
/// 通知事件（因 -> 果的“因”）
/// </summary>
public class NotificationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NotificationEventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SourceModule { get; set; }
    public string? TargetServerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
