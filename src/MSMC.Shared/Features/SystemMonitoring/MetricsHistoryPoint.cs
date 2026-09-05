// -----------------------------------------------------------------------------
// 文件名: MetricsHistoryPoint.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Models
// 功能描述: 监控历史数据点记录类型，承载 CPU/内存使用率的时间序列采样
// 依赖组件: 无
// 设计模式: 不可变记录类型（record）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Models;

/// <summary>
/// 监控历史数据点 —— CPU/内存使用率时间序列的单个采样记录
/// </summary>
/// <param name="Timestamp">采集时间戳</param>
/// <param name="CpuUsagePercent">CPU 使用率百分比（0-100）</param>
/// <param name="MemoryUsagePercent">内存使用率百分比（0-100）</param>
public record MetricsHistoryPoint(DateTime Timestamp, double CpuUsagePercent, double MemoryUsagePercent);
