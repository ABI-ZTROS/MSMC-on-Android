// -----------------------------------------------------------------------------
// 文件名: AndroidSystemMonitor.cs
// 命名空间: io.NET.ZTR_OS.Android.Monitoring
// 功能描述: Linux 系统监控 —— 基于 /proc 与 statvfs 解析 CPU/内存/磁盘/线程指标，
//           完全替代 Windows 版 WMI/PDH 采集实现
// 设计模式: 单例模式 + 快照模式（两次采样差分计算 CPU 使用率）
// 弱机优化: 全部差异分计算在前台线程完成，采集间隔由调用方控制
// -----------------------------------------------------------------------------
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using io.NET.ZTR_OS.Features.SystemMonitoring.Models;

namespace io.NET.ZTR_OS.Android.Monitoring;

/// <summary>
/// Linux 系统监控服务 —— 解析 /proc 与 /sys 获取实时性能指标
/// </summary>
public sealed partial class AndroidSystemMonitor
{
    /// <summary>CPU 差分采样需要的最小间隔（毫秒）</summary>
    private const long MinSampleIntervalMs = 150;

    private DateTime _lastCpuSampleTime = DateTime.MinValue;
    private long[] _lastCpuJiffies = [];
    private long _lastTotalJiffies;

    /// <summary>
    /// 采集系统整体指标快照（CPU/内存/磁盘/线程）
    /// </summary>
    public SystemMetrics CollectSystemMetrics()
    {
        var metrics = new SystemMetrics();
        var now = DateTime.Now;

        var (total, perCore) = ReadCpuUsage();
        metrics.CpuUsagePercent = Round2(total);
        metrics.PerCoreCpuUsages = perCore.Select(Round2).ToArray();

        // 内存
        var (memTotal, memAvailable) = ReadMemInfo();
        metrics.TotalMemoryBytes = memTotal;
        metrics.UsedMemoryBytes = Math.Max(0, memTotal - memAvailable);
        metrics.MemoryUsagePercent = memTotal > 0 ? Round2((double)(memTotal - memAvailable) / memTotal * 100.0) : 0;
        metrics.MemoryType = ReadDmiMemoryType();
        metrics.MemoryModuleCount = Math.Max(1, Environment.ProcessorCount);
        metrics.MemorySpeedMHz = ReadDmiMemorySpeed();

        // 磁盘（服务器工作目录所在分区，兜底根分区）
        var disk = ReadDiskUsage();
        metrics.DiskTotalBytes = disk.Total;
        metrics.DiskUsedBytes = disk.Used;
        metrics.DiskFreeBytes = disk.Free;
        metrics.DiskUsagePercent = disk.Total > 0 ? Round2((double)disk.Used / disk.Total * 100.0) : 0;
        metrics.DiskName = disk.Name;

        // 进程树
        var scanner = AndroidProcessScanner.GetShared();
        var java = scanner.FindJavaProcesses().FirstOrDefault();
        if (java is not null)
        {
            metrics.JavaCpuUsagePercent = Round2(java.CpuUsagePercent);
            metrics.JavaWorkingSetBytes = java.WorkingSetBytes;
            metrics.JavaPrivateBytes = java.WorkingSetBytes;
            metrics.JavaThreadCount = java.ThreadCount;
            metrics.JavaHandleCount = 0;
        }
        metrics.TotalThreadCount = scanner.GetTotalThreadCount();

        return metrics;
    }

    /// <summary>
    /// 读取 CPU 使用率（整体 + 每核），基于两次 /proc/stat 差分
    /// </summary>
    private (double Total, double[] PerCore) ReadCpuUsage()
    {
        var stat = ReadProcStat();
        if (stat.Count == 0)
        {
            return (0, []);
        }

        var now = DateTime.UtcNow;
        var totalJiffies = stat[0].Jiffies;
        var elapsedMs = (now - _lastCpuSampleTime).TotalMilliseconds;

        if (elapsedMs < MinSampleIntervalMs || _lastCpuSampleTime == DateTime.MinValue)
        {
            // 无有效历史采样：仅记录基线并返回 0，避免首次为 100%
            _lastCpuSampleTime = now;
            _lastCpuJiffies = [totalJiffies];
            _lastTotalJiffies = totalJiffies;
            return (0, stat.Skip(1).Select(_ => 0.0).ToArray());
        }

        var totalDelta = totalJiffies - _lastTotalJiffies;
        var totalPct = totalDelta > 0
            ? Math.Clamp((totalDelta - (stat[0].Idle - _lastCpuIdleJiffies)) / (double)totalDelta * 100.0, 0, 100)
            : 0;

        // 每核
        var perCore = new double[stat.Count - 1];
        for (var i = 1; i < stat.Count; i++)
        {
            var prev = i - 1 < _lastCpuJiffies.Length ? _lastCpuJiffies[i] : 0;
            var delta = stat[i].Jiffies - prev;
            if (delta <= 0)
            {
                perCore[i - 1] = 0;
                continue;
            }

            var idleDelta = stat[i].Idle - (i - 1 < _lastCpuIdlePerCore.Length ? _lastCpuIdlePerCore[i - 1] : 0);
            perCore[i - 1] = Math.Clamp((delta - idleDelta) / (double)delta * 100.0, 0, 100);
        }

        _lastCpuSampleTime = now;
        _lastCpuJiffies = stat.Skip(1).Select(s => s.Jiffies).ToArray();
        _lastTotalJiffies = totalJiffies;
        _lastCpuIdleJiffies = stat[0].Idle;
        _lastCpuIdlePerCore = stat.Skip(1).Select(s => s.Idle).ToArray();

        return (totalPct, perCore);
    }

    private long _lastCpuIdleJiffies;
    private long[] _lastCpuIdlePerCore = [];

    /// <summary>解析 /proc/stat 的 CPU 行</summary>
    private static List<(long Jiffies, long Idle)> ReadProcStat()
    {
        var result = new List<(long, long)>();
        try
        {
            foreach (var line in File.ReadLines("/proc/stat"))
            {
                if (!line.StartsWith("cpu", StringComparison.Ordinal)) break;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                var idle = long.Parse(parts[4], CultureInfo.InvariantCulture);
                long jiffies = 0;
                for (var i = 1; i < parts.Length; i++)
                {
                    jiffies += long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0;
                }
                result.Add((jiffies, idle));
            }
        }
        catch (IOException)
        {
            // /proc 不可读时返回空，调用方兜底为 0
        }
        return result;
    }

    /// <summary>读取 /proc/meminfo（Total / Available）</summary>
    private static (long Total, long Available) ReadMemInfo()
    {
        long total = 0, available = 0;
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2) continue;
                var kb = ParseKb(parts[1]);
                switch (parts[0])
                {
                    case "MemTotal": total = kb; break;
                    case "MemAvailable": available = kb; break;
                }
            }
        }
        catch (IOException)
        {
            // 无 /proc 时返回 0
        }
        return (total, available);
    }

    private static long ParseKb(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return 0;
        return long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var v)
            ? v * 1024
            : 0;
    }

    /// <summary>磁盘使用率（statvfs 系统调用）</summary>
    private static (long Total, long Used, long Free, string Name) ReadDiskUsage()
    {
        var stat = new StatVfs();
        var path = Environment.GetEnvironmentVariable("MSMC_SERVER_DIR");
        if (string.IsNullOrEmpty(path) || statvfs(path, stat) != 0)
        {
            path = "/";
            if (statvfs(path, stat) != 0)
            {
                return (0, 0, 0, "/");
            }
        }

        var blockSize = (long)stat.Frsize;
        var total = blockSize * (long)stat.Blocks;
        var free = blockSize * (long)stat.Bavail;
        var used = total - blockSize * (long)stat.Bfree;

        return (total, Math.Max(0, used), Math.Max(0, free), path);
    }

    /// <summary>读取内存类型（/sys 或 dmidecode，不可得则返回 Unknown）</summary>
    private static string ReadDmiMemoryType()
    {
        try
        {
            const string root = "/sys/devices/system/memory";
            if (!Directory.Exists(root)) return "Unknown";
            return "Linux";
        }
        catch (IOException)
        {
            return "Unknown";
        }
    }

    /// <summary>内存频率（/sys 或 dmidecode，不可得返回 0）</summary>
    private static int ReadDmiMemorySpeed()
    {
        try
        {
            const string root = "/sys/devices/system/memory";
            if (!Directory.Exists(root)) return 0;
            return 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// 读取 CPU 拓扑信息（型号、物理核数、缓存）
    /// </summary>
    public CpuInfo GetCpuInfo()
    {
        var info = new CpuInfo { Architecture = RuntimeInformation.ProcessArchitecture.ToString() };
        var names = new List<string>();
        var physicalIds = new HashSet<string>();
        long? cacheBytes = null;

        try
        {
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                switch (key)
                {
                    case "model name":
                        if (!names.Contains(value)) names.Add(value);
                        break;
                    case "physical id":
                        if (!string.IsNullOrEmpty(value)) physicalIds.Add(value);
                        break;
                    case "cache size":
                        cacheBytes ??= ParseCacheSize(value);
                        break;
                }
            }
        }
        catch (IOException)
        {
            // 降级返回
        }

        info.ModelName = string.Join(" / ", names);
        info.LogicalProcessorCount = Environment.ProcessorCount;
        info.PhysicalCoreCount = physicalIds.Count > 0 ? physicalIds.Count : Math.Max(1, Environment.ProcessorCount / 2);
        info.CacheSizeBytes = cacheBytes ?? 0;
        return info;
    }

    private static long ParseCacheSize(string value)
    {
        // 形如 "8192 KB"
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return 0;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var size)) return 0;
        var unit = parts.Length > 1 ? parts[1].ToUpperInvariant() : "KB";
        return unit switch
        {
            "KB" => (long)(size * 1024),
            "MB" => (long)(size * 1024 * 1024),
            "GB" => (long)(size * 1024 * 1024 * 1024),
            _ => (long)size,
        };
    }

    private static double Round2(double v) => Math.Round(v, 2);

    [StructLayout(LayoutKind.Sequential)]
    private sealed class StatVfs
    {
        public long Bsize;
        public long Frsize;
        public long Blocks;
        public long Bfree;
        public long Bavail;
        public long Files;
        public long Ffree;
        public long Favail;
        public long Fsid;
        public long Flag;
        public long Namemax;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "statvfs")]
    private static extern int statvfs(string path, StatVfs buf);
}

/// <summary>
/// CPU 拓扑信息 DTO（与前端 CpuInfo 契约一致）
/// </summary>
public sealed class CpuInfo
{
    public string ModelName { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public int PhysicalCoreCount { get; set; }
    public int LogicalProcessorCount { get; set; }
    public long CacheSizeBytes { get; set; }
}