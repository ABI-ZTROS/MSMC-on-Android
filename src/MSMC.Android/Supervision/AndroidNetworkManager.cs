// -----------------------------------------------------------------------------
// 文件名: AndroidNetworkManager.cs
// 命名空间: io.NET.ZTR_OS.Android.Supervision
// 功能描述: 网络管理 —— root 下 iptables 端口转发与防火墙规则
// -----------------------------------------------------------------------------
using io.NET.ZTR_OS.Android.Root;

namespace io.NET.ZTR_OS.Android.Supervision;

/// <summary>
/// Android 网络管理（root）—— iptables 转发 / 防火墙
/// </summary>
public sealed class AndroidNetworkManager
{
    /// <summary>添加端口转发规则（PREROUTING + 本机回环）</summary>
    public (bool Success, string Error) AddPortForward(int listenPort, string targetAddr, int targetPort)
    {
        try
        {
            // 本机回环（手机本机访问也生效）
            var loopback = $"iptables -t nat -A OUTPUT -p tcp --dport {listenPort} -j DNAT --to-destination {targetAddr}:{targetPort}";
            // 外部入站（内网其他设备）
            var prerouting = $"iptables -t nat -A PREROUTING -p tcp --dport {listenPort} -j DNAT --to-destination {targetAddr}:{targetPort}";
            var forward = "iptables -A FORWARD -j ACCEPT";

            var (_, err1, code1) = RootService.ExecWithCode(loopback);
            var (_, err2, code2) = RootService.ExecWithCode(prerouting);
            var (_, err3, code3) = RootService.ExecWithCode(forward);

            var ok = code1 == 0 && code2 == 0 && code3 == 0;
            return (ok, ok ? string.Empty : $"{err1} {err2} {err3}".Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>移除端口转发规则</summary>
    public (bool Success, string Error) RemovePortForward(int listenPort)
    {
        try
        {
            var loopback = $"iptables -t nat -D OUTPUT -p tcp --dport {listenPort} -j DNAT";
            var prerouting = $"iptables -t nat -D PREROUTING -p tcp --dport {listenPort} -j DNAT";

            _ = RootService.ExecWithCode(loopback);
            var (_, _, code) = RootService.ExecWithCode(prerouting);
            return (code == 0, code == 0 ? string.Empty : "移除转发失败");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>阻止入站某端口（防火墙）</summary>
    public (bool Success, string Error) BlockPort(int port)
    {
        try
        {
            var (_, _, code) = RootService.ExecWithCode($"iptables -A INPUT -p tcp --dport {port} -j DROP");
            return (code == 0, code == 0 ? string.Empty : "封禁端口失败");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>放行入站某端口</summary>
    public (bool Success, string Error) AllowPort(int port)
    {
        try
        {
            var (_, _, code) = RootService.ExecWithCode($"iptables -D INPUT -p tcp --dport {port} -j DROP 2>/dev/null; iptables -A INPUT -p tcp --dport {port} -j ACCEPT");
            return (code == 0, code == 0 ? string.Empty : "放行端口失败");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}