// -----------------------------------------------------------------------------
// 文件名: JavaRuntimeManager.cs
// 命名空间: io.NET.ZTR_OS.Android.Runtime
// 功能描述: Java 运行时管理 —— 内置 4×JDK（17/21/25/26，internal flavor asset）
//           或 Termux apt 安装（external 兜底）；按 MC 版本自动映射 + 手动覆盖。
// 设计模式: 策略模式（内置源 / apt 源 / 检测源）+ 结果缓存
// -----------------------------------------------------------------------------
using Serilog;

namespace io.NET.ZTR_OS.Android.Runtime;

/// <summary>
/// Java 运行时管理器
/// </summary>
public sealed class JavaRuntimeManager
{
    /// <summary>内置 JDK 主版本列表（覆盖全 MC 时代）</summary>
    public static readonly int[] BundledMajors = [17, 21, 25, 26];

    /// <summary>Termux JDK 安装目录（$PREFIX/lib/jvm/openjdk-N）</summary>
    public const string JvmRelDir = "usr/lib/jvm";

    private readonly TermuxRuntime _termux;

    public JavaRuntimeManager(TermuxRuntime termux)
    {
        _termux = termux;
    }

    /// <summary>获取 Termux 中的 Java 二进制路径（$PREFIX/bin/java 或 lib/jvm 下）</summary>
    public string DefaultJavaPath => Path.Combine(_termux.Prefix, "bin", "java");

    /// <summary>某主版本的安装目录（兼容 Termux 的 java-N-openjdk 与规范的 openjdk-N）</summary>
    public string JdkDir(int major)
    {
        var candidates = new[]
        {
            Path.Combine(_termux.RootDir, JvmRelDir, $"openjdk-{major}"),
            Path.Combine(_termux.RootDir, JvmRelDir, $"java-{major}-openjdk"),
        };
        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return c;
        }
        return candidates[0];
    }

    /// <summary>某主版本的 java 可执行路径</summary>
    public string JavaPath(int major) => Path.Combine(JdkDir(major), "bin", "java");

    /// <summary>
    /// 已有运行时列表（仅报告存在的目录，不触发安装）
    /// </summary>
    public List<JavaRuntimeInfo> ScanInstalled()
    {
        var list = new List<JavaRuntimeInfo>();
        foreach (var major in BundledMajors)
        {
            var path = JavaPath(major);
            if (File.Exists(path))
            {
                list.Add(new JavaRuntimeInfo { Major = major, JavaPath = path, Source = "installed" });
            }
        }

        // Termux 全局 java（apt 安装后的标准位）
        if (File.Exists(DefaultJavaPath) && list.All(r => r.JavaPath != DefaultJavaPath))
        {
            list.Add(new JavaRuntimeInfo { Major = QueryMajor(DefaultJavaPath), JavaPath = DefaultJavaPath, Source = "termux" });
        }

        return list;
    }

    /// <summary>
    /// 确保指定主版本的 JDK 可用：内置 asset 解压 → Termux apt 安装 → root 解包兜底
    /// </summary>
    /// <param name="major">JDK 主版本（17/21/25/26）</param>
    /// <param name="progress">进度回显</param>
    /// <returns>java 可执行路径；失败返回 null</returns>
    public async Task<string?> EnsureAsync(int major, Action<string>? progress = null)
    {
        if (!BundledMajors.Contains(major))
        {
            Log.Warning("[JDK] 不支持的 JDK 版本 {Major}", major);
            return null;
        }

        var javaPath = JavaPath(major);
        if (File.Exists(javaPath))
        {
            return javaPath;
        }

        progress?.Invoke($"正在准备 JDK {major}…");

        // 1. 内置 asset（internal flavor 出厂即捆绑）
        var assetName = $"jdk{major}.tar.gz";
        if (await ExtractJdkAssetAsync(assetName, major))
        {
            if (File.Exists(javaPath))
            {
                progress?.Invoke($"JDK {major} 已就绪（内置）");
                return javaPath;
            }
        }

        // 2. Termux apt 安装（external 兜底 / internal 出厂异常兜底）
        progress?.Invoke($"通过 Termux 安装 JDK {major}…");
        var installed = await Task.Run(() =>
            _termux.Exec($"pkg install -y openjdk-{major} 2>&1 | tail -3; readlink -f $(which java)"));

        var detected = await Task.Run(() => ResolveJavaFromApt(major));
        if (File.Exists(detected))
        {
            EnsureSymlink(detected, javaPath);
            progress?.Invoke($"JDK {major} 已就绪（apt）");
            return javaPath;
        }

        progress?.Invoke($"JDK {major} 安装失败（apt 输出尾部：{installed.Trim()[(^Math.Min(120, installed.Trim().Length))..]}）");
        Log.Error("[JDK] 安装失败 Major={Major} Out={Out}", major, installed);
        return null;
    }

    /// <summary>从 apt 安装结果解析 java 路径（readlink 解析 which java 的符号链）</summary>
    private string ResolveJavaFromApt(int major)
    {
        try
        {
            var outStr = _termux.Exec("readlink -f $(which java) 2>/dev/null || ls " + Path.Combine(_termux.Prefix, "bin", "java"));
            foreach (var line in outStr.Split('\n'))
            {
                var trimmed = line.Trim();
                // 兼容 Termux 目录名 java-N-openjdk 与规范 openjdk-N
                if ((trimmed.Contains($"openjdk-{major}", StringComparison.Ordinal) ||
                     trimmed.Contains($"java-{major}-openjdk", StringComparison.Ordinal)) && File.Exists(trimmed))
                {
                    return trimmed;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[JDK] 解析 apt java 失败");
        }
        return JavaPath(major);
    }

    /// <summary>若 $PREFIX/bin/java 已指向目标版本，补一个到标准目录的符号链接</summary>
    private void EnsureSymlink(string actual, string expected)
    {
        try
        {
            if (actual == expected) return;
            Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
            if (!File.Exists(expected))
            {
                _ = _termux.Exec($"ln -sf '{actual}' '{expected}'");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[JDK] 创建符号链接失败");
        }
    }

    /// <summary>从内置 asset 解压 JDK（tar.gz，Termux 布局：usr/lib/jvm/openjdk-N/…）</summary>
    private async Task<bool> ExtractJdkAssetAsync(string assetName, int major)
    {
        Stream? stream;
        try
        {
            stream = await Task.Run(() =>
            {
                try { return (Stream?)new MemoryStream(ReadAssetBytes(assetName)); }
                catch (global::Java.IO.FileNotFoundException) { return null; }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[JDK] 读取 asset 失败 {Name}", assetName);
            return false;
        }

        if (stream is null) return false;

        try
        {
            var tmp = Path.Combine(_termux.HomeDir is { } h ? h : throw new InvalidOperationException(), $"jdk-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmp);
            await Task.Run(() => ExtractTarGz(stream, tmp));

            // 解出来的内容可能是 usr/… （Termux 布局）也可能是直接 lib/jvm/…（直打包）
            var src = Directory.Exists(Path.Combine(tmp, "usr"))
                ? Path.Combine(tmp, "usr", "lib", "jvm")
                : Directory.Exists(Path.Combine(tmp, "lib", "jvm"))
                    ? Path.Combine(tmp, "lib", "jvm")
                    : null;
            if (src is null)
            {
                Log.Error("[JDK] asset 布局异常 {Name}", assetName);
                return false;
            }

            foreach (var dir in Directory.EnumerateDirectories(src))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals($"openjdk-{major}", StringComparison.Ordinal))
                {
                    var dst = JdkDir(major);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    _ = _termux.Exec($"rm -rf '{dst}' && cp -a '{dir}' '{dst}' && chmod -R 755 '{dst}/bin'");
                    return File.Exists(JavaPath(major));
                }
            }

            Log.Warning("[JDK] asset 内未找到 openjdk-{Major} {Name}", major, assetName);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[JDK] 解压内置 JDK 失败 {Name}", assetName);
            return false;
        }
        finally
        {
            stream.Dispose();
        }
    }

    private byte[] ReadAssetBytes(string name)
    {
        using var s = _termux.Assets.Open(name);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>解压 tar.gz 到目标目录</summary>
    private static void ExtractTarGz(Stream stream, string destDir)
    {
        using var gz = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
        using var reader = SharpCompress.Readers.ReaderFactory.Open(gz);
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory) continue;
            var target = Path.GetFullPath(Path.Combine(destDir, reader.Entry.Key));
            if (!target.StartsWith(Path.GetFullPath(destDir) + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var src = reader.OpenEntryStream();
            using var dst = File.Create(target);
            src.CopyTo(dst);
        }
    }

    /// <summary>
    /// 按 MC 版本推荐 JDK 主版本：1.17.0–1.20.4 → 17；≥1.20.5 → 21；特殊场景人工覆盖 25/26
    /// </summary>
    public static int MapMinecraftVersion(string mcVersion)
    {
        if (TryParsePart(mcVersion, out var minor))
        {
            if (minor < 17) return 17;  // 太老也摸高 17（低版本也能跑）
            if (minor >= 20 && VersionTailAtLeast(mcVersion, "1.20.5")) return 21;
            if (minor >= 17) return 17;
        }
        return 21; // 未知版本默认 21
    }

    private static bool TryParsePart(string v, out int minor)
    {
        minor = 0;
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 && int.TryParse(parts[1], out minor))
        {
            return true;
        }
        return int.TryParse(v.Split(' ')[0], out minor) && minor != 0;
    }

    private static bool VersionTailAtLeast(string v, string probe)
    {
        var vParts = v.Split('.');
        var pParts = probe.Split('.');
        for (var i = 0; i < Math.Min(vParts.Length, pParts.Length); i++)
        {
            if (int.TryParse(vParts[i], out var a) && int.TryParse(pParts[i], out var b))
            {
                if (a != b) return a > b;
            }
        }
        return vParts.Length >= pParts.Length;
    }

    /// <summary>读取某 java 的版本号</summary>
    public string QueryVersion(string javaPath)
    {
        try
        {
            var outStr = _termux.Exec($"'{javaPath}' -version 2>&1; echo done");
            foreach (var line in outStr.Split('\n'))
            {
                var idx = line.IndexOf("\"", StringComparison.Ordinal);
                if (idx < 0) continue;
                var end = line.IndexOf("\"", idx + 1, StringComparison.Ordinal);
                if (end > idx)
                {
                    return line[(idx + 1)..end];
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[JDK] 查询版本失败 Path={Path}", javaPath);
        }
        return "unknown";
    }

    /// <summary>从 java 路径估主版本（目录名 openjdk-N 或版本串）</summary>
    public int QueryMajor(string javaPath)
    {
        var m = System.Text.RegularExpressions.Regex.Match(javaPath, @"openjdk-(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var v)) return v;
        var ver = QueryVersion(javaPath);
        var v2 = System.Text.RegularExpressions.Regex.Match(ver, @"^(\d+)");
        return v2.Success && int.TryParse(v2.Groups[1].Value, out var n) && n is >= 17 and <= 26 ? n : 21;
    }
}

/// <summary>Java 运行时信息（供面板展示）</summary>
public sealed class JavaRuntimeInfo
{
    public int Major { get; set; }
    public string JavaPath { get; set; } = string.Empty;
    public string Source { get; set; } = "installed"; // installed | termux
    public string Version { get; set; } = "unknown";
}