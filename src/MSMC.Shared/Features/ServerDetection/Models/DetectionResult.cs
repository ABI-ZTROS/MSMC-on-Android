// -----------------------------------------------------------------------------
// 文件名: DetectionResult.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Models
// 功能描述: 服务器检测结果聚合数据契约，封装单次扫描的全部输出
// 依赖组件: CommunityToolkit.Mvvm, ServerInstance, StartupScriptInfo
// 设计模式: 贫血模型 + ObservableObject 属性变更通知
// -----------------------------------------------------------------------------
using CommunityToolkit.Mvvm.ComponentModel;

namespace io.NET.ZTR_OS.Features.ServerDetection.Models;

/// <summary>
/// 服务器检测结果聚合数据契约，封装单次进程扫描操作的完整输出。
/// 作为检测服务与 UI 层之间的 DTO，包含服务器实例、启动脚本、耗时及日志等信息。
/// </summary>
public partial class DetectionResult : ObservableObject
{
    /// <summary>
    /// 指示是否检测到任何 Minecraft 服务器。
    /// 当 Servers 集合非空时为 true。
    /// </summary>
    [ObservableProperty] private bool _isDetected;

    /// <summary>
    /// 检测到的服务器实例列表。
    /// 每个元素代表一个独立运行的 Minecraft 服务器进程。
    /// </summary>
    [ObservableProperty] private List<ServerInstance> _servers = [];

    /// <summary>
    /// 检测到的启动脚本信息列表。
    /// 包含判定为服务器启动脚本的文件元数据。
    /// </summary>
    [ObservableProperty] private List<StartupScriptInfo> _startupScripts = [];

    /// <summary>
    /// 检测操作总耗时，单位为毫秒。
    /// </summary>
    [ObservableProperty] private long _elapsedMs;

    /// <summary>
    /// 检测过程中的错误信息。
    /// 检测成功时为 null，失败时包含异常描述。
    /// </summary>
    [ObservableProperty] private string? _errorMessage;

    /// <summary>
    /// 检测过程中的日志消息列表。
    /// 记录各阶段的检测进度与详情。
    /// </summary>
    [ObservableProperty] private List<string> _logMessages = [];
}
