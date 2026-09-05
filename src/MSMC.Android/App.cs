using Android.App;
using Android.OS;
using Serilog;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 应用宿主：初始化日志，M0 阶段不建 DI 全量容器（M2 接入 Shared 服务时再补）。
/// </summary>
[Application]
public class App : Application
{
    internal const string Tag = "MSMC.Android";

    public App(IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        var logDir = Path.Combine(GetFilesDir()?.AbsolutePath ?? "/data/user/0/io.net.ztr_os.msmc/files", "logs");
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
    }
}