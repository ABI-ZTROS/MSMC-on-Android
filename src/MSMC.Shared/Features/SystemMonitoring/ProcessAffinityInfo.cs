// -----------------------------------------------------------------------------
// 文件名: ProcessAffinityInfo.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Models
// 功能描述: Java 进程亲和性信息 DTO —— 描述进程与 CPU 逻辑核的关联关系
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Models;

/// <summary>
/// Java 进程亲和性信息 —— 描述进程与 CPU 逻辑核的关联关系
/// </summary>
public record ProcessAffinityInfo
{
    /// <summary>进程 PID</summary>
    public int ProcessId { get; init; }

    /// <summary>进程名（如 java、javaw、chrome 等）</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>是否为 Minecraft 服务器进程（由 ProcessScanner 识别）</summary>
    public bool IsMinecraftServer { get; init; }

    /// <summary>是否为 Java 进程（java/javaw）</summary>
    public bool IsJavaProcess { get; init; }

    /// <summary>是否为系统进程（PID ≤ 4 或位于 System Idle / Registry / Session Window 等关键系统进程名）</summary>
    public bool IsSystemProcess { get; init; }

    /// <summary>服务器显示名（仅 Minecraft 进程有值）</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>CPU 亲和性掩码（位 N=1 表示允许在逻辑核 N 运行）</summary>
    public long AffinityMask { get; init; }

    /// <summary>亲和性掩码对应的逻辑核编号列表</summary>
    public int[] AllowedCoreIndices { get; init; } = [];

    /// <summary>进程总 CPU 使用率百分比（0-100）</summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>工作集内存（字节）</summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>线程数</summary>
    public int ThreadCount { get; init; }

    /// <summary>进程优先级</summary>
    public string PriorityClass { get; init; } = string.Empty;

    /// <summary>命令行参数（截断显示）</summary>
    public string CommandLine { get; init; } = string.Empty;
}
