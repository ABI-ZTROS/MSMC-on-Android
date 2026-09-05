// -----------------------------------------------------------------------------
// 文件名: AndroidProcessScanner.cs
// 命名空间: io.NET.ZTR_OS.Android.Monitoring
// 功能描述: Linux 进程扫描器 —— 基于 /proc 目录枚举进程、识别 Java/Minecraft
//           计算 CPU 占用与线程数，完全替代 Windows 版 NtQuerySystemInformation
// 弱机优化: 扫描结果短缓存（2~3s），避免 UI 高频枚举拖垮初代 i3
// 设计模式: 单例模式（共享实例 + 结果缓存）
// -----------------------------------------------------------------------------
using System.Globalization;
using System.Text;
using io.NET.ZTR_OS.Features.SystemMonitoring.Models;

namespace io.NET.ZTR_OS.Android.Monitoring;

/// <summary>
/// Linux 进程扫描器 —— 枚举 /proc 下的进程并提供性能信息
/// </summary>
public sealed class AndroidProcessScanner
{
    private static AndroidProcessScanner? _shared;
    private static readonly object SharedLock = new();

    /// <summary>进程 CPU 差分历史（pid → (墙钟, 总 jiffies)）</summary>
    private readonly Dictionary<int, (DateTime WallClock, long TotalJiffies)> _cpuHistory = new();

    /// <summary>进程列表缓存</summary>
    private IReadOnlyList<ProcessInfo> _cachedProcesses = [];
    private DateTime _processCacheExpire = DateTime.MinValue;

    /// <summary>CPU 统计缓存</summary>
    private IReadOnlyList<ProcessInfo> _cachedWithCpu = [];
    private DateTime _cpuCacheExpire = DateTime.MinValue;

    /// <summary>每 4096 字节一页（statm 的 RSS 单位为页）</summary>
    private const int PageSize = 4096;

    private static readonly string[] KnownJavaNames = ["java", "javaw", "jrunscript"];

    /// <summary>获取共享实例（AndroidSystemMonitor 等协作服务共用一份缓存）</summary>
    public static AndroidProcessScanner GetShared()
    {
        lock (SharedLock)
        {
            return _shared ??= new AndroidProcessScanner();
        }
    }

    /// <summary>
    /// 枚举全部进程（带 2s CPU 统计，短缓存）
    /// </summary>
    public IReadOnlyList<ProcessInfo> GetProcessesWithCpu(bool refresh = false)
    {
        var now = DateTime.UtcNow;
        if (!refresh && now < _cpuCacheExpire && _cachedWithCpu.Count > 0)
        {
            return _cachedWithCpu;
        }

        var snapshot = new List<ProcessInfo>(GetProcessSnapshot());
        ComputeCpuUsage(snapshot, now);

        var result = snapshot
            .OrderByDescending(p => p.CpuUsagePercent)
            .ToList();

        _cachedWithCpu = result;
        _processCacheExpire = now.AddSeconds(3);
        _cpuCacheExpire = now.AddSeconds(2);
        return result;
    }

    /// <summary>
    /// 获取原始进程快照（无 CPU 统计，缓存 3s）
    /// </summary>
    public IReadOnlyList<ProcessInfo> GetProcessSnapshot()
    {
        var now = DateTime.UtcNow;
        if (now < _processCacheExpire && _cachedProcesses.Count > 0)
        {
            return _cachedProcesses;
        }

        var result = new List<ProcessInfo>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories("/proc"))
            {
                var pidStr = Path.GetFileName(dir);
                if (!int.TryParse(pidStr, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                {
                    continue;
                }

                var info = ReadProcessInfo(pid);
                if (info is not null)
                {
                    result.Add(info);
                }
            }
        }
        catch (IOException)
        {
            // /proc 不可读
        }

        _cachedProcesses = result;
        _processCacheExpire = now.AddSeconds(3);
        return result;
    }

    /// <summary>计算总线程数</summary>
    public int GetTotalThreadCount()
    {
        var processes = GetProcessSnapshot();
        return processes.Sum(p => p.ThreadCount);
    }

    /// <summary>查找 Java / Minecraft 服务器进程</summary>
    public IReadOnlyList<ProcessInfo> FindJavaProcesses()
    {
        return GetProcessesWithCpu().Where(p => p.IsJava || p.IsMinecraft).ToList();
    }

    /// <summary>读取单个进程的信息（/proc/[pid] 的 stat/status/statm/cmdline）</summary>
    private ProcessInfo? ReadProcessInfo(int pid)
    {
        var basePath = $"/proc/{pid}";
        try
        {
            var comm = ReadFirstLine($"{basePath}/comm")?.Trim('(', ')') ?? $"pid{pid}";
            var statText = File.ReadAllText($"{basePath}/stat");
            var fields = ParseStatFields(statText);

            long utime = 0, stime = 0;
            if (fields.Length > 22)
            {
                long.TryParse(TrimWrapping(fields[13]), NumberStyles.None, CultureInfo.InvariantCulture, out utime);
            }

            if (fields.Length > 24)
            {
                long.TryParse(TrimWrapping(fields[14]), NumberStyles.None, CultureInfo.InvariantCulture, out stime);
            }

            var threadCount = 0;
            if (fields.Length > 19 && int.TryParse(TrimWrapping(fields[19]), NumberStyles.None, CultureInfo.InvariantCulture, out var threads))
            {
                threadCount = threads;
            }

            var commandLine = ReadCommandLine($"{basePath}/cmdline");
            var isJava = IsJavaCommand(commandLine) || comm is "java" or "javaw";
            var isMinecraft = isJava && IsMinecraftCommand(commandLine);
            var displayName = isMinecraft ? BuildDisplayName(commandLine) : comm;

            // status 补充：VmRSS / Threads / Cpus_allowed / State
            long rssBytes = 0;
            ulong affinityMask = 0;
            var isSystem = pid <= 2;
            foreach (var line in File.ReadLines($"{basePath}/status"))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                switch (key)
                {
                    case "VmRSS":
                        rssBytes = ParseKb(value) ?? 0;
                        break;
                    case "Threads":
                        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var t) && t > 0)
                        {
                            threadCount = t;
                        }
                        break;
                    case "Cpus_allowed":
                        affinityMask = ParseHexMask(value);
                        break;
                }
            }

            // 判断系统进程：内核线程（comm 带 [ ]）或短生命周期
            if (comm.StartsWith('[') && comm.EndsWith(']'))
            {
                isSystem = true;
            }

            return new ProcessInfo
            {
                Pid = pid,
                Comm = comm,
                ThreadCount = threadCount,
                WorkingSetBytes = rssBytes,
                CommandLine = commandLine,
                AffinityMask = (long)affinityMask,
                AllowedCoreIndices = MaskToIndices(affinityMask),
                IsJava = isJava,
                IsMinecraft = isMinecraft,
                DisplayName = displayName,
                IsSystem = isSystem,
                TotalJiffies = utime + stime,
                Utime = utime,
                Stime = stime,
                PriorityClass = ReadNiceName(pid),
            };
        }
        catch (IOException)
        {
            return null; // 进程已退出
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>计算每个进程的 CPU 使用率（差分 /proc/[pid]/stat 的 utime+stime）</summary>
    private void ComputeCpuUsage(List<ProcessInfo> processes, DateTime now)
    {
        foreach (var p in processes)
        {
            if (p.TotalJiffies <= 0)
            {
                p.CpuUsagePercent = 0;
                continue;
            }

            if (_cpuHistory.TryGetValue(p.Pid, out var prev))
            {
                var wallDeltaSec = (now - prev.WallClock).TotalSeconds;
                var jiffyDelta = p.TotalJiffies - prev.TotalJiffies;
                if (wallDeltaSec > 0.5 && jiffyDelta >= 0)
                {
                    // 时钟频率默认 100 Hz，除以可用核数归一化到 0-100
                    p.CpuUsagePercent = Math.Clamp(
                        jiffyDelta / (double)wallDeltaSec / 100.0 / Math.Max(1, Environment.ProcessorCount) * 100.0,
                        0, 100);
                }
                else
                {
                    p.CpuUsagePercent = 0;
                }
            }
            else
            {
                p.CpuUsagePercent = 0;
            }

            _cpuHistory[p.Pid] = (now, p.TotalJiffies);
        }

        // 清理已退出进程的历史
        var live = new HashSet<int>(processes.Select(p => p.Pid));
        foreach (var pid in _cpuHistory.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _cpuHistory.Remove(pid);
        }
    }

    // ─────────────────────────── 辅助解析 ───────────────────────────

    private static string ReadFirstLine(string path)
    {
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            return reader.ReadLine() ?? string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private static string ReadCommandLine(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var sb = new StringBuilder(bytes.Length);
            foreach (var b in bytes)
            {
                if (b == 0)
                {
                    if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                }
                else
                {
                    sb.Append((char)b);
                }
            }
            return sb.ToString().Trim();
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>切分 /proc/[pid]/stat 字段（第 2 字段 comm 可能含空格，需特殊处理）</summary>
    private static string[] ParseStatFields(string stat)
    {
        var closeIdx = stat.LastIndexOf(')');
        if (closeIdx <= 0) return stat.Split(' ');
        var rest = stat[(closeIdx + 2)..]; // 跳过 ") "
        var fields = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new string[fields.Length + 1];
        result[0] = stat[..stat.IndexOf('(')].Trim();
        result[1] = stat[(stat.IndexOf('(') + 1)..closeIdx];
        Array.Copy(fields, 0, result, 2, fields.Length);
        return result;
    }

    private static string TrimWrapping(string v) => v;

    private static long? ParseKb(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0
            && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var v)
            ? v * 1024
            : null;
    }

    /// <summary>解析 Cpus_allowed 十六进制掩码（如 "ff"、"55"）</summary>
    private static ulong ParseHexMask(string value)
    {
        var hex = value.Replace("0x", string.Empty).Trim();
        if (hex.Length == 0) return 0;
        return ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var mask)
            ? mask
            : 0;
    }

    private static int[] MaskToIndices(ulong mask)
    {
        var list = new List<int>();
        for (var i = 0; i < 64; i++)
        {
            if (((mask >> i) & 1) == 1) list.Add(i);
        }
        return list.ToArray();
    }

    private static bool IsJavaCommand(string cmdline)
    {
        return cmdline.Contains("-javaagent", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("java", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("javaw", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMinecraftCommand(string cmdline)
    {
        return cmdline.Contains("server.jar", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("paper", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("spigot", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("forge", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("fabric", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("velocity", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("purpur", StringComparison.OrdinalIgnoreCase)
            || cmdline.Contains("bukkit", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDisplayName(string cmdline)
    {
        // 提取 -jar 后的 jar 文件名
        var idx = cmdline.IndexOf("-jar", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var name = cmdline[(idx + 4)..].Trim().Split(' ')[0];
            return Path.GetFileName(name);
        }
        return "Minecraft Server";
    }

    private static string ReadNiceName(int pid)
    {
        var priority = ReadFirstLine($"/proc/{pid}/stat");
        var fields = priority.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length > 17 && int.TryParse(TrimWrapping(fields[17]), NumberStyles.None, CultureInfo.InvariantCulture, out var nice))
        {
            return nice switch
            {
                <= -10 => "High",
                >= 10 => "Low",
                _ => "Normal",
            };
        }
        return "Normal";
    }
}

/// <summary>
/// 进程信息内部 DTO（含 CPU 差分中间字段）
/// </summary>
public sealed class ProcessInfo
{
    public int Pid { get; set; }
    public string Comm { get; set; } = string.Empty;
    public string CommandLine { get; set; } = string.Empty;
    public bool IsJava { get; set; }
    public bool IsMinecraft { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int ThreadCount { get; set; }
    public long WorkingSetBytes { get; set; }
    public long AffinityMask { get; set; }
    public int[] AllowedCoreIndices { get; set; } = [];
    public string PriorityClass { get; set; } = "Normal";
    public long TotalJiffies { get; set; }
    public long Utime { get; set; }
    public long Stime { get; set; }

    /// <summary>CPU 使用率（0-100）</summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>转换为前端契约的 ProcessAffinityInfo</summary>
    public ProcessAffinityInfo ToAffinityInfo() => new()
    {
        ProcessId = Pid,
        ProcessName = Comm,
        IsMinecraftServer = IsMinecraft,
        IsJavaProcess = IsJava,
        IsSystemProcess = IsSystem,
        DisplayName = DisplayName,
        AffinityMask = AffinityMask,
        AllowedCoreIndices = AllowedCoreIndices,
        CpuUsagePercent = CpuUsagePercent,
        WorkingSetBytes = WorkingSetBytes,
        ThreadCount = ThreadCount,
        PriorityClass = PriorityClass,
        CommandLine = CommandLine.Length > 200 ? CommandLine[..200] : CommandLine,
    };
}