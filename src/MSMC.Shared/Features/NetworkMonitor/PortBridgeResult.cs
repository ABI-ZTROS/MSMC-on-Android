namespace io.NET.ZTR_OS.Features.NetworkMonitor.Models;

/// <summary>
/// 端口桥接操作结果码
/// </summary>
public enum PortBridgeErrorCode
{
    /// <summary>成功</summary>
    Success = 0,

    /// <summary>未知错误</summary>
    UnknownError = 1,

    /// <summary>权限不足（需管理员/UAC 提权）</summary>
    InsufficientPrivileges = 10,

    /// <summary>端口已被占用</summary>
    PortAlreadyInUse = 20,

    /// <summary>IP Helper 服务未启动（netsh portproxy 依赖）</summary>
    IpHelperServiceNotRunning = 30,

    /// <summary>防火墙规则添加/删除失败</summary>
    FirewallRuleFailed = 40,

    /// <summary>无效参数（地址格式、端口范围等）</summary>
    InvalidParameter = 50,

    /// <summary>命令执行超时</summary>
    CommandTimeout = 60,

    /// <summary>规则已存在（幂等场景可视为成功）</summary>
    RuleAlreadyExists = 70,

    /// <summary>规则不存在</summary>
    RuleNotFound = 80,

    /// <summary>操作系统不支持（非 Windows 等）</summary>
    PlatformNotSupported = 90,
}

/// <summary>
/// 端口桥接操作结果
/// </summary>
public class PortBridgeResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误码</summary>
    public PortBridgeErrorCode ErrorCode { get; set; }

    /// <summary>错误消息（详细描述）</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>用户可操作的建议</summary>
    public string Suggestion { get; set; } = string.Empty;

    /// <summary>使用的引擎（netsh / TcpForwarder）</summary>
    public string Engine { get; set; } = string.Empty;

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static PortBridgeResult Ok(string engine = "") =>
        new()
        {
            Success = true,
            ErrorCode = PortBridgeErrorCode.Success,
            Engine = engine
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static PortBridgeResult Fail(
        PortBridgeErrorCode errorCode,
        string errorMessage,
        string suggestion = "",
        string engine = "") =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Suggestion = suggestion,
            Engine = engine
        };
}
