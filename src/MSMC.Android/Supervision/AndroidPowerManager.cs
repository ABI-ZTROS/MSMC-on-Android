// -----------------------------------------------------------------------------
// 文件名: AndroidPowerManager.cs
// 命名空间: io.NET.ZTR_OS.Android.Supervision
// 功能描述: 性能调优 —— root 下 taskset 锁核、renice 优先级、OOM 保护（oom_score_adj）
// -----------------------------------------------------------------------------
using io.NET.ZTR_OS.Android.Root;

namespace io.NET.ZTR_OS.Android.Supervision;

/// <summary>
/// Android 性能调优（root）—— 亲和性 / 优先级 / OOM 保护
/// </summary>
public sealed class AndroidPowerManager
{
    /// <summary>设置进程 CPU 亲和掩码（taskset）</summary>
    public (bool Success, string Error) SetAffinity(int pid, long mask)
    {
        if (pid <= 0) return (false, "无效 PID");
        try
        {
            var (_, _, code) = RootService.ExecWithCode($"taskset -p {mask} {pid} 2>&1");
            return (code == 0, code == 0 ? string.Empty : $"taskset 失败（exit {code}）");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>设置进程 nice 值（renice；范围 -20..19，root 可调负值）</summary>
    public (bool Success, string Error) SetPriority(int pid, int nice)
    {
        if (pid <= 0) return (false, "无效 PID");
        var clamped = Math.Clamp(nice, -20, 19);
        try
        {
            var (_, _, code) = RootService.ExecWithCode($"renice -n {clamped} -p {pid} 2>&1");
            return (code == 0, code == 0 ? string.Empty : $"renice 失败（exit {code}）");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>设置 OOM 保护（oom_score_adj，-1000=完全保护，0=默认）</summary>
    public (bool Success, string Error) SetOomProtection(int pid, int score = -1000)
    {
        if (pid <= 0) return (false, "无效 PID");
        try
        {
            var (_, _, code) = RootService.ExecWithCode($"echo {score} > /proc/{pid}/oom_score_adj 2>&1");
            return (code == 0, code == 0 ? string.Empty : "oom 保护设置失败");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>读取 CPU 拓扑信息（/proc/cpuinfo 精简版）</summary>
    public object GetCpuInfo()
    {
        var model = string.Empty;
        var cores = 0;
        try
        {
            var seen = new HashSet<string>();
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                if (key == "model name" && seen.Add(value)) model = value;
                if (key == "processor") cores++;
            }
        }
        catch (Exception)
        {
            // 读取失败返回空信息
        }

        return new
        {
            modelName = model,
            manufacturer = string.Empty,
            physicalCores = Math.Max(1, cores),
            logicalCores = Environment.ProcessorCount,
            socketCount = 1,
            numaNodeCount = 1,
            isHyperThreadingEnabled = false,
            logicalToPhysicalCoreMap = Enumerable.Range(0, Environment.ProcessorCount).ToArray(),
            isRecognized = cores > 0,
        };
    }
}