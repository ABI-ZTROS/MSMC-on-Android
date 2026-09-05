// -----------------------------------------------------------------------------
// 文件名: SystemMetrics.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Models
// 功能描述: 系统性能指标快照数据契约，承载系统与 Java 进程的实时监控数据
// 依赖组件: CommunityToolkit.Mvvm
// 设计模式: 贫血模型 + ObservableObject 属性变更通知
// -----------------------------------------------------------------------------
using CommunityToolkit.Mvvm.ComponentModel;

namespace io.NET.ZTR_OS.Features.SystemMonitoring.Models;

/// <summary>
/// 系统性能指标快照数据契约，封装操作系统级与 Java 进程级的实时监控数据。
/// 以时间点为单位采集，作为性能监控模块与 UI 层之间的 DTO。
/// </summary>
public partial class SystemMetrics : ObservableObject
{
    /// <summary>
    /// 系统整体 CPU 使用率百分比。
    /// 取值范围 0.0 至 100.0。
    /// </summary>
    [ObservableProperty] private double _cpuUsagePercent;

    /// <summary>
    /// 每个逻辑 CPU 核心的使用率百分比数组。
    /// 数组索引对应核心编号（0 开始）。
    /// </summary>
    [ObservableProperty] private double[] _perCoreCpuUsages = [];

    /// <summary>
    /// 系统总物理内存容量，单位为字节。
    /// </summary>
    [ObservableProperty] private long _totalMemoryBytes;

    /// <summary>
    /// 系统已使用物理内存大小，单位为字节。
    /// </summary>
    [ObservableProperty] private long _usedMemoryBytes;

    /// <summary>
    /// 系统内存使用率百分比。
    /// 取值范围 0.0 至 100.0。
    /// </summary>
    [ObservableProperty] private double _memoryUsagePercent;

    /// <summary>
    /// 系统总线程数。
    /// 统计所有进程的线程总和。
    /// </summary>
    [ObservableProperty] private int _totalThreadCount;

    /// <summary>
    /// 内存运行频率，单位为兆赫兹（MHz）。
    /// </summary>
    [ObservableProperty] private int _memorySpeedMHz;

    /// <summary>
    /// 内存类型标识。
    /// 常见值包括 DDR4、DDR5 等。
    /// </summary>
    [ObservableProperty] private string _memoryType = string.Empty;

    /// <summary>
    /// 已安装的内存模块数量。
    /// </summary>
    [ObservableProperty] private int _memoryModuleCount;

    /// <summary>
    /// 磁盘总容量，单位为字节。
    /// 统计服务器工作目录所在的磁盘分区。
    /// </summary>
    [ObservableProperty] private long _diskTotalBytes;

    /// <summary>
    /// 磁盘已使用空间大小，单位为字节。
    /// </summary>
    [ObservableProperty] private long _diskUsedBytes;

    /// <summary>
    /// 磁盘可用空间大小，单位为字节。
    /// </summary>
    [ObservableProperty] private long _diskFreeBytes;

    /// <summary>
    /// 磁盘使用率百分比。
    /// 取值范围 0.0 至 100.0。
    /// </summary>
    [ObservableProperty] private double _diskUsagePercent;

    /// <summary>
    /// 磁盘名称或卷标。
    /// 标识当前监控的磁盘分区。
    /// </summary>
    [ObservableProperty] private string _diskName = string.Empty;

    /// <summary>
    /// Java 进程 CPU 使用率百分比。
    /// 取值范围 0.0 至 100.0。
    /// </summary>
    [ObservableProperty] private double _javaCpuUsagePercent;

    /// <summary>
    /// Java 进程工作集内存大小，单位为字节。
    /// 表示进程当前占用的物理内存量。
    /// </summary>
    [ObservableProperty] private long _javaWorkingSetBytes;

    /// <summary>
    /// Java 进程私有内存大小，单位为字节。
    /// 表示仅由该进程独占的内存量。
    /// </summary>
    [ObservableProperty] private long _javaPrivateBytes;

    /// <summary>
    /// Java 进程活动线程数。
    /// </summary>
    [ObservableProperty] private int _javaThreadCount;

    /// <summary>
    /// Java 进程句柄数。
    /// Windows 平台有效，其他平台可能为 0。
    /// </summary>
    [ObservableProperty] private int _javaHandleCount;

    /// <summary>
    /// Java 堆内存已使用大小，单位为字节。
    /// 对应 JVM 监控指标 HeapMemoryUsage.used。
    /// </summary>
    [ObservableProperty] private long _javaHeapUsedBytes;

    /// <summary>
    /// Java 堆内存最大容量，单位为字节。
    /// 对应 JVM 参数 -Xmx 的值。
    /// </summary>
    [ObservableProperty] private long _javaHeapMaxBytes;

    /// <summary>
    /// 指标采集时间戳。
    /// 表示该快照数据的生成时刻。
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
