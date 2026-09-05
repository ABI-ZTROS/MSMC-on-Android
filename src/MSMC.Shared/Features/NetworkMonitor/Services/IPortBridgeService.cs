using System.Collections.Generic;
using io.NET.ZTR_OS.Features.NetworkMonitor.Models;

namespace io.NET.ZTR_OS.Features.NetworkMonitor.Services;

public interface IPortBridgeService
{
    /// <summary>最近一次添加/删除操作失败的详细原因，供 UI 展示真实错误（线程安全）。</summary>
    string LastError { get; }

    bool AddBridgeRule(PortBridgeRule rule);

    /// <param name="protocol">协议（v4tov4/v6tov6/v4tov6/v6tov4），默认 v4tov4。</param>
    bool RemoveBridgeRule(string listenAddress, int listenPort, string protocol = "v4tov4");

    List<PortBridgeRule> GetAllBridgeRules();
    bool BridgeRuleExists(string listenAddress, int listenPort);

    /// <param name="protocol">防火墙协议（TCP/UDP），默认 TCP。</param>
    bool EnableFirewallRule(int listenPort, string protocol = "TCP");
    bool DisableFirewallRule(int listenPort);
}
