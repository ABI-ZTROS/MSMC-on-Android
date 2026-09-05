using System;

namespace io.NET.ZTR_OS.Features.NetworkMonitor.Models;

public class PortInfo : IEquatable<PortInfo>
{
    public int Port { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public bool IsOpen { get; set; }
    public PortRangeType PortRange { get; set; }
    public DateTime LastUpdated { get; set; }

    // 值相等性：基于端口/协议/PID/进程名比较，让 UpdateCollection 的智能跳过真正生效
    public bool Equals(PortInfo? other) =>
        other is not null
        && Port == other.Port
        && Protocol == other.Protocol
        && ProcessId == other.ProcessId
        && ProcessName == other.ProcessName;

    public override bool Equals(object? obj) => Equals(obj as PortInfo);

    public override int GetHashCode() => HashCode.Combine(Port, Protocol, ProcessId, ProcessName);
}

public enum PortRangeType
{
    System,
    Registered,
    Dynamic
}