// -----------------------------------------------------------------------------
// 文件名: ServerInstance.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Models
// 功能描述: 服务器实例数据契约，表示运行中或已识别的 Minecraft 服务器进程
// 依赖组件: CommunityToolkit.Mvvm, ServerConstants, ServerType
// 设计模式: 贫血模型 + ObservableObject 属性变更通知
// -----------------------------------------------------------------------------
using CommunityToolkit.Mvvm.ComponentModel;
using io.NET.ZTR_OS.Features.JavaInstallation.Constants;

namespace io.NET.ZTR_OS.Features.ServerDetection.Models;

/// <summary>
/// 服务器实例数据契约，承载被检测到的 Minecraft 服务器进程的完整元数据。
/// 作为进程检测模块与 UI 层之间的 DTO，支持属性变更通知以驱动绑定更新。
/// </summary>
public partial class ServerInstance : ObservableObject
{
    /// <summary>
    /// 操作系统进程标识符。
    /// 仅在服务器运行时有效，已知服务器未运行时为默认值 0。
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 服务器类型枚举值，标识服务端核心品牌。
    /// 初始值为 Unknown，经检测逻辑判定后更新。
    /// </summary>
    [ObservableProperty] private ServerType _serverType = ServerType.Unknown;

    /// <summary>
    /// 服务器工作目录绝对路径，即 server.properties 所在目录。
    /// </summary>
    [ObservableProperty] private string _workingDirectory = string.Empty;

    /// <summary>
    /// Java 虚拟机可执行文件的绝对路径。
    /// </summary>
    [ObservableProperty] private string _javaPath = string.Empty;

    /// <summary>
    /// 服务器核心 JAR 文件的绝对路径。
    /// </summary>
    [ObservableProperty] private string _serverJarPath = string.Empty;

    /// <summary>
    /// 服务器核心 JAR 文件名（含扩展名）。
    /// </summary>
    [ObservableProperty] private string _serverJarName = string.Empty;

    /// <summary>
    /// 启动脚本文件的绝对路径。
    /// 若未检测到启动脚本则为 null。
    /// </summary>
    [ObservableProperty] private string? _startupScriptPath;

    /// <summary>
    /// 启动模式 — Manual=手动组装java命令, Script=直接调.bat
    /// 来自 KnownServer.Startup.Mode 传播。
    /// </summary>
    [ObservableProperty] private StartupMode _startupMode = StartupMode.Manual;

    /// <summary>
    /// 脚本是否含自动重启循环（Supervisor 互斥禁用崩溃重启用）
    /// </summary>
    [ObservableProperty] private bool _scriptHasAutoRestart;

    /// <summary>
    /// 进程完整命令行字符串，用于追溯原始启动参数。
    /// </summary>
    [ObservableProperty] private string _fullCommandLine = string.Empty;

    /// <summary>
    /// 从命令行解析得到的 JVM 参数列表。
    /// 集合初始化后为只读，通过集合初始化器赋值。
    /// </summary>
    public List<string> JvmArguments { get; init; } = [];

    /// <summary>
    /// 初始堆内存大小，单位为字节。
    /// 对应 JVM 参数 -Xms。
    /// </summary>
    [ObservableProperty] private long _initialHeapMemoryBytes;

    /// <summary>
    /// 最大堆内存大小，单位为字节。
    /// 对应 JVM 参数 -Xmx。
    /// </summary>
    [ObservableProperty] private long _maxHeapMemoryBytes;

    /// <summary>
    /// 服务器目录下检测到的配置文件路径列表。
    /// 包含 server.properties 及各服务端特有的配置文件。
    /// </summary>
    [ObservableProperty] private List<string> _configFiles = [];

    /// <summary>
    /// 指示是否使用 Aikar 推荐的 JVM 参数集。
    /// 基于命令行中是否包含 aikars.flags 标识判定。
    /// </summary>
    [ObservableProperty] private bool _usesAikarFlags;

    /// <summary>
    /// 垃圾回收器类型名称。
    /// 可能值包括 G1、ZGC、Shenandoah、Parallel 等。
    /// </summary>
    [ObservableProperty] private string _gcType = string.Empty;

    /// <summary>
    /// 服务器监听端口号。
    /// 默认值取自 ServerConstants.DefaultServerPort（25565）。
    /// </summary>
    [ObservableProperty] private int _serverPort = ServerConstants.DefaultServerPort;

    /// <summary>
    /// 指示配置端口是否真的在监听（TCP connect 探测结果）。
    /// 由网络套件的 PortScanner 探测后回填。
    /// </summary>
    [ObservableProperty] private bool _isPortOpen;

    /// <summary>
    /// 实际监听该端口的 PID（通过 IP Helper API 反查）。
    /// 与 ProcessId 不同时，说明端口被其他程序占用。
    /// 无监听时为 null。
    /// </summary>
    [ObservableProperty] private int? _actualListeningPid;

    /// <summary>
    /// 实例检测时间戳，即对象创建的时刻。
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.Now;

    /// <summary>
    /// 关联的已知服务器 ID（KnownServer.KnownServerId）。
    /// 当此运行中实例能够被关联到 KnownServers 列表中的某台服务器时，此字段非空。
    /// 用于桥接层正确标记 isKnown=true，以及 UI 层显示「已知」标签。
    /// 未关联时为 null。
    /// </summary>
    [ObservableProperty] private string? _knownServerId;

    /// <summary>
    /// 用于 UI 展示的格式化显示名称。
    /// 运行中的实例包含 PID，未运行实例仅显示类型与目录。
    /// 当 ProcessId 为 0 时不显示 PID，避免误导用户以为存在幽灵进程。
    /// ServerType 为 Unknown 时显示 "Minecraft Server"——因为能被扫描到说明确实是服务器，
    /// 只是核心品牌无法识别，不应向用户暴露内部枚举值 "Unknown"。
    /// </summary>
    public string DisplayName => ProcessId > 0
        ? $"{GetFriendlyTypeDisplay()} @ {System.IO.Path.GetFileName(WorkingDirectory)} (PID: {ProcessId})"
        : $"{GetFriendlyTypeDisplay()} @ {System.IO.Path.GetFileName(WorkingDirectory)}";

    /// <summary>
    /// 获取 ServerType 的友好显示文本。
    /// Unknown → "Minecraft Server"（已通过进程扫描确认为服务器，仅品牌未知）
    /// 其余类型直接使用枚举名（Paper/Spigot/Forge 等）
    /// </summary>
    private string GetFriendlyTypeDisplay()
        => ServerType == ServerType.Unknown ? "Minecraft Server" : ServerType.ToString();

    /// <summary>
    /// 以人类可读格式表示的最大堆内存字符串。
    /// 自动将字节数转换为 GB/MB/KB 单位。
    /// </summary>
    public string FormattedMaxMemory => FormatBytes(MaxHeapMemoryBytes);

    /// <summary>
    /// 网络状态文本（用于 UI 展示）。
    /// [OK]端口开放 | [WARN]端口被占用 | [ERR]端口未开放
    /// </summary>
    public string NetworkStatusText => (IsPortOpen, PortConflict) switch
    {
        (false, _)    => "[ERR] 端口未开放",
        (true, false) => "[OK] 端口开放",
        (true, true)  => "[WARN] 端口被占用",
    };

    /// <summary>
    /// 指示端口是否被其他进程占用（端口开放但监听 PID 与本实例 PID 不一致）。
    /// 用于 UI DataTrigger 变色。
    /// </summary>
    public bool PortConflict => IsPortOpen
        && ActualListeningPid.HasValue
        && ActualListeningPid.Value != ProcessId;

    /// <summary>
    /// 将字节数格式化为人类可读的内存大小字符串。
    /// 采用二进制换算（1 GB = 1024 MB），向下取整显示整数。
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <returns>格式化后的内存大小字符串</returns>
    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1L << 30)
            return $"{(bytes >> 30)} GB";
        if (bytes >= 1L << 20)
            return $"{(bytes >> 20)} MB";
        return $"{bytes} KB";
    }

    /// <summary>
    /// ServerType 属性变更回调，同步触发 DisplayName 的属性变更通知。
    /// </summary>
    partial void OnServerTypeChanged(ServerType value)
        => OnPropertyChanged(nameof(DisplayName));

    /// <summary>
    /// WorkingDirectory 属性变更回调，同步触发 DisplayName 的属性变更通知。
    /// </summary>
    partial void OnWorkingDirectoryChanged(string value)
        => OnPropertyChanged(nameof(DisplayName));

    /// <summary>
    /// MaxHeapMemoryBytes 属性变更回调，同步触发 FormattedMaxMemory 的属性变更通知。
    /// </summary>
    partial void OnMaxHeapMemoryBytesChanged(long value)
        => OnPropertyChanged(nameof(FormattedMaxMemory));

    /// <summary>
    /// IsPortOpen 变更回调，触发网络状态派生属性的通知。
    /// </summary>
    partial void OnIsPortOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(NetworkStatusText));
        OnPropertyChanged(nameof(PortConflict));
    }

    /// <summary>
    /// ActualListeningPid 变更回调，触发端口冲突派生属性的通知。
    /// </summary>
    partial void OnActualListeningPidChanged(int? value)
    {
        OnPropertyChanged(nameof(NetworkStatusText));
        OnPropertyChanged(nameof(PortConflict));
    }
}
