// -----------------------------------------------------------------------------
// 文件名: MetricsDownsampler.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Services
// 功能描述: 监控指标降采样器——把高频率原始采样点压缩到按分钟分桶的 1440 点/24h 窗口
// 依赖组件: io.NET.ZTR_OS.Features.SystemMonitoring.Models.MetricsSample
// 设计模式: 分桶聚合（时间窗口 = 1 分钟，聚合函数 = 算术平均）
// -----------------------------------------------------------------------------
using io.NET.ZTR_OS.Features.SystemMonitoring.Models;

namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

/// <summary>
/// 降采样后的分钟桶数据点
/// </summary>
/// <param name="BucketStart">分钟桶起始时间戳（已对齐到整分钟）</param>
/// <param name="CpuPercent">该分钟内 CPU 平均值</param>
/// <param name="MemoryPercent">该分钟内内存平均值</param>
/// <param name="SampleCount">参与聚合的原始采样数（用于诊断稀疏数据）</param>
public record DownsampledMetricsPoint(
    DateTime BucketStart,
    double CpuPercent,
    double MemoryPercent,
    int SampleCount);

/// <summary>
/// 监控指标降采样工具类
/// </summary>
/// <remarks>
/// 设计原理（与 README 声明的「24h × 1440 点」对齐）：
/// <list type="bullet">
/// <item>原始数据可能 2s/采样 → 24h = 43200 点 → 前端渲染压力过大</item>
/// <item>按「整分钟」分桶：每分钟取算术平均值 → 1440 点/天（不足 1440 则为实际分钟数）</item>
/// <item>空桶不填充（避免人为制造假数据导致 0 谷值干扰趋势判断）</item>
/// <item>时间戳对齐：使用原始点所属的「UTC+8 本地时间」整分钟，跨天数据也能连续</item>
/// </list>
/// </remarks>
public static class MetricsDownsampler
{
    /// <summary>
    /// 把任意频率的原始采样点降采样为按分钟分桶的聚合序列
    /// </summary>
    /// <param name="rawPoints">原始采样列表（可以无序，内部会自动按桶时间聚合）</param>
    /// <returns>按 BucketStart 升序排列的降采样点（≤1440 点/24h）</returns>
    public static List<DownsampledMetricsPoint> DownsampleToOneMinuteBuckets(
        List<MetricsSample> rawPoints)
    {
        if (rawPoints == null || rawPoints.Count == 0)
            return [];

        // key = 整分钟时间戳（已对齐到分钟 00 秒）
        // value = (cpuSum, memSum, count) —— 原地累加避免 List 分配
        var buckets = new Dictionary<DateTime, (double CpuSum, double MemSum, int Count)>();

        foreach (var p in rawPoints)
        {
            // 对齐到整分钟（丢弃秒/毫秒）
            var bucket = new DateTime(
                p.Timestamp.Year,
                p.Timestamp.Month,
                p.Timestamp.Day,
                p.Timestamp.Hour,
                p.Timestamp.Minute,
                0,
                p.Timestamp.Kind);

            // Clamp：防止脏数据（比如 CPU=999）影响均值
            var cpu = ClampPercent(p.CpuPercent);
            var mem = ClampPercent(p.MemoryPercent);

            if (buckets.TryGetValue(bucket, out var agg))
            {
                agg.CpuSum += cpu;
                agg.MemSum += mem;
                agg.Count++;
                buckets[bucket] = agg;
            }
            else
            {
                buckets[bucket] = (cpu, mem, 1);
            }
        }

        // 转成升序列表
        var result = new List<DownsampledMetricsPoint>(buckets.Count);
        foreach (var kv in buckets.OrderBy(static x => x.Key))
        {
            var (cpuSum, memSum, count) = kv.Value;
            if (count == 0) continue; // 理论不会发生，但防御一下
            result.Add(new DownsampledMetricsPoint(
                BucketStart: kv.Key,
                CpuPercent: Math.Round(cpuSum / count, 2),
                MemoryPercent: Math.Round(memSum / count, 2),
                SampleCount: count));
        }
        return result;
    }

    /// <summary>
    /// 从持久化服务读取最近 N 天并直接返回降采样结果（便捷方法）
    /// </summary>
    /// <param name="persistence">持久化服务实例</param>
    /// <param name="days">读取天数（1 = 今天）</param>
    /// <returns>降采样后的数据点列表</returns>
    public static List<DownsampledMetricsPoint> LoadRecentDaysDownsampled(
        IMetricsPersistenceService persistence,
        int days = 1)
    {
        if (persistence == null || days <= 0) return [];

        var raw = persistence.LoadRecentDays(days);
        if (raw.Count == 0) return [];

        // MetricsHistoryPoint → MetricsSample（字段语义一致，直接转换）
        var samples = raw.Select(hp => new MetricsSample(
            Timestamp: hp.Timestamp,
            CpuPercent: hp.CpuUsagePercent,
            MemoryPercent: hp.MemoryUsagePercent)).ToList();

        return DownsampleToOneMinuteBuckets(samples);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double ClampPercent(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
        if (v < 0) return 0;
        if (v > 100) return 100;
        return v;
    }
}
