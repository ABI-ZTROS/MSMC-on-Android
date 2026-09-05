// -----------------------------------------------------------------------------
// 文件名: BootReceiver.cs
// 命名空间: io.NET.ZTR_OS.Android
// 功能描述: 开机自启 —— 收到 BOOT_COMPLETED 后拉起前台服务（M4）。
// -----------------------------------------------------------------------------
using Android.Content;
using Serilog;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 开机自启接收器
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[global::Android.App.IntentFilter(
    new[] { Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON" })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        try
        {
            Log.Information("[BOOT-RECV] 收到 {Action}，拉起前台服务", intent?.Action);
            var svc = new Intent(context, typeof(SupervisorService));
            svc.SetAction(SupervisorService.ActionStart);
#pragma warning disable CA1416
            context?.StartForegroundService(svc);
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[BOOT-RECV] 启动服务失败");
        }
    }
}