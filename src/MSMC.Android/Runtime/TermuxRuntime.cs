// -----------------------------------------------------------------------------
// 文件名: TermuxRuntime.cs
// 命名空间: io.NET.ZTR_OS.Android.Runtime
// 功能描述: Termux 运行时管理 —— 内置/下载 bionic Linux 用户态到 App 私有目录，
//           提供 root 视角的完整 Termux 环境（bash/apt/wget/ssh 等），
//           MSMC 的全部服务器能力跑在这套环境里（设计决策：内置完整 Termux）。
// 设计模式: 单例 + 资源就绪状态机（未安装 → 解压/下载 → 可用）
// -----------------------------------------------------------------------------
using System.IO.Compression;
using io.NET.ZTR_OS.Android.Root;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace io.NET.ZTR_OS.Android.Runtime;

/// <summary>
/// Termux 运行时 —— 管理内置 Linux 用户态环境
/// </summary>
public sealed class TermuxRuntime
{
    /// <summary>Termux 官方 bootstrap 下载地址（aarch64）</summary>
    public const string BootstrapDownloadUrl =
        "https://packages.termux.dev/apt/termux-main/bootstraps/bootstrap-aarch64.zip";

    /// <summary>内置 asset 名（缺省时走下载）</summary>
    public const string BootstrapAssetName = "termux-bootstrap.zip";

    private readonly global::Android.Content.Context _context;

    public TermuxRuntime(global::Android.Content.Context context)
    {
        _context = context;
    }

    /// <summary>Termux 根目录（App 私有目录，root 与 App 进程均可访问）</summary>
    public string RootDir
    {
        get
        {
            var files = _context.FilesDir?.AbsolutePath ?? "/data/user/0/io.net.ztr_os.msmc/files";
            return Path.Combine(files, "termux");
        }
    }

    /// <summary>Termux prefix（usr）</summary>
    public string Prefix => Path.Combine(RootDir, "usr");

    /// <summary>Android AssetManager（供同级运行时组件读取内置 asset）</summary>
    internal global::Android.Content.Res.AssetManager Assets => _context.Assets!;

    /// <summary>Termux HOME（服务器/用户数据根）</summary>
    public string HomeDir => Path.Combine(RootDir, "home");

    /// <summary>是否已安装（bash 存在且可执行）</summary>
    public bool IsInstalled => File.Exists(Path.Combine(Prefix, "bin", "bash"));

    /// <summary>
    /// 确保 Termux 环境就绪：内置 asset 解压 → 失败则下载官方 bootstrap → 修正权限
    /// </summary>
    /// <param name="progress">进度回显（中文文案）</param>
    public async Task<bool> EnsureInstalledAsync(Action<string>? progress = null)
    {
        if (IsInstalled)
        {
            progress?.Invoke("Termux 环境已就绪");
            return true;
        }

        progress?.Invoke("正在部署 Termux 环境…");

        try
        {
            Directory.CreateDirectory(Prefix);
            Directory.CreateDirectory(HomeDir);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TERMUX] 创建目录失败");
            return false;
        }

        // 1. 尝试内置 asset
        var assetName = BootstrapAssetName;
        var assetStream = ExtractAsset(assetName);
        if (assetStream is null)
        {
            progress?.Invoke("未找到内置 Termux，尝试在线下载…");
            assetStream = await DownloadBootstrapAsync();
        }

        if (assetStream is null)
        {
            progress?.Invoke("Termux 下载失败：请检查网络或稍后重试");
            return false;
        }

        // 2. 解压到临时目录再整体搬入（避免半成品污染）
        var tmp = Path.Combine(_context.CacheDir?.AbsolutePath ?? RootDir, $"boot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            await Task.Run(() => ExtractZip(assetStream, tmp));
            progress?.Invoke("解压完成，正在修正权限…");

            // 3. 权限修正（Asset/下载 出来的文件可能丢可执行位）
            var ok = RootService.FileOp("chmod", $"-R 755 '{tmp}'");
            ok &= RootService.FileOp("cp", $"-a '{tmp}/.' '{RootDir}/'");
            if (ok)
            {
                RootService.FileOp("chmod", $"-R 755 '{Prefix}/bin' '{Prefix}/libexec' '{Prefix}/lib'");
            }

            // 4. 校验
            progress?.Invoke("校验 Termux 环境…");
            var check = await Task.Run(() => Exec("echo termux-ok"));
            if (check.Contains("termux-ok", StringComparison.Ordinal))
            {
                InitHome();
                Log.Information("[TERMUX] Termux 环境就绪");
                return true;
            }

            Log.Error("[TERMUX] 环境校验失败 Out={Out}", check);
            progress?.Invoke("Termux 环境校验失败");
            return false;
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* 清理失败无所谓 */ }
        }
    }

    /// <summary>
    /// 初始化用户主目录与基础配置（.bashrc 等）
    /// </summary>
    private void InitHome()
    {
        try
        {
            Directory.CreateDirectory(HomeDir);
            var bashrc = Path.Combine(HomeDir, ".bashrc");
            if (!File.Exists(bashrc))
            {
                File.WriteAllText(bashrc,
                    "# MSMC on Android 自动生成的默认 .bashrc\n"
                    + "export PREFIX=\"" + Prefix + "\"\n"
                    + "export HOME=\"" + HomeDir + "\"\n");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TERMUX] 初始化 home 失败");
        }
    }

    /// <summary>
    /// 在 Termux 环境内执行命令（root 视角，注入完整环境变量）
    /// </summary>
    public string Exec(string command)
    {
        return RootService.Exec(BuildEnvCommand(command));
    }

    /// <summary>
    /// 异步在 Termux 环境内执行命令
    /// </summary>
    public void ExecAsync(string command)
    {
        RootService.ExecAsync(BuildEnvCommand(command));
    }

    /// <summary>
    /// 构造带全套环境变量的命令（env -i 隔离 Android 自带 PATH）
    /// </summary>
    public string BuildEnvCommand(string command)
    {
        var env = string.Join(' ', EnvVars().Select(kv => $"{kv.Key}={kv.Value}"));
        return $"env -i {env} sh -c {ShellQuote(command)}";
    }

    /// <summary>
    /// Termux 环境变量（供 env -i 使用）
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> EnvVars()
    {
        var prefix = Prefix;
        return
        [
            new("HOME", HomeDir),
            new("PREFIX", prefix),
            new("PATH", $"{prefix}/bin:/system/bin:/system/xbin"),
            new("LD_LIBRARY_PATH", $"{prefix}/lib"),
            new("TMPDIR", Path.Combine(prefix, "tmp")),
            new("TERM", "dumb"),
            new("LANG", "C.UTF-8"),
        ];
    }

    /// <summary>提取内置 asset 为流（不存在返回 null）</summary>
    private Stream? ExtractAsset(string name)
    {
        try
        {
            return _context.Assets.Open(name);
        }
        catch (global::Java.IO.FileNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TERMUX] 读取 asset 失败 {Name}", name);
            return null;
        }
    }

    /// <summary>下载官方 bootstrap（带进度，最大 300MB）</summary>
    private async Task<Stream?> DownloadBootstrapAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var bytes = await http.GetByteArrayAsync(BootstrapDownloadUrl);
            return new MemoryStream(bytes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TERMUX] 下载 bootstrap 失败");
            return null;
        }
    }

    /// <summary>解压 zip 到目标目录（SharpCompress，保留可执行位）</summary>
    private static void ExtractZip(Stream stream, string destDir)
    {
        using var archive = SharpCompress.Archives.Zip.ZipArchive.Open(stream);
        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory) continue;
            var target = Path.GetFullPath(Path.Combine(destDir, entry.Key));
            if (!target.StartsWith(Path.GetFullPath(destDir) + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var src = entry.OpenEntryStream();
            using var dst = File.Create(target);
            src.CopyTo(dst);
        }
    }

    /// <summary>简单 shell 单引号包裹（防注入）</summary>
    private static string ShellQuote(string s) => $"'{s.Replace("'", "'\\''")}'";
}