// -----------------------------------------------------------------------------
// 文件名: SupervisorService.cs
// 命名空间: io.NET.ZTR_OS.Android
// 功能描述: 前台常驻服务 —— 整个 MSMC 管理面：
//           ① 常驻通知（保活 + 状态）
//           ② 启动 WebPanel（内网 0.0.0.0:8080，token 鉴权，托管前端）
//           ③ 确保 Termux + JDK 运行时就绪
//           ④ 开服成功自动调起系统浏览器访问面板（M2 核心闭环）
// 设计模式: 前台服务 + 单例状态机
// -----------------------------------------------------------------------------
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using io.NET.ZTR_OS.Android.Notifications;
using io.NET.ZTR_OS.Android.Root;
using io.NET.ZTR_OS.Android.Runtime;
using io.NET.ZTR_OS.Features.WebPanel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// MSMC 前台常驻服务
/// </summary>
[Service(Name = "io.net.ztr_os.msmc.SupervisorService", Exported = true, ForegroundServiceType = ForegroundService.TypeDataSync)]
[global::Android.App.IntentFilter(
    new[] { ActionStart, ActionOpenPanel })]
public class SupervisorService : Service
{
    public const string ActionStart = "io.net.ztr_os.msmc.action.START";
    public const string ActionOpenPanel = "io.net.ztr_os.msmc.action.OPEN_PANEL";
    public const string ExtraToken = "msmc_panel_token";

    private WebPanel? _panel;
    private TermuxRuntime? _termux;
    private JavaRuntimeManager? _javaManager;
    private AndroidToastService? _toast;
    private bool _runtimeReady;

    /// <summary>当前面板地址（局域网 IP:8080 或本机回环）</summary>
    public static string PanelUrl { get; private set; } = string.Empty;

    public override void OnCreate()
    {
        base.OnCreate();
        _toast = App.Services.GetRequiredService<AndroidToastService>();
        _toast.Initialize();
        _panel = new WebPanel
        {
            ListenAddress = System.Net.IPAddress.Any,
            Token = GetOrCreateToken(),
        };
        _termux = App.Services.GetRequiredService<TermuxRuntime>();
        _javaManager = App.Services.GetRequiredService<JavaRuntimeManager>();
        Log.Information("[SVC] 前台服务创建");
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // 前台通知（必须尽快 startForeground）
        StartForeground(AndroidToastService.ForegroundNotificationId, BuildForegroundNotification());

        switch (intent?.Action)
        {
            case ActionOpenPanel:
                OpenBrowser();
                break;
            case ActionStart:
            default:
                _ = StartAsync();
                break;
        }

        return StartCommandResult.Sticky;
    }

    /// <summary>启动管理面（幂等）</summary>
    private async Task StartAsync()
    {
        if (_panel!.IsRunning) return;

        try
        {
            // 1. 前端装配
            AssetPack.ExtractWeb(this);

            // 2. 面板启动（端口 8080，被占用自动换随机）
            var port = 8080;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    _panel.Start(port);
                    break;
                }
                catch (Exception)
                {
                    port = 0; // 随机空闲端口
                    if (attempt == 4) throw;
                }
            }

            // 3. 注册 action
            var logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WebPanel");
            AndroidBridgeActionRegistrar.RegisterAll(_panel, App.Services, logger);

            PanelUrl = $"http://{(IsLocalNetworkAvailable ? GetLocalIp() : "127.0.0.1")}:{_panel.Port}";
            Log.Information("[SVC] 面板已启动 {Url} Token={Token}", PanelUrl, _panel.Token);
            _toast!.ShowInfo("MSMC", $"管理面板已启动：{PanelUrl}");

            // 4. 运行时装配（异步，不阻塞面板）
            _ = Task.Run(EnsureRuntimeAsync);

            // 5. 开服成功 → 自动开浏览器（事件推送时由前端触发，这里注册兜底回调）
            _panel.PublishEvent("app:ready", new { url = PanelUrl, token = _panel.Token });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SVC] 面板启动失败");
            _toast!.ShowError("MSMC", $"面板启动失败：{ex.Message}");
        }
    }

    /// <summary>确保 Termux + 默认 JDK 就绪（失败仅提示，不阻断面板）</summary>
    private async Task EnsureRuntimeAsync()
    {
        void Report(string m) => _panel?.PublishEvent("runtime:progress", new { message = m });

        Report("开始装配运行环境…");
        var termuxOk = await _termux!.EnsureInstalledAsync(Report);
        if (termuxOk)
        {
            var major = JavaRuntimeManager.BundledMajors.Contains(21) ? 21 : JavaRuntimeManager.BundledMajors[0];
            var javaPath = await _javaManager!.EnsureAsync(major, Report);
            _runtimeReady = javaPath is not null;
            Report(_runtimeReady ? "运行环境就绪" : "JDK 就绪失败（可稍后在面板重试）");
        }
        else
        {
            Report("Termux 装配失败，请检查网络后重试");
        }
    }

    /// <summary>调起系统浏览器访问面板（ACTION_VIEW）</summary>
    public void OpenBrowser()
    {
        try
        {
            var url = $"http://127.0.0.1:{_panel?.Port ?? 8080}";
            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url)!);
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
            Log.Information("[SVC] 已调起浏览器 {Url}", url);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SVC] 调起浏览器失败");
        }
    }

    private Notification BuildForegroundNotification()
    {
        var openIntent = new Intent(this, typeof(SupervisorService));
        openIntent.SetAction(ActionOpenPanel);
        var pi = PendingIntent.GetService(this, 0, openIntent, PendingIntentFlags.Immutable);

        var builder = new Notification.Builder(this, AndroidToastService.ChannelId)
            .SetContentTitle("MSMC 服务器管理")
            .SetContentText("后台运行中 · 点击打开管理面板")
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyMore)
            .SetContentIntent(pi)
            .SetOngoing(true);

        return builder.Build();
    }

    private string GetOrCreateToken()
    {
        var prefs = GetSharedPreferences("msmc", FileCreationMode.Private)!;
        var existing = prefs.GetString(ExtraToken, null);
        if (!string.IsNullOrEmpty(existing)) return existing;

        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        prefs.Edit()!.PutString(ExtraToken, token)!.Apply();
        return token;
    }

    private static bool IsLocalNetworkAvailable => true;

    private static string GetLocalIp()
    {
        try
        {
            var interfaces = Java.Net.NetworkInterface.NetworkInterfaces;
            while (interfaces.HasMoreElements)
            {
                var addr = interfaces.NextElement() as Java.Net.NetworkInterface;
                if (addr is null || addr.IsLoopback || !addr.IsUp) continue;

                var inets = addr.InetAddresses;
                while (inets.HasMoreElements)
                {
                    var ip = inets.NextElement() as Java.Net.Inet4Address;
                    if (ip is not null && !string.IsNullOrEmpty(ip.HostAddress))
                    {
                        return ip.HostAddress;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 取不到就回环
        }
        return "127.0.0.1";
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        try { _panel?.Dispose(); } catch (Exception) { }
        Log.Information("[SVC] 前台服务销毁");
        base.OnDestroy();
    }
}