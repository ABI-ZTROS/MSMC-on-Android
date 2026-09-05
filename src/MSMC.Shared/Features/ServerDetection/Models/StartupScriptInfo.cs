// -----------------------------------------------------------------------------
// 文件名: StartupScriptInfo.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Models
// 功能描述: 启动脚本信息数据契约，承载脚本解析与判定结果
// 依赖组件: CommunityToolkit.Mvvm
// 设计模式: 贫血模型 + ObservableObject 属性变更通知
// -----------------------------------------------------------------------------
using CommunityToolkit.Mvvm.ComponentModel;

namespace io.NET.ZTR_OS.Features.ServerDetection.Models;

/// <summary>
/// 启动脚本信息数据契约，封装被判定为 Minecraft 服务器启动脚本的文件元数据与解析结果。
/// 基于脚本内容架构特征进行判定，而非依赖文件名。
/// </summary>
public partial class StartupScriptInfo : ObservableObject
{
    /// <summary>
    /// 脚本文件的绝对路径。
    /// </summary>
    [ObservableProperty] private string _scriptPath = string.Empty;

    /// <summary>
    /// 脚本文件名（含扩展名）。
    /// </summary>
    [ObservableProperty] private string _scriptName = string.Empty;

    /// <summary>
    /// 指示该脚本是否被判定为服务器启动脚本。
    /// 基于内容特征规则匹配判定。
    /// </summary>
    [ObservableProperty] private bool _isServerStartupScript;

    /// <summary>
    /// 脚本中引用的 Java 可执行文件路径。
    /// 未识别到时为 null。
    /// </summary>
    [ObservableProperty] private string? _javaPath;

    /// <summary>
    /// 脚本中配置的最大堆内存大小，单位为字节。
    /// 对应 -Xmx 参数值，未识别到时为 0。
    /// </summary>
    [ObservableProperty] private long _maxHeapMemoryBytes;

    /// <summary>
    /// 脚本中引用的服务器核心 JAR 文件名。
    /// 未识别到时为 null。
    /// </summary>
    [ObservableProperty] private string? _serverJarName;

    /// <summary>
    /// 指示脚本是否包含自动重启逻辑。
    /// 基于循环结构或重启标志判定。
    /// </summary>
    [ObservableProperty] private bool _hasAutoRestart;

    /// <summary>
    /// 指示脚本是否使用 Aikar 推荐的 JVM 参数集。
    /// 基于 aikars.flags 标识判定。
    /// </summary>
    [ObservableProperty] private bool _usesAikarFlags;

    /// <summary>
    /// 脚本原始内容全文。
    /// 用于二次分析或 UI 展示。
    /// </summary>
    [ObservableProperty] private string _rawContent = string.Empty;

    /// <summary>
    /// 匹配成功的规则标识列表。
    /// 记录触发服务器启动脚本判定的具体规则名称。
    /// </summary>
    public List<string> MatchedRules { get; set; } = [];
}
