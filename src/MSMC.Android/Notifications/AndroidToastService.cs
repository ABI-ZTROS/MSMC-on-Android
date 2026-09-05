// -----------------------------------------------------------------------------
// 文件名: AndroidToastService.cs
// 命名空间: io.NET.ZTR_OS.Android.Notifications
// 功能描述: Android 通知服务 —— 实现 IToastNotificationService 契约，
//           用 Android 通知渠道投递系统气泡（前台服务常驻通知也在同渠道）。
// -----------------------------------------------------------------------------
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using io.NET.ZTR_OS.Features.Settings.Services;

namespace io.NET.ZTR_OS.Android.Notifications;

/// <summary>
/// Android 通知服务（IToastNotificationService 的 Android 实现）
/// </summary>
public sealed class AndroidToastService : IToastNotificationService
{
    public const string ChannelId = "msmc_servers";
    public const string NotificationTag = "msmc_supervisor";
    public const int ForegroundNotificationId = 1001;

    private readonly Context _context;
    private NotificationManager? _manager;

    public AndroidToastService(Context context)
    {
        _context = context;
    }

    private NotificationManager Manager => _manager ??= (NotificationManager)_context.GetSystemService(Context.NotificationService)!;

    /// <inheritdoc />
    public void Initialize()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "MSMC 服务器", NotificationImportance.Low)
            {
                Description = "MSMC 服务器运行状态与事件通知",
            };
            channel.EnableVibration(false);
            Manager.CreateNotificationChannel(channel);
        }
    }

    /// <inheritdoc />
    public void ShowInfo(string title, string message, Action<string>? onActivated = null)
        => Show(title, message, NotificationCompatImportance.Low, onActivated);

    /// <inheritdoc />
    public void ShowSuccess(string title, string message, Action<string>? onActivated = null)
        => Show(title, message, NotificationCompatImportance.Default, onActivated);

    /// <inheritdoc />
    public void ShowWarning(string title, string message, Action<string>? onActivated = null)
        => Show(title, message, NotificationCompatImportance.High, onActivated);

    /// <inheritdoc />
    public void ShowError(string title, string message, Action<string>? onActivated = null)
        => Show(title, message, NotificationCompatImportance.High, onActivated);

    /// <inheritdoc />
    public void ClearAll()
    {
        try { Manager.CancelAll(); } catch (Exception) { }
    }

    private void Show(string title, string message, NotificationCompatImportance importance, Action<string>? onActivated)
    {
        try
        {
            var builder = new Notification.Builder(_context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyMore)
                .SetAutoCancel(true);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                builder.SetVisibility(NotificationVisibility.Public);
            }

            var intent = _context.PackageManager.GetLaunchIntentForPackage(_context.PackageName!);
            if (intent is not null)
            {
                var pi = PendingIntent.GetActivity(_context, 0, intent, PendingIntentFlags.Immutable);
                builder.SetContentIntent(pi);
            }

            Manager.Notify(NotificationTag, NextId(), builder.Build());
        }
        catch (Exception)
        {
            // 通知失败不影响核心功能
        }
    }

    private static int _notificationId = 1002;
    private static int NextId() => System.Threading.Interlocked.Increment(ref _notificationId);
}

/// <summary>轻量 NotificationImportance 对齐</summary>
public enum NotificationCompatImportance
{
    Low,
    Default,
    High,
}