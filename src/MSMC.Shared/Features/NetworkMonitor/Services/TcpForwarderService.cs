// -----------------------------------------------------------------------------
// 文件名: TcpForwarderService.cs
// 命名空间: io.NET.ZTR_OS.Features.NetworkMonitor.Services
// 功能描述: 用户态 TCP 转发引擎实现 —— 桥接系统默认引擎
//           基于 TcpListener + TcpClient + CopyToAsync 实现端口转发
// 依赖组件: Serilog, io.NET.ZTR_OS.Models
// 设计模式: 适配器模式（封装 .NET TcpListener/TcpClient 为转发引擎）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.NetworkMonitor.Services;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using io.NET.ZTR_OS.Features.NetworkMonitor.Models;
using Serilog;

/// <summary>
/// 用户态 TCP 转发引擎实现。用 .NET 原生 TcpListener 监听入站连接，
/// 每连接建立 TcpClient 到目标地址，双向 CopyToAsync 桥接流量。
/// </summary>
/// <remarks>
/// <para>规则增删即启停 listener，无 netsh/locale/iphlpsvc 依赖。</para>
/// <para>规则列表基于内部 ConcurrentDictionary，无需解析 netsh 文本输出。</para>
/// <para>每条规则独立后台 AcceptTcpClientAsync 循环，取消时停止 listener 并断开所有活动连接。</para>
/// </remarks>
public sealed class TcpForwarderService : ITcpForwarder, IDisposable
{
    private readonly ConcurrentDictionary<(string ListenAddress, int ListenPort, string Protocol), ForwardSession> _sessions = new();
    private readonly object _errorLock = new();
    private string _lastError = string.Empty;
    private bool _disposed;

    public string LastError
    {
        get { lock (_errorLock) return _lastError; }
        private set { lock (_errorLock) _lastError = value; }
    }

    public bool AddForward(PortBridgeRule rule)
    {
        var key = (rule.ListenAddress, rule.ListenPort, rule.Protocol);
        if (_sessions.ContainsKey(key))
        {
            LastError = $"转发规则已存在: {rule.ListenAddress}:{rule.ListenPort}";
            Log.Information("[RETRY] TcpForwarder 规则已存在，跳过: {Key}", key);
            return true; // 幂等
        }

        try
        {
            if (!IPAddress.TryParse(rule.ListenAddress, out var listenAddr))
            {
                LastError = $"监听地址无效: {rule.ListenAddress}";
                Log.Error("{Error}", LastError);
                return false;
            }

            if (!IPAddress.TryParse(rule.ConnectAddress, out var connectAddr))
            {
                LastError = $"目标地址无效: {rule.ConnectAddress}";
                Log.Error("{Error}", LastError);
                return false;
            }

            // v6tov6 协议下地址族必须匹配
            var expectedFamily = rule.Protocol == "v6tov6" ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
            if (listenAddr.AddressFamily != expectedFamily || connectAddr.AddressFamily != expectedFamily)
            {
                LastError = $"协议 {rule.Protocol} 与地址族不匹配（监听 {listenAddr.AddressFamily}，目标 {connectAddr.AddressFamily}）";
                Log.Error("{Error}", LastError);
                return false;
            }

            var listener = new TcpListener(listenAddr, rule.ListenPort);
            listener.Start();

            var cts = new CancellationTokenSource();
            var session = new ForwardSession(listener, cts, rule);
            _sessions[key] = session;

            // 启动 Accept 循环（后台 Task，不阻塞调用方）
            _ = Task.Run(() => AcceptLoopAsync(session, connectAddr, rule.ConnectPort, cts.Token));

            Log.Information("[OK] TcpForwarder 已启动: {Listen}:{LPort} -> {Connect}:{CPort}",
                rule.ListenAddress, rule.ListenPort, rule.ConnectAddress, rule.ConnectPort);
            return true;
        }
        catch (SocketException ex)
        {
            LastError = $"监听失败 (SocketErrorCode={ex.SocketErrorCode}): {ex.Message}";
            Log.Error(ex, "TcpForwarder 启动监听失败: {Listen}:{LPort}",
                rule.ListenAddress, rule.ListenPort);
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "TcpForwarder 启动异常");
            return false;
        }
    }

    public bool RemoveForward(string listenAddress, int listenPort, string protocol)
    {
        var key = (listenAddress, listenPort, protocol);
        if (!_sessions.TryRemove(key, out var session))
        {
            LastError = $"转发规则不存在: {listenAddress}:{listenPort}";
            return false;
        }

        try
        {
            session.Cts.Cancel();
            session.Listener.Stop();

            // 断开所有活动连接
            lock (session.ActiveClients)
            {
                foreach (var client in session.ActiveClients)
                {
                    try { client.Close(); } catch { }
                }
                session.ActiveClients.Clear();
            }

            session.Cts.Dispose();
            Log.Information("[STOP] TcpForwarder 已停止: {Listen}:{LPort}", listenAddress, listenPort);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "TcpForwarder 停止异常");
            return false;
        }
    }

    public List<PortBridgeRule> GetActiveForwards()
    {
        var rules = new List<PortBridgeRule>(_sessions.Count);
        foreach (var kv in _sessions)
        {
            rules.Add(new PortBridgeRule
            {
                ListenAddress = kv.Key.ListenAddress,
                ListenPort = kv.Key.ListenPort,
                Protocol = kv.Key.Protocol,
                ConnectAddress = kv.Value.Rule.ConnectAddress,
                ConnectPort = kv.Value.Rule.ConnectPort
            });
        }
        return rules;
    }

    public IReadOnlyDictionary<(string ListenAddress, int ListenPort), ForwardStats> GetForwardStats()
    {
        var stats = new Dictionary<(string, int), ForwardStats>(_sessions.Count);
        foreach (var kv in _sessions)
        {
            int activeCount;
            long totalBytes;
            lock (kv.Value.ActiveClients)
            {
                activeCount = kv.Value.ActiveClients.Count;
                totalBytes = kv.Value.TotalBytesRelayed;
            }
            stats[(kv.Key.ListenAddress, kv.Key.ListenPort)] = new ForwardStats(activeCount, totalBytes);
        }
        return stats;
    }

    /// <summary>
    /// 释放 TCP 转发引擎占用的所有资源
    /// </summary>
    /// <remarks>
    /// 遍历所有活动转发会话，逐一取消 Accept 循环、停止 TcpListener、断开活动连接、释放取消令牌。
    /// 幂等设计：重复调用安全。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Log.Information("[CLEAN] TcpForwarderService 释放资源中，共 {Count} 个活动会话", _sessions.Count);

        // 快照会话列表，避免在遍历时修改字典
        var sessions = _sessions.ToArray();
        _sessions.Clear();

        foreach (var kv in sessions)
        {
            var session = kv.Value;
            try
            {
                session.Cts.Cancel();
                session.Listener.Stop();

                lock (session.ActiveClients)
                {
                    foreach (var client in session.ActiveClients)
                    {
                        try { client.Close(); } catch { }
                    }
                    session.ActiveClients.Clear();
                }

                session.Cts.Dispose();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TcpForwarder 释放会话异常: {Key}", kv.Key);
            }
        }

        GC.SuppressFinalize(this);
        Log.Information("[OK] TcpForwarderService 资源释放完成");
    }

    private static async Task AcceptLoopAsync(ForwardSession session, IPAddress connectAddr, int connectPort, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await session.Listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TcpForwarder Accept 异常，监听已停止");
                break;
            }

            // 每个连接独立处理，不阻塞 Accept 循环；转发 ct 以便取消时中断所有派生任务
            _ = Task.Run(() => RelayConnectionAsync(session, client, connectAddr, connectPort, ct), ct);
        }
    }

    private static async Task RelayConnectionAsync(
        ForwardSession session,
        TcpClient client,
        IPAddress connectAddr,
        int connectPort,
        CancellationToken ct)
    {
        TcpClient? remote = null;
        try
        {
            lock (session.ActiveClients)
                session.ActiveClients.Add(client);

            remote = new TcpClient();
            await remote.ConnectAsync(connectAddr, connectPort, ct);

            var clientStream = client.GetStream();
            var remoteStream = remote.GetStream();

            // 双向 CopyToAsync，任一方向完成则取消另一个
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var c2r = CopyAndCountAsync(clientStream, remoteStream, session, linkedCts.Token);
            var r2c = CopyAndCountAsync(remoteStream, clientStream, session, linkedCts.Token);

            var finished = await Task.WhenAny(c2r, r2c);
            linkedCts.Cancel(); // 中断另一方向

            try { await finished; } catch { }
            try { await (finished == c2r ? r2c : c2r); } catch { }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "TcpForwarder 连接转发异常");
        }
        finally
        {
            lock (session.ActiveClients)
                session.ActiveClients.Remove(client);
            try { client.Close(); } catch { }
            try { remote?.Close(); } catch { }
        }
    }

    /// <summary>复制流并累加字节数到 session 统计。</summary>
    private static async Task CopyAndCountAsync(
        NetworkStream source,
        NetworkStream dest,
        ForwardSession session,
        CancellationToken ct)
    {
        var buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read), ct);
            lock (session.ActiveClients)
            {
                session.TotalBytesRelayed += read;
            }
        }
    }

    private sealed class ForwardSession
    {
        public TcpListener Listener { get; }
        public CancellationTokenSource Cts { get; }
        public PortBridgeRule Rule { get; }
        public List<TcpClient> ActiveClients { get; } = new();
        public long TotalBytesRelayed;

        public ForwardSession(TcpListener listener, CancellationTokenSource cts, PortBridgeRule rule)
        {
            Listener = listener;
            Cts = cts;
            Rule = rule;
        }
    }
}
