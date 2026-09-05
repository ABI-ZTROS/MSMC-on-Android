// -----------------------------------------------------------------------------
// 文件名: HardwareInfo.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Models.Hardware
// 功能描述: 硬件信息值对象集合，封装 CPU、内存模块及系统内存的元数据
// 依赖组件: 无
// 设计模式: 值对象（Record）+ 不可变数据模型
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Models.Hardware;

/// <summary>
/// CPU 详细信息值对象，封装处理器的完整硬件规格元数据。
/// 作为硬件检测模块的输出契约，所有属性仅初始化时赋值，运行时不可变。
/// </summary>
public record CpuInfo
{
    /// <summary>
    /// CPU 型号名称。
    /// 例如 "Intel Core i9-13900K"。
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// CPU 制造商标识。
    /// 例如 "GenuineIntel" 或 "AuthenticAMD"。
    /// </summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>
    /// CPU 架构标识。
    /// 例如 "x86_64"、"ARM64" 等。
    /// </summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>
    /// CPU 代数标识。
    /// 例如 13 代表第 13 代酷睿处理器。
    /// 未识别时为 0。
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    /// 物理核心数量。
    /// 即 CPU 实际集成的物理核心数。
    /// </summary>
    public int PhysicalCores { get; init; }

    /// <summary>
    /// 逻辑核心数量。
    /// 含超线程技术后的逻辑处理器总数。
    /// </summary>
    public int LogicalCores { get; init; }

    /// <summary>
    /// 基础主频，单位为吉赫兹（GHz）。
    /// </summary>
    public double BaseClockGHz { get; init; }

    /// <summary>
    /// 加速/睿频主频，单位为吉赫兹（GHz）。
    /// </summary>
    public double BoostClockGHz { get; init; }

    /// <summary>
    /// Cinebench R23 单核跑分。
    /// 为 null 表示暂无基准测试数据。
    /// </summary>
    public int? CinebenchR23Single { get; init; }

    /// <summary>
    /// Cinebench R23 多核跑分。
    /// 为 null 表示暂无基准测试数据。
    /// </summary>
    public int? CinebenchR23Multi { get; init; }

    /// <summary>
    /// CPU 发布日期。
    /// 为 null 表示发布日期未知。
    /// </summary>
    public DateTime? ReleaseDate { get; init; }

    /// <summary>
    /// CPU 性能等级标识。
    /// 如 "入门"、"主流"、"高端"、"旗舰" 等。
    /// 默认值为 "未知"。
    /// </summary>
    public string Tier { get; init; } = "未知";

    /// <summary>
    /// CPU 插槽类型。
    /// 例如 "LGA1700"、"AM5" 等。
    /// </summary>
    public string Socket { get; init; } = string.Empty;

    /// <summary>
    /// 综合性能评分。
    /// 基于多项基准测试加权计算得出。
    /// </summary>
    public double PerformanceScore { get; init; }

    /// <summary>
    /// 物理 CPU 插槽数（即 CPU 芯片个数）。
    /// </summary>
    public int SocketCount { get; init; }

    /// <summary>
    /// NUMA 节点数量。
    /// </summary>
    public int NumaNodeCount { get; init; }

    /// <summary>
    /// 是否启用超线程（逻辑核心数 > 物理核心数）。
    /// </summary>
    public bool IsHyperThreadingEnabled { get; init; }

    /// <summary>
    /// 逻辑核心编号到物理核心编号的映射数组。
    /// 数组索引为逻辑核心号，值为物理核心号。
    /// 例如 [0, 0, 1, 1] 表示 2 个物理核心，每个物理核心 2 个线程。
    /// </summary>
    public int[] LogicalToPhysicalCoreMap { get; init; } = [];

    /// <summary>
    /// 指示 CPU 是否被成功识别。
    /// 未识别时其他字段可能为默认值或空。
    /// </summary>
    public bool IsRecognized { get; init; }
}

/// <summary>
/// 内存模块信息值对象，封装单条物理内存的规格元数据。
/// 作为硬件检测模块的输出契约，所有属性仅初始化时赋值，运行时不可变。
/// </summary>
public record MemoryModuleInfo
{
    /// <summary>
    /// 内存模块容量，单位为字节。
    /// </summary>
    public long CapacityBytes { get; init; }

    /// <summary>
    /// 内存运行频率，单位为兆赫兹（MHz）。
    /// </summary>
    public int SpeedMHz { get; init; }

    /// <summary>
    /// 内存类型标识。
    /// 常见值包括 DDR4、DDR5 等。
    /// </summary>
    public string MemoryType { get; init; } = string.Empty;

    /// <summary>
    /// 内存制造商标识。
    /// 例如 "Samsung"、"SK Hynix"、"Micron" 等。
    /// </summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>
    /// 内存部件编号。
    /// 厂商定义的产品型号标识。
    /// </summary>
    public string PartNumber { get; init; } = string.Empty;

    /// <summary>
    /// 内存插槽编号。
    /// 标识该内存模块安装在主板的第几个插槽。
    /// </summary>
    public int SlotNumber { get; init; }
}

/// <summary>
/// 系统内存总览信息值对象，封装系统内存的汇总数据与各模块明细。
/// 作为硬件检测模块的输出契约，所有属性仅初始化时赋值，运行时不可变。
/// </summary>
public record MemorySystemInfo
{
    /// <summary>
    /// 系统总内存容量，单位为字节。
    /// 即所有内存模块容量之和。
    /// </summary>
    public long TotalCapacityBytes { get; init; }

    /// <summary>
    /// 已安装的内存模块数量。
    /// </summary>
    public int ModuleCount { get; init; }

    /// <summary>
    /// 内存运行频率，单位为兆赫兹（MHz）。
    /// 取所有模块的最低共同频率。
    /// </summary>
    public int SpeedMHz { get; init; }

    /// <summary>
    /// 内存类型标识。
    /// 常见值包括 DDR4、DDR5 等。
    /// </summary>
    public string MemoryType { get; init; } = string.Empty;

    /// <summary>
    /// 指示是否工作在双通道模式。
    /// </summary>
    public bool IsDualChannel { get; init; }

    /// <summary>
    /// 各内存模块的详细信息列表。
    /// 集合元素顺序对应插槽编号升序。
    /// </summary>
    public List<MemoryModuleInfo> Modules { get; init; } = [];
}
