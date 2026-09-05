// -----------------------------------------------------------------------------
// 文件名: MsmcColor.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Colors
// 功能描述: 跨平台 RGBA 颜色结构体 —— 替代 WPF 的 System.Windows.Media.Color，
//          保证 MSMC.Shared 可在 Linux/Windows 全平台编译
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.Settings.Colors;

/// <summary>
/// 跨平台 RGBA 颜色（0-255 分量）
/// </summary>
public readonly struct MsmcColor
{
    public byte A { get; }
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    public MsmcColor(byte a, byte r, byte g, byte b)
    {
        A = a;
        R = r;
        G = g;
        B = b;
    }

    public static MsmcColor FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public static MsmcColor FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);

    /// <summary>默认蓝色（原 WPF Colors.Blue）</summary>
    public static MsmcColor Blue => new(255, 0, 0, 255);
}