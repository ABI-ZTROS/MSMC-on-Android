// -----------------------------------------------------------------------------
// 文件名: NotificationChannel.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Models
// 功能描述: 通知通道配置模型（Discord / GenericWebhook / Email / WindowsToast）
// 设计模式: 三链原则 - 因果链：明确通道类型与触发事件；执行链：可独立配置与测试
// -----------------------------------------------------------------------------

namespace io.NET.ZTR_OS.Features.Notifications.Models;

/// <summary>
/// 通知通道类型
/// </summary>
public enum NotificationChannelType
{
    DiscordWebhook,
    GenericWebhook,
    Email,
    WindowsToast,
    SystemTray
}

/// <summary>
/// 通知事件类型（因果链的“果”）
/// </summary>
public enum NotificationEventType
{
    ServerStarted,
    ServerStopped,
    ServerCrashed,
    BackupCompleted,
    BackupFailed,
    PluginInstalled,
    PluginUpdateAvailable,
    ScheduleCompleted,
    ManualTest,
    SystemAlert
}

/// <summary>
/// 通知通道配置（聚合根）
/// </summary>
public class NotificationChannelConfig
{
    public DiscordChannelConfig Discord { get; set; } = new();
    public GenericWebhookChannelConfig GenericWebhook { get; set; } = new();
    public EmailChannelConfig Email { get; set; } = new();
    public ToastChannelConfig WindowsToast { get; set; } = new();
    public int RetryMaxAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 1000;
}

/// <summary>
/// Discord 通道配置
/// </summary>
public class DiscordChannelConfig
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string BotName { get; set; } = "MSMC Bot";
    public bool EnableOnCrash { get; set; } = true;
    public bool EnableOnStartStop { get; set; } = true;
    public bool EnableOnBackup { get; set; } = true;
    public bool EnableOnPlugin { get; set; } = true;
    public bool EnableOnSchedule { get; set; } = true;
}

/// <summary>
/// 通用 Webhook 通道配置
/// </summary>
public class GenericWebhookChannelConfig
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = string.Empty;
    public string AuthorizationHeader { get; set; } = string.Empty;
    public string CustomPayloadJson { get; set; } = string.Empty;
}

/// <summary>
/// 邮件通道配置
/// </summary>
public class EmailChannelConfig
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddresses { get; set; } = string.Empty;
}

/// <summary>
/// Windows Toast 通道配置
/// </summary>
public class ToastChannelConfig
{
    public bool Enabled { get; set; } = true;
}
