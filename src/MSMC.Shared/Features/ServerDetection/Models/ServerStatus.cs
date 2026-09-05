// -----------------------------------------------------------------------------
// 文件名: ServerStatus.cs
// 命名空间: io.NET.ZTR_OS.Features.ServerDetection.Models
// 功能描述: 服务器状态与操作类型枚举定义，构成服务器生命周期的状态机
// 依赖组件: 无
// 设计模式: 判别式联合枚举 + 状态机模型
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ServerDetection.Models;

/// <summary>
/// 服务器运行状态枚举，构成服务器生命周期的状态机判别式。
/// 用于驱动 UI 状态着色、按钮启用状态及文案展示。
/// </summary>
public enum ServerStatus
{
    /// <summary>
    /// 未知状态，尚未执行检测或检测结果不可用。
    /// </summary>
    Unknown,

    /// <summary>
    /// 运行中，服务器进程正常运行。
    /// </summary>
    Running,

    /// <summary>
    /// 启动中，正在执行服务器启动流程。
    /// 过渡状态，启动完成后转为 Running。
    /// </summary>
    Starting,

    /// <summary>
    /// 停止中，正在执行服务器停止流程。
    /// 过渡状态，停止完成后转为 Stopped。
    /// </summary>
    Stopping,

    /// <summary>
    /// 已停止，服务器进程未运行。
    /// </summary>
    Stopped,

    /// <summary>
    /// 异常状态，启动失败、进程意外退出或崩溃。
    /// </summary>
    Error
}

/// <summary>
/// 服务器操作类型枚举，表示当前正在执行的操作。
/// 作为功能互斥锁的判别式，任意时刻仅允许一个非 None 操作处于进行状态。
/// 用于并发控制与 UI 状态指示。
/// </summary>
public enum ServerOperation
{
    /// <summary>
    /// 空闲状态，无操作进行中。
    /// </summary>
    None,

    /// <summary>
    /// 正在执行服务器进程扫描检测。
    /// </summary>
    Detecting,

    /// <summary>
    /// 正在执行服务器导入操作。
    /// </summary>
    Importing,

    /// <summary>
    /// 正在执行服务器启动操作。
    /// </summary>
    Starting,

    /// <summary>
    /// 正在执行服务器停止操作。
    /// </summary>
    Stopping,

    /// <summary>
    /// 正在将配置保存至已知服务器列表。
    /// </summary>
    SavingConfig,

    /// <summary>
    /// 正在删除已知服务器条目。
    /// </summary>
    Deleting
}
