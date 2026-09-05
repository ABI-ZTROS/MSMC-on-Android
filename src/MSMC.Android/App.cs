using Android.App;
using Android.OS;
using io.NET.ZTR_OS.Android.Monitoring;
using io.NET.ZTR_OS.Android.Notifications;
using io.NET.ZTR_OS.Android.Runtime;
using io.NET.ZTR_OS.Android.Supervision;
using io.NET.ZTR_OS.Features.ContentMarket.Services;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;
using io.NET.ZTR_OS.Features.Scheduler.Services;
using io.NET.ZTR_OS.Features.Settings.Services;
using io.NET.ZTR_OS.Features.Startup.Services;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 应用宿主：初始化日志与 DI 容器，作为全局服务定位器。
/// </summary>
[Application]
public class App : Application
{
    internal const string Tag = "MSMC.Android";

    /// <summary>全局 DI 服务提供者</summary>
    public static IServiceProvider Services { get; private set; } = default!;

    public App(IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        // 日志：App 私有目录
        var logDir = Path.Combine(FilesDir?.AbsolutePath ?? "/data/user/0/io.net.ztr_os.msmc/files", "logs");
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDir, "msmc-android-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();
        Log.Information("[BOOT] MSMC on Android 启动 Flavor={Flavor}",
#if MSMC_EXTERNAL
            "external");
#else
            "internal");
#endif

        // DI：复用 MSMC.Shared 纯逻辑 + Android 系统服务
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSerilog(dispose: true));
        services.AddSingleton<TimeService>();

        // Android 基础服务
        services.AddSingleton(_ => this);
        services.AddSingleton(_ => new TermuxRuntime(this));
        services.AddSingleton<JavaRuntimeManager>();
        services.AddSingleton<AndroidSystemMonitor>();
        services.AddSingleton<AndroidProcessScanner>();
        services.AddSingleton<AndroidPortMapper>();
        services.AddSingleton<AndroidPowerManager>();
        services.AddSingleton<AndroidNetworkManager>();
        services.AddSingleton<AndroidToastService>();

        // 通知模块（跨平台）
        services.AddSingleton<IDiscordWebhookSender, DiscordWebhookSender>();
        services.AddSingleton<GenericWebhookSender>();
        services.AddSingleton<EmailNotificationService>();
        services.AddSingleton<NotificationChannelConfig>();
        services.AddSingleton<IToastNotificationService>(sp => sp.GetRequiredService<AndroidToastService>());
        services.AddSingleton<INotificationService, NotificationService>();
        // 调度器（跨平台）
        services.AddSingleton<ISchedulerStorageService, SchedulerStorageService>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        // 指标持久化（跨平台）
        services.AddSingleton<IMetricsPersistenceService, MetricsPersistenceService>();
        // 市场模块（跨平台多源聚合）
        services.AddSingleton<IMarketProvider, ModrinthProvider>();
        services.AddSingleton<IMarketProvider, HangarProvider>();
        services.AddSingleton<IMarketProvider, SpigetProvider>();
        services.AddSingleton<MarketProviderFactory>();
        services.AddSingleton<PluginManagerService>();
        // 监管器（依赖 Termux + Java 运行时）
        services.AddSingleton(sp => new AndroidSupervisor(
            sp.GetRequiredService<TermuxRuntime>(),
            sp.GetRequiredService<JavaRuntimeManager>()));

        Services = services.BuildServiceProvider();
        Log.Information("[BOOT] DI 容器构建完成");
    }
}