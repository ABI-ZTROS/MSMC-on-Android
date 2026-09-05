// -----------------------------------------------------------------------------
// 文件名: ITcpForwarder.cs
// 命名空间: io.NET.ZTR_OS.Features.NetworkMonitor.Services
// 功能描述: 用户态 TCP 转发引擎接口 —— 桥接系统默认引擎
//           基于 TcpListener + TcpClient + CopyToAsync 实现端口转发，
//           完全托管代码、无 netsh/locale/iphlpsvc 依赖
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.NetworkMonitor.Services;

using io.NET.ZTR_OS.Features.NetworkMonitor.Models;

/// <summary>
/// 用户态 TCP 转发引擎接口。
/// </summary>
/// <remarks>
/// 作为 <see cref="IPortBridgeService"/> 的默认实现引擎，用 .NET 原生 TcpListener
/// 监听入站连接，建立到目标地址的 TcpClient，双向 CopyToAsync 桥接流量。
/// 相比 netsh portproxy 的优势：无 locale/iphlpsvc 依赖、规则增删即启停、
/// 可观测连接数与字节量、规则列表基于内部字典无需解析外部输出。
/// </remarks>
public interface ITcpForwarder
{
    /// <summary>最近一次操作失败的详细原因（线程安全）。</summary>
    string LastError { get; }

    /// <summary>
    /// 启动一条 TCP 转发规则。监听端口被占用或地址无效返回 false，<see cref="LastError"/> 含原因。
    /// </summary>
    bool AddForward(PortBridgeRule rule);

    /// <summary>
    /// 停止转发规则。规则不存在返回 false。protocol 仅用于 Key 匹配，转发本身总是 TCP。
    /// </summary>
    bool RemoveForward(string listenAddress, int listenPort, string protocol);

    /// <summary>当前活跃的转发规则列表（基于内部字典，无外部输出解析依赖）。</summary>
    List<PortBridgeRule> GetActiveForwards();

    /// <summary>每条规则的实时统计：活跃连接数、累计转发字节数。</summary>
    IReadOnlyDictionary<(string ListenAddress, int ListenPort), ForwardStats> GetForwardStats();
}

/// <summary>转发规则实时统计。</summary>
/// <param name="ActiveConnections">当前活跃的客户端连接数。</param>
/// <param name="TotalBytesRelayed">累计转发的字节数（双向之和）。</param>
public record ForwardStats(int ActiveConnections, long TotalBytesRelayed);
