using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 极简门面：显示 root 状态与版本信息。M0 无管理功能，M2 起承载网页面板入口。
/// </summary>
[Activity(Label = "MSMC on Android", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetTheme(Android.Resource.Style.ThemeDeviceDefaultDark);

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Gravity = GravityFlags.Center,
        };
        layout.SetPadding(48, 48, 48, 48);

        var title = new TextView(this)
        {
            Text = "MSMC on Android",
            TextSize = 24f,
            Gravity = GravityFlags.Center,
        };
        var status = new TextView(this)
        {
            Text = $"Flavor: {(IsExternal ? "external（非内置版）" : "internal（内置版）")}\n"
                 + $"Pid: {Android.OS.Process.MyPid()}",
            TextSize = 15f,
            Gravity = GravityFlags.Center,
        };
        status.SetTextColor(Android.Graphics.Color.Gray);
        var hint = new TextView(this)
        {
            Text = "M0 骨架 · 网页面板与开服能力将在后续里程碑上线（M2）。",
            TextSize = 13f,
            Gravity = GravityFlags.Center,
        };
        hint.SetTextColor(Android.Graphics.Color.Gray);

        layout.AddView(title);
        layout.AddView(status);
        layout.AddView(hint);

        SetContentView(layout);
    }

    private static bool IsExternal =>
#if MSMC_EXTERNAL
        true;
#else
        false;
#endif
}