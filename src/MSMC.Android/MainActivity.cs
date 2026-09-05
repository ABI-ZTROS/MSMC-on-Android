using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using io.NET.ZTR_OS.Android.Root;
using io.NET.ZTR_OS.Android.Runtime;
using Serilog;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 极简门面：root 状态 + 运行时状态 + 面板入口。
/// 本 App 无 GUI 管理页，管理在浏览器里 —— 这里只做「开机 + 状态 + 打开面板」。
/// </summary>
[Activity(Label = "MSMC on Android", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    private TextView? _status;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
        };
        layout.SetPadding(48, 48, 48, 48);

        var title = new TextView(this)
        {
            Text = "MSMC on Android",
            TextSize = 24f,
            Gravity = GravityFlags.Center,
        };

        var hint = new TextView(this)
        {
            Text = "强制 root · 内置 Termux + JDK · 内网页管理",
            TextSize = 13f,
            Gravity = GravityFlags.Center,
        };
        hint.SetTextColor(global::Android.Graphics.Color.Gray);

        _status = new TextView(this)
        {
            Text = "加载中…",
            TextSize = 14f,
            Gravity = GravityFlags.Center,
        };
        _status.SetTextColor(global::Android.Graphics.Color.Gray);

        var startBtn = new Button(this) { Text = "启动管理服务" };
        startBtn.Click += (_, _) =>
        {
            StartSupervisorService();
            RefreshStatus();
        };

        var openBtn = new Button(this) { Text = "打开管理面板（浏览器）" };
        openBtn.Click += (_, _) =>
        {
            var svc = new Intent(this, typeof(SupervisorService));
            svc.SetAction(SupervisorService.ActionOpenPanel);
            StartService(svc);
        };

        var stopBtn = new Button(this) { Text = "停止管理服务" };
        stopBtn.Click += (_, _) =>
        {
            StopService(new Intent(this, typeof(SupervisorService)));
            RefreshStatus();
        };

        layout.AddView(title);
        layout.AddView(hint);
        layout.AddView(_status);
        layout.AddView(startBtn);
        layout.AddView(openBtn);
        layout.AddView(stopBtn);

        SetContentView(layout);

        // 首次启动自动拉起前台服务（设计：装完打开即开始管理）
        StartSupervisorService();
        RefreshStatus();
    }

    protected override void OnResume()
    {
        base.OnResume();
        RefreshStatus();
    }

    private void StartSupervisorService()
    {
        try
        {
            var svc = new Intent(this, typeof(SupervisorService));
            svc.SetAction(SupervisorService.ActionStart);
#pragma warning disable CA1416
            StartForegroundService(svc);
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MAIN] 启动前台服务失败");
        }
    }

    private void RefreshStatus()
    {
        if (_status is null) return;

        var termux = App.Services.GetService(typeof(TermuxRuntime)) as TermuxRuntime;
        var java = App.Services.GetService(typeof(JavaRuntimeManager)) as JavaRuntimeManager;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Flavor: {(IsExternal ? "external（非内置版）" : "internal（内置版）")}");
        sb.AppendLine($"Root: {(RootService.IsGranted ? "已授权 ✅" : "未授权 ❌")}");
        sb.AppendLine($"Termux: {(termux?.IsInstalled == true ? "已就绪 ✅" : "未就绪 ⏳")}");
        if (java is not null)
        {
            var javas = java.ScanInstalled();
            sb.AppendLine(javas.Count > 0
                ? $"JDK: {string.Join(", ", javas.Select(j => j.Major))} ✅"
                : "JDK: 未就绪 ⏳");
        }
        sb.AppendLine($"面板: {(SupervisorService.PanelUrl.Length > 0 ? SupervisorService.PanelUrl : "未启动")}");
        _status.Text = sb.ToString();
    }

    private static bool IsExternal =>
#if MSMC_EXTERNAL
        true;
#else
        false;
#endif
}