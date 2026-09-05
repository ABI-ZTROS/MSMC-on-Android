// -----------------------------------------------------------------------------
// 文件名: IMetricsPersistenceService.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Services
// 功能描述: 系统监控指标持久化服务接口，定义趋势数据的磁盘固化与历史查询契约
// 依赖组件: io.NET.ZTR_OS.Models
// 设计模式: 仓储模式（时间序列数据持久化）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

using io.NET.ZTR_OS.Features.SystemMonitoring.Models;

/// <summary>
/// 系统监控指标持久化服务接口
/// </summary>
/// <remarks>
/// 负责将 CPU/内存使用率趋势数据以自定义二进制格式（.msmcd）追加写入磁盘，
/// 支持按天加载历史数据、自动跨天切割与旧文件清理。
/// </remarks>
public interface IMetricsPersistenceService : IDisposable
{
    /// <summary>
    /// 追加一个监控数据点到当前日期的持久化文件
    /// </summary>
    /// <param name="timestamp">采集时间戳</param>
    /// <param name="cpuUsagePercent">CPU 使用率百分比（0-100）</param>
    /// <param name="memoryUsagePercent">内存使用率百分比（0-100）</param>
    /// <remarks>自动处理跨天切割：若 timestamp 日期与当前文件不同，则关闭旧文件并创建新文件。</remarks>
    void Append(DateTime timestamp, double cpuUsagePercent, double memoryUsagePercent);

    /// <summary>
    /// 加载指定日期的所有监控数据点
    /// </summary>
    /// <param name="date">目标日期（仅取日期部分，忽略时分秒）</param>
    /// <returns>按时间升序排列的数据点列表</returns>
    List<MetricsHistoryPoint> LoadDay(DateTime date);

    /// <summary>
    /// 加载最近 N 天的监控数据点
    /// </summary>
    /// <param name="days">回溯天数（含今天）</param>
    /// <returns>按时间升序排列的数据点列表</returns>
    List<MetricsHistoryPoint> LoadRecentDays(int days);

    /// <summary>
    /// 清理超过保留天数的旧数据文件
    /// </summary>
    /// <param name="retainDays">保留天数，默认 30</param>
    void CleanupOldFiles(int retainDays = 30);
}
