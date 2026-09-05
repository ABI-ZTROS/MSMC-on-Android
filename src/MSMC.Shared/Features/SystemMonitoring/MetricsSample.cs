// -----------------------------------------------------------------------------
// 文件名: MetricsSample.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Models
// 功能描述: 降采样前的原始监控采样点（与 MetricsHistoryPoint 字段语义一致，
//           但作为 MetricsDownsampler 的输入契约使用独立命名空间类型）
// 依赖组件: 无
// 设计模式: 不可变记录类型（record）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Models;

/// <summary>
/// 原始监控采样点（单条 CPU/内存 瞬时采样）
/// </summary>
/// <param name="Timestamp">采样时间戳（本地 UTC+8 时间）</param>
/// <param name="CpuPercent">CPU 使用率百分比（0-100）</param>
/// <param name="MemoryPercent">内存使用率百分比（0-100）</param>
public record MetricsSample(
    DateTime Timestamp,
    double CpuPercent,
    double MemoryPercent);
