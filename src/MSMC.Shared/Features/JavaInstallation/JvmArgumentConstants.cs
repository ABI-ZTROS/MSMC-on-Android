// -----------------------------------------------------------------------------
// 文件名: JvmArgumentConstants.cs
// 命名空间: io.NET.ZTR_OS.Features.JavaInstallation.Constants
// 功能描述: JVM 参数定义与常量集合，提供参数元数据、分类及查找能力
// 依赖组件: System.Linq
// 设计模式: 常量容器 + 策略模式参数定义 + 规范化查找
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.JavaInstallation.Constants;

/// <summary>
/// JVM 参数值类型枚举，表示参数值的数据类型。
/// 用于 UI 编辑控件的选择与输入校验。
/// </summary>
public enum ArgumentValueType
{
    /// <summary>
    /// 无值类型，纯标志位参数。
    /// </summary>
    None,

    /// <summary>
    /// 数值类型，整数或浮点数。
    /// </summary>
    Number,

    /// <summary>
    /// 内存大小类型，支持 K/M/G 等单位后缀。
    /// </summary>
    MemorySize,

    /// <summary>
    /// 布尔标志类型，+ 启用 / - 禁用。
    /// </summary>
    BooleanFlag,

    /// <summary>
    /// 字符串类型，任意文本值。
    /// </summary>
    String,

    /// <summary>
    /// 枚举类型，仅支持预定义的取值集合。
    /// </summary>
    Enum
}

/// <summary>
/// JVM 参数分类枚举，用于参数的分组展示与筛选。
/// </summary>
public enum ArgumentCategory
{
    /// <summary>
    /// 内存相关参数。
    /// </summary>
    Memory,

    /// <summary>
    /// 垃圾回收相关参数。
    /// </summary>
    GarbageCollection,

    /// <summary>
    /// 性能调优相关参数。
    /// </summary>
    Performance,

    /// <summary>
    /// 编码相关参数。
    /// </summary>
    Encoding,

    /// <summary>
    /// 安全相关参数。
    /// </summary>
    Security,

    /// <summary>
    /// 调试相关参数。
    /// </summary>
    Debug,

    /// <summary>
    /// 服务器行为相关参数。
    /// </summary>
    ServerBehavior,

    /// <summary>
    /// 其他未分类参数。
    /// </summary>
    Other
}

/// <summary>
/// JVM 参数定义数据契约，封装单个 JVM 参数的完整元数据。
/// 包含参数标志、名称、描述、值类型、取值范围及推荐状态等信息。
/// 作为参数配置模块的核心数据模型。
/// </summary>
public class JvmArgumentDefinition
{
    /// <summary>
    /// JVM 参数标志字符串。
    /// 例如 "-Xmx"、"-XX:+UseG1GC"。
    /// </summary>
    public string Flag { get; init; } = string.Empty;

    /// <summary>
    /// 参数的本地化显示名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 参数的功能描述文本。
    /// 说明参数的作用与适用场景。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 参数值的数据类型。
    /// 决定 UI 编辑控件的类型。
    /// </summary>
    public ArgumentValueType ValueType { get; init; }

    /// <summary>
    /// 参数所属分类。
    /// 用于分组展示与筛选。
    /// </summary>
    public ArgumentCategory Category { get; init; }

    /// <summary>
    /// 参数的默认值。
    /// 为 null 表示无默认值。
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// 参数的最小允许值。
    /// 为 null 表示无下限约束。
    /// </summary>
    public string? MinimumValue { get; init; }

    /// <summary>
    /// 参数的最大允许值。
    /// 为 null 表示无上限约束。
    /// </summary>
    public string? MaximumValue { get; init; }

    /// <summary>
    /// 参数的允许取值集合。
    /// 仅枚举类型参数有效，为 null 表示无枚举限制。
    /// </summary>
    public string[]? AllowedValues { get; init; }

    /// <summary>
    /// 指示是否为推荐参数。
    /// 推荐参数在 UI 中高亮或置顶显示。
    /// </summary>
    public bool Recommended { get; init; }

    /// <summary>
    /// 参数的警告信息。
    /// 描述使用该参数的注意事项或潜在风险。
    /// 为 null 表示无警告。
    /// </summary>
    public string? Warning { get; init; }

    /// <summary>
    /// 指示是否为实验性参数。
    /// 实验性参数需要 -XX:+UnlockExperimentalVMOptions 前置启用。
    /// JVM 规范要求 UnlockExperimentalVMOptions 必须出现在实验性参数之前。
    /// </summary>
    public bool RequiresExperimentalUnlock { get; init; }
}

/// <summary>
/// JVM 参数常量容器类，提供常用 JVM 参数标志定义与完整参数元数据集合。
/// 包含参数分类查询、推荐参数筛选及标志查找等辅助方法。
/// </summary>
public static class JvmArgumentConstants
{
    /// <summary>
    /// 内存参数标志常量。
    /// 用于从命令行中解析内存配置。
    /// </summary>
    public const string InitialHeapMemory = "-Xms";
    public const string MaxHeapMemory = "-Xmx";
    public const string MetaspaceSize = "-XX:MetaspaceSize=";
    public const string MaxMetaspaceSize = "-XX:MaxMetaspaceSize=";

    /// <summary>
    /// 垃圾回收器选择标志常量。
    /// 用于识别当前使用的 GC 类型。
    /// </summary>
    public const string G1GC = "-XX:+UseG1GC";
    public const string ZGC = "-XX:+UseZGC";
    public const string ShenandoahGC = "-XX:+UseShenandoahGC";
    public const string ParallelGC = "-XX:+UseParallelGC";

    /// <summary>
    /// Aikar 参数标识常量。
    /// 用于判定是否使用 Aikar 推荐参数集。
    /// </summary>
    public const string AikarFlagIdentifier = "-Dusing.aikars.flags=";
    public const string AikarNewFlagIdentifier = "-Daikars.new.flags=true";

    /// <summary>
    /// JVM 参数前缀与模式常量。
    /// 用于参数解析与规范化处理。
    /// </summary>
    public const string JvmFlagPrefix = "-XX:";
    public const string SystemPropertyPrefix = "-D";
    public const string GcLogPatternLegacy = "-Xloggc:";
    public const string GcLogPatternModern = "-Xlog:gc*";

    /// <summary>
    /// 服务器启动相关标志常量。
    /// 用于识别 nogui 模式与 -jar 参数。
    /// </summary>
    public const string NoGuiLegacy = "nogui";
    public const string NoGuiModern = "--nogui";
    public const string JarFlag = "-jar";

    /// <summary>
    /// 全部 JVM 参数定义集合。
    /// 包含内存、GC、性能、安全等各分类的参数元数据。
    /// </summary>
    public static readonly List<JvmArgumentDefinition> AllArguments =
    [
        new()
        {
            Flag = "-Xms",
            Name = "初始堆内存",
            Description = "JVM 启动时分配的初始堆内存大小。建议与 -Xmx 设置相同，避免运行中动态扩容导致卡顿。",
            ValueType = ArgumentValueType.MemorySize,
            Category = ArgumentCategory.Memory,
            DefaultValue = "2G",
            MinimumValue = "512M",
            MaximumValue = "物理内存的75%",
            Recommended = true,
            Warning = "不要小于 512MB，否则服务器可能无法启动"
        },
        new()
        {
            Flag = "-Xmx",
            Name = "最大堆内存",
            Description = "JVM 允许使用的最大堆内存大小。这是服务器最重要的性能参数。",
            ValueType = ArgumentValueType.MemorySize,
            Category = ArgumentCategory.Memory,
            DefaultValue = "4G",
            MinimumValue = "1G",
            MaximumValue = "物理内存的75%",
            Recommended = true,
            Warning = "不要超过物理内存的 75%，否则会导致系统内存不足"
        },
        new()
        {
            Flag = "-XX:+UseG1GC",
            Name = "启用 G1GC",
            Description = "使用 G1（Garbage First）垃圾回收器。专为大堆内存优化，能控制暂停时间，减少卡顿。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.GarbageCollection,
            Recommended = true,
            Warning = "Java 9+ 默认启用，强烈推荐用于 Minecraft 服务器"
        },
        new()
        {
            Flag = "-XX:+UseZGC",
            Name = "启用 ZGC",
            Description = "使用 ZGC（Z Garbage Collector）。极低延迟的垃圾回收器，适合 8GB 以上内存的服务器。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.GarbageCollection,
            Warning = "仅支持 Java 11+，需要至少 8GB 内存才能发挥优势"
        },
        new()
        {
            Flag = "-XX:+UseShenandoahGC",
            Name = "启用 ShenandoahGC",
            Description = "使用 Shenandoah 垃圾回收器。低延迟设计，适合对响应速度要求高的场景。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.GarbageCollection,
            Warning = "OpenJDK 12+ 可用，部分发行版可能不支持"
        },
        new()
        {
            Flag = "-XX:MaxGCPauseMillis=",
            Name = "GC 最大暂停时间",
            Description = "向垃圾回收器建议的最大暂停时间目标（毫秒）。G1GC 会调整行为来尽量满足此目标。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "200",
            MinimumValue = "50",
            MaximumValue = "500",
            Recommended = true,
            Warning = "设置太小会导致 GC 过于频繁，反而降低性能"
        },
        new()
        {
            Flag = "-XX:+AlwaysPreTouch",
            Name = "内存预触",
            Description = "启动时立即分配并触摸所有堆内存页。避免运行中因缺页中断导致的延迟波动。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Recommended = true
        },
        new()
        {
            Flag = "-XX:+DisableExplicitGC",
            Name = "禁用显式 GC",
            Description = "禁止代码调用 System.gc()。某些插件/模组会误调用，导致长时间 Full GC 卡顿。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.GarbageCollection,
            Recommended = true
        },
        new()
        {
            Flag = "-XX:+ParallelRefProcEnabled",
            Name = "并行引用处理",
            Description = "并行处理引用对象，减少 GC 暂停时间。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.GarbageCollection,
            Recommended = true
        },
        new()
        {
            Flag = "-XX:G1NewSizePercent=",
            Name = "新生代最小比例",
            Description = "G1GC 新生代占堆的最小百分比。Minecraft 创建大量短生命周期对象，建议设为 30-40%。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "30",
            MinimumValue = "5",
            MaximumValue = "60",
            Recommended = true,
            RequiresExperimentalUnlock = true
        },
        new()
        {
            Flag = "-XX:G1MaxNewSizePercent=",
            Name = "新生代最大比例",
            Description = "G1GC 新生代占堆的最大百分比。防止新生代过大影响老年代回收效率。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "40",
            MinimumValue = "10",
            MaximumValue = "70",
            Recommended = true,
            RequiresExperimentalUnlock = true
        },
        new()
        {
            Flag = "-XX:G1HeapRegionSize=",
            Name = "G1 堆区域大小",
            Description = "G1GC 每个堆区域的大小。匹配 Minecraft 区块大小（约 8MB）最佳。",
            ValueType = ArgumentValueType.MemorySize,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "8M",
            AllowedValues = ["1M", "2M", "4M", "8M", "16M", "32M"],
            Recommended = true,
            Warning = "必须是 2 的幂，且不能超过堆内存的 1/20"
        },
        new()
        {
            Flag = "-XX:G1ReservePercent=",
            Name = "G1 预留比例",
            Description = "G1GC 预留的内存比例，用于应对晋升失败。防止 Evacuation Failure 导致的 Full GC。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "20",
            MinimumValue = "10",
            MaximumValue = "30",
            Recommended = true
        },
        new()
        {
            Flag = "-XX:InitiatingHeapOccupancyPercent=",
            Name = "并发标记触发阈值",
            Description = "当堆占用达到此百分比时启动并发标记周期。低内存环境建议设为 15-20%。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "15",
            MinimumValue = "5",
            MaximumValue = "50",
            Recommended = true
        },
        new()
        {
            Flag = "-XX:MaxTenuringThreshold=",
            Name = "最大晋升年龄",
            Description = "对象经历多少次 Minor GC 后晋升到老年代。Minecraft 对象生命周期极短，建议设为 1。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "1",
            MinimumValue = "0",
            MaximumValue = "15",
            Recommended = true
        },
        new()
        {
            Flag = "-XX:SurvivorRatio=",
            Name = "Survivor 区比例",
            Description = "Eden 区与 Survivor 区的比例。比例越大，Survivor 区越小。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "32",
            MinimumValue = "8",
            MaximumValue = "64",
            Recommended = true
        },
        new()
        {
            Flag = "-XX:+PerfDisableSharedMem",
            Name = "禁用性能计数器共享内存",
            Description = "禁用 JVM 性能计数器共享内存，减少小文件创建开销，避免 GC 暂停。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Recommended = true
        },
        new()
        {
            Flag = "-Dfile.encoding=UTF-8",
            Name = "UTF-8 编码",
            Description = "强制 JVM 使用 UTF-8 编码读取配置文件。解决中文 MOTD、日志乱码问题。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Encoding,
            Recommended = true
        },
        new()
        {
            Flag = "-Dlog4j2.formatMsgNoLookups=true",
            Name = "Log4Shell 防护",
            Description = "修复 Log4Shell 漏洞，同时提升日志性能。强烈建议启用。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Security,
            Recommended = true
        },
        new()
        {
            Flag = "-Dusing.aikars.flags=",
            Name = "Aikar 标志标识",
            Description = "标识使用 Aikar 推荐参数，便于社区识别和兼容某些性能补丁。",
            ValueType = ArgumentValueType.String,
            Category = ArgumentCategory.Other,
            DefaultValue = "https://mcflags.emc.gs",
            Recommended = true
        },
        new()
        {
            Flag = "-Daikars.new.flags=true",
            Name = "Aikar 新版标志",
            Description = "启用 Aikar 新版参数集的标识。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Other,
            Recommended = true
        },
        new()
        {
            Flag = "-XX:+UseStringDeduplication",
            Name = "字符串去重",
            Description = "对字符串常量池进行去重，节省内存。大型世界中可节省 15-28% 内存。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Recommended = true,
            Warning = "仅在 G1GC 下有效"
        },
        new()
        {
            Flag = "-XX:+UseCompactObjectHeaders",
            Name = "紧凑对象头",
            Description = "压缩对象头，减少内存开销。需要 Java 21+。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Warning = "仅 Java 21+ 支持",
            RequiresExperimentalUnlock = true
        },
        new()
        {
            Flag = "-XX:+UnlockExperimentalVMOptions",
            Name = "解锁实验性选项",
            Description = "允许使用实验性的 JVM 参数。某些高级调优参数需要此选项。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Other,
            Recommended = true
        },
        new()
        {
            Flag = "-XX:+UnlockDiagnosticVMOptions",
            Name = "解锁诊断选项",
            Description = "允许使用诊断级别的 JVM 参数。一般不需要，仅用于深度调优。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Other
        },
        new()
        {
            Flag = "-Xlog:gc*:gc.log:time",
            Name = "GC 日志",
            Description = "启用 GC 日志记录到 gc.log 文件。用于分析 GC 行为和性能问题。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Debug
        },
        new()
        {
            Flag = "-server",
            Name = "服务器模式",
            Description = "启用服务器级 JVM 模式，优化长时间运行的应用程序。Java 8+ 某些场景下默认启用。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance
        },
        new()
        {
            Flag = "-XX:ParallelGCThreads=",
            Name = "并行 GC 线程数",
            Description = "并行垃圾回收使用的线程数。建议设为 CPU 核心数的 50-75%。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "CPU核心数的50%"
        },
        new()
        {
            Flag = "-XX:ConcGCThreads=",
            Name = "并发 GC 线程数",
            Description = "并发垃圾回收使用的线程数。通常为 ParallelGCThreads 的 1/4。",
            ValueType = ArgumentValueType.Number,
            Category = ArgumentCategory.GarbageCollection,
            DefaultValue = "ParallelGCThreads的1/4"
        },
        new()
        {
            Flag = "-XX:+UseNUMA",
            Name = "NUMA 支持",
            Description = "启用 NUMA（非均匀内存访问）优化，提升多 CPU 系统性能。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Warning = "仅在多 CPU 系统上有效"
        },
        new()
        {
            Flag = "-XX:+UseLargePages",
            Name = "大页支持",
            Description = "使用大内存页，减少 TLB 缓存失效，提升内存访问性能。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Warning = "需要操作系统配置支持"
        },
        new()
        {
            Flag = "-XX:+ResizeTLAB",
            Name = "TLAB 动态调整",
            Description = "允许动态调整线程本地分配缓冲区大小，适应不同的分配模式。",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Recommended = true
        },
        new()
        {
            Flag = "-XX:+UseFastAccessorMethods",
            Name = "快速访问方法",
            Description = "使用优化的字段访问方法，提升性能。（JDK 17+ 已废弃）",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Recommended = false,
            Warning = "[WARN] 该参数在 JDK 17+ 中已被废弃并移除，不建议使用"
        },
        new()
        {
            Flag = "-XX:+OptimizeStringConcat",
            Name = "字符串拼接优化",
            Description = "优化字符串拼接操作，减少临时对象创建。（现代 JDK 默认启用）",
            ValueType = ArgumentValueType.BooleanFlag,
            Category = ArgumentCategory.Performance,
            Recommended = false,
            Warning = "[WARN] 现代 JDK 默认启用此优化，显式设置已无意义"
        },
        new()
        {
            Flag = "-XX:MetaspaceSize=",
            Name = "元空间初始大小",
            Description = "Metaspace（元空间）的初始大小。Mod 服需要更大的元空间。",
            ValueType = ArgumentValueType.MemorySize,
            Category = ArgumentCategory.Memory,
            DefaultValue = "256M",
            MinimumValue = "64M",
            MaximumValue = "1G"
        },
        new()
        {
            Flag = "-XX:MaxMetaspaceSize=",
            Name = "元空间最大大小",
            Description = "Metaspace 的最大大小。防止元空间无限增长。",
            ValueType = ArgumentValueType.MemorySize,
            Category = ArgumentCategory.Memory,
            DefaultValue = "512M",
            MinimumValue = "128M",
            MaximumValue = "2G"
        }
    ];

    /// <summary>
    /// 按分类筛选 JVM 参数定义。
    /// </summary>
    /// <param name="category">参数分类</param>
    /// <returns>指定分类下的所有参数定义列表</returns>
    public static List<JvmArgumentDefinition> GetArgumentsByCategory(ArgumentCategory category)
    {
        return AllArguments.Where(a => a.Category == category).ToList();
    }

    /// <summary>
    /// 获取所有推荐的 JVM 参数定义。
    /// </summary>
    /// <returns>推荐参数定义列表</returns>
    public static List<JvmArgumentDefinition> GetRecommendedArguments()
    {
        return AllArguments.Where(a => a.Recommended).ToList();
    }

    /// <summary>
    /// 根据参数标志查找对应的参数定义。
    /// 支持精确匹配、前缀匹配与反向前缀匹配三种策略。
    /// </summary>
    /// <param name="flag">参数标志字符串</param>
    /// <returns>匹配的参数定义，未找到时返回 null</returns>
    public static JvmArgumentDefinition? FindByFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
            return null;

        var normalized = NormalizeFlagForLookup(flag);

        var exact = AllArguments.FirstOrDefault(a =>
            NormalizeFlagForLookup(a.Flag).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var prefix = AllArguments.FirstOrDefault(a =>
            NormalizeFlagForLookup(a.Flag).StartsWith(normalized, StringComparison.OrdinalIgnoreCase));
        if (prefix != null) return prefix;

        return AllArguments.FirstOrDefault(a =>
            normalized.StartsWith(NormalizeFlagForLookup(a.Flag), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 规范化 JVM 参数标志用于查找匹配。
    /// 移除 -XX:+/-、-XX:、-X、-/+ 等前缀噪音，保留核心参数名。
    /// </summary>
    /// <param name="flag">原始参数标志</param>
    /// <returns>规范化后的参数名</returns>
    private static string NormalizeFlagForLookup(string flag)
    {
        var s = flag.Trim();
        if (s.StartsWith("-XX:+", StringComparison.OrdinalIgnoreCase))
            s = s[5..];
        else if (s.StartsWith("-XX:-", StringComparison.OrdinalIgnoreCase))
            s = s[5..];
        else if (s.StartsWith("-XX:", StringComparison.OrdinalIgnoreCase))
            s = s[4..];
        else if (s.StartsWith("-X", StringComparison.OrdinalIgnoreCase) && s.Length > 2)
            s = s[2..];
        else if (s.StartsWith('-') || s.StartsWith('+'))
            s = s[1..];
        return s;
    }
}
