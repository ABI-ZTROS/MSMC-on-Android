// -----------------------------------------------------------------------------
// 文件名: KnownServer.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Models
// 功能描述: 已知服务器数据契约，持久化存储用户导入的服务器配置元数据
// 依赖组件: System.Guid
// 设计模式: POCO 数据模型 + 纯贫血模型
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ServerDetection.Models;

/// <summary>
/// 已知服务器 POCO 数据模型，表示用户已导入并保存至配置中的服务器条目。
/// 作为应用配置持久化层的数据契约，不包含业务逻辑与属性变更通知。
/// </summary>
public class KnownServer
{
    /// <summary>
    /// 服务器唯一标识符，采用 GUID 字符串形式。
    /// 默认值为 Guid.NewGuid() 生成的随机标识。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 统一命名的已知服务器 ID，与 <see cref="ServerInstance.KnownServerId"/> 对应。
    /// 本质是 <see cref="Id"/> 的别名（get/set 直接代理），用于桥接层统一命名，
    /// 避免一处用 Id、另一处用 KnownServerId 导致命名混乱。
    /// 序列化时仍以 <see cref="Id"/> 为准（此属性不参与 JSON 序列化）。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [System.Xml.Serialization.XmlIgnore]
    public string KnownServerId
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// 用户自定义的服务器显示名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 服务器核心 JAR 文件的绝对路径。
    /// </summary>
    public string ServerJarPath { get; set; } = string.Empty;

    /// <summary>
    /// 服务器工作目录绝对路径，即 server.properties 所在目录。
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Java 虚拟机可执行文件的绝对路径。
    /// </summary>
    public string JavaPath { get; set; } = string.Empty;

    /// <summary>
    /// 初始堆内存大小，单位为字节。
    /// 对应 JVM 参数 -Xms。
    /// </summary>
    public long InitialHeapMemoryBytes { get; set; }

    /// <summary>
    /// 最大堆内存大小，单位为字节。
    /// 对应 JVM 参数 -Xmx。
    /// </summary>
    public long MaxHeapMemoryBytes { get; set; }

    /// <summary>
    /// 服务器监听端口号。
    /// 默认值为 Minecraft 标准端口 25565。
    /// </summary>
    public int Port { get; set; } = 25565;

    /// <summary>
    /// 用户配置的 JVM 参数列表。
    /// 用于启动服务器时组装命令行。
    /// </summary>
    public List<string> JvmArguments { get; set; } = [];

    /// <summary>
    /// 用户备注信息，可存储任意文本说明。
    /// 为 null 表示未设置备注。
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// 服务器分组名称，用于 UI 中服务器列表的分类展示。
    /// 默认值为 "默认"。
    /// </summary>
    public string Group { get; set; } = "默认";

    /// <summary>
    /// 服务器添加时间戳，即首次导入配置的时刻。
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 服务器最后一次被检测到运行的时间戳。
    /// 用于判断服务器活跃度。
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 指示是否为收藏的服务器。
    /// 收藏的服务器在 UI 中置顶或高亮显示。
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// 服务器级进程监管策略覆盖。
    /// 仅非 null 的字段会覆盖全局 <see cref="io.NET.ZTR_OS.Features.Settings.Services.AppConfig.Supervisor"/>，
    /// 其余字段走全局默认（实现字段级而非整对象级覆盖）。
    /// </summary>
    public PerServerSupervisorPolicy? Supervisor { get; set; }

    /// <summary>
    /// 启动配置子对象 — 启动模式、脚本路径、解析快照。
    /// 可空：旧 KnownServer JSON 没有此字段时为 null，启动时自动检测一次。
    /// </summary>
    public StartupConfig? Startup { get; set; }
}

/// <summary>启动模式枚举</summary>
public enum StartupMode
{
    /// <summary>手动组装 java 命令（默认）</summary>
    Manual = 0,
    /// <summary>直接调用 .bat 启动脚本</summary>
    Script = 1,
}

/// <summary>启动脚本配置快照 — 固化扫描 + 解析结果，避免每次启动重 parse</summary>
public class StartupConfig
{
    /// <summary>启动模式：Manual=手动组装java命令, Script=直接调.bat</summary>
    public StartupMode Mode { get; set; } = StartupMode.Manual;

    /// <summary>识别到/用户指定的启动脚本绝对路径</summary>
    public string? ScriptPath { get; set; }

    /// <summary>脚本文件名（start.bat / run.bat / 自定义）</summary>
    public string? ScriptName { get; set; }

    /// <summary>脚本最后一次解析时间</summary>
    public DateTime? LastParseTime { get; set; }

    /// <summary>脚本是否包含自动重启循环（Supervisor 据此互斥禁用崩溃自动重启）</summary>
    public bool HasAutoRestart { get; set; }

    /// <summary>上次解析时提取的 JVM 参数快照（用于 diff 对比用户手动改动）</summary>
    public List<string> ScriptJvmArgs { get; set; } = [];

    /// <summary>上次解析时提取的 Jar 路径（用于启动时 sanity check）</summary>
    public string? ScriptJarPath { get; set; }

    /// <summary>上次解析时提取的最大堆内存（字节）</summary>
    public long ScriptMaxHeapBytes { get; set; }

    /// <summary>上次解析时提取的初始堆内存（字节）</summary>
    public long ScriptInitialHeapBytes { get; set; }
}

/// <summary>
/// 服务器级监管策略覆盖 —— 所有字段均为可空，null 表示继承全局策略。
/// 目的：用户想仅对某台服改「崩溃不重启」时，不必复制整个策略对象。
/// </summary>
public class PerServerSupervisorPolicy
{
    /// <summary>服务器级覆盖：是否启用崩溃自动重启（null = 走全局）。</summary>
    public bool? EnableCrashRestart { get; set; }

    /// <summary>服务器级覆盖：每小时最多重启次数（null = 走全局）。</summary>
    public int? MaxRestartAttemptsPerHour { get; set; }

    /// <summary>服务器级覆盖：冷却秒数（null = 走全局）。</summary>
    public int? RestartCooldownSeconds { get; set; }

    /// <summary>服务器级覆盖：是否阻止系统睡眠（注意：此开关本质是「只要有服在跑就锁」，更推荐走全局）。</summary>
    public bool? PreventSystemSleepWhenRunning { get; set; }

    /// <summary>服务器级覆盖：进程优先级类（null = 走全局）。</summary>
    public System.Diagnostics.ProcessPriorityClass? ProcessPriority { get; set; }

    /// <summary>服务器级覆盖：单服提交内存上限字节（null = 走全局；0 = 不限制）。</summary>
    public long? MaxProcessMemoryBytes { get; set; }

    /// <summary>服务器级覆盖：总重启次数上限（null = 走全局；-1 = 无限；0 = 永不重启）。</summary>
    public int? MaxTotalRestartAttempts { get; set; }
}
