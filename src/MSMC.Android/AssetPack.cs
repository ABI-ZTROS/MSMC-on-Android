// -----------------------------------------------------------------------------
// 文件名: AssetPack.cs
// 命名空间: io.NET.ZTR_OS.Android
// 功能描述: 内置资源装配 —— 把 APK assets 里的前端 dist 解压到私有目录，
//           供 WebPanel 静态托管（M2 起前端真正跑起来）。
// -----------------------------------------------------------------------------
using System.IO.Compression;
using Android.Content;
using Serilog;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 内置资源装配器
/// </summary>
public static class AssetPack
{
    /// <summary>前端 dist 内置 asset 名</summary>
    public const string WwwAsset = "www.zip";

    /// <summary>前端静态目录（App 私有）</summary>
    public static string WebRoot(Context ctx)
        => Path.Combine(ctx.FilesDir?.AbsolutePath ?? "/data/user/0/io.net.ztr_os.msmc/files", "web");
    /// <summary>
    /// 解压内置前端到私有目录；无内置 asset 时返回 false（WebPanel 显示占位页）
    /// </summary>
    public static bool ExtractWeb(Context ctx)
    {
        var root = WebRoot(ctx);
        var marker = Path.Combine(root, "index.html");
        if (File.Exists(marker))
        {
            return true;
        }

        try
        {
            using var src = ctx.Assets!.Open(WwwAsset);
            using var zip = new ZipArchive(src, ZipArchiveMode.Read);
            Directory.CreateDirectory(root);
            foreach (var entry in zip.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!target.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal)) continue;

                if (entry.Name.Length == 0)
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var es = entry.Open();
                using var fs = File.Create(target);
                es.CopyTo(fs);
            }
            Log.Information("[WWW] 前端已装配 {Root}", root);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WWW] 前端 asset 缺失或解压失败，使用占位页");
            return false;
        }
    }
}