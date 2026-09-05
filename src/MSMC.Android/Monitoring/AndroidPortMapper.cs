// -----------------------------------------------------------------------------
// 文件名: AndroidPortMapper.cs
// 命名空间: io.NET.ZTR_OS.Android.Monitoring
// 功能描述: Linux 端口映射器 —— 解析 /proc/net/[tcp|tcp6|udp|udp6] 与 /proc/[pid]/fd
//           建立「监听端口 → 占用进程 PID」映射，替代 Windows 版 GetExtendedTcpTable
// 设计模式: 快照模式（inode 对照表 + 进程 fd 遍历）
// -----------------------------------------------------------------------------
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using io.NET.ZTR_OS.Features.NetworkMonitor;

namespace io.NET.ZTR_OS.Android.Monitoring;

/// <summary>
/// Linux 端口映射器 —— 端口占用查询与进程归属
/// </summary>
public sealed partial class AndroidPortMapper
{
    private static readonly string[] NetFiles = ["/proc/net/tcp", "/proc/net/tcp6", "/proc/net/udp", "/proc/net/udp6"];

    /// <summary>
    /// 查询端口占用情况
    /// </summary>
    /// <param name="port">目标端口</param>
    /// <returns>占用该端口的进程信息（无占用返回 null）</returns>
    public PortProcessInfo? FindProcessByPort(int port)
    {
        foreach (var (ip, inode) in FindInodesByPort(port))
        {
            var pid = FindPidByInode(inode);
            if (pid is null) continue;

            var name = ProcessName(pid.Value);
            var cmdline = System.IO.File.Exists($"/proc/{pid}/cmdline") ? ReadCmdLine(pid.Value) : string.Empty;
            if (cmdline.Length > 200) cmdline = cmdline[..200];

            return new PortProcessInfo
            {
                Ip = ip,
                Inode = inode,
                ProcessId = pid.Value,
                ProcessName = name,
                CommandLine = cmdline,
            };
        }

        return null;
    }

    /// <summary>获取全部监听端口列表</summary>
    public List<PortInfo> GetListeningPorts()
    {
        var result = new List<PortInfo>();
        var inodeToEntry = new Dictionary<string, (string Address, string Protocol)>();

        foreach (var file in NetFiles)
        {
            var protocol = Path.GetFileName(file).Contains("udp", StringComparison.Ordinal) ? "UDP" : "TCP";
            try
            {
                foreach (var line in File.ReadLines(file).Skip(1))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 10) continue;
                    if (!parts[3].Equals("0A", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // 仅监听态
                    }

                    var (address, port) = ParseHexAddress(parts[1], file.Contains("6", StringComparison.Ordinal));
                    var inode = parts[9];

                    // 每端口只保留一条（优先 TCP）
                    if (!result.Any(p => p.Port == port))
                    {
                        result.Add(new PortInfo
                        {
                            Port = port,
                            Address = address,
                            Protocol = protocol,
                            ProcessId = null,
                            ProcessName = null,
                        });
                    }

                    inodeToEntry[inode] = (address, protocol);
                }
            }
            catch (IOException)
            {
                // 遇网络命名空间受限时跳过该表
            }
        }

        // 关联 PID
        var pidByInode = BuildInodePidMap();
        foreach (var info in result)
        {
            var entry = inodeToEntry.FirstOrDefault(kv => kv.Key is { Length: > 0 } && info.Port > 0 && kv.Value.Address == info.Address);
            // 简化关联：端口+地址匹配后再按 inode 找 pid
            var matched = inodeToEntry
                .Where(kv => kv.Value.Address == info.Address && kv.Value.Protocol.Equals(info.Protocol, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var inode in matched)
            {
                if (pidByInode.TryGetValue(inode, out var pid))
                {
                    info.ProcessId = pid;
                    info.ProcessName = ProcessName(pid);
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>查找指定端口对应的 socket inode（含本地地址）</summary>
    private static List<(string Ip, string Inode)> FindInodesByPort(int port)
    {
        var hexPort = port.ToString("X", CultureInfo.InvariantCulture);
        var matches = new List<(string, string)>();
        foreach (var file in NetFiles)
        {
            try
            {
                foreach (var line in File.ReadLines(file).Skip(1))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 10) continue;
                    var addrColon = parts[1].IndexOf(':');
                    if (addrColon <= 0) continue;
                    var localPort = parts[1][(addrColon + 1)..];
                    if (!localPort.Equals(hexPort, StringComparison.OrdinalIgnoreCase)) continue;

                    var (ip, _) = ParseHexAddress(parts[1], file.Contains("6", StringComparison.Ordinal));
                    matches.Add((ip, parts[9]));
                }
            }
            catch (IOException)
            {
                // 跳过不可读的表
            }
        }
        return matches;
    }

    /// <summary>遍历 /proc/[pid]/fd 构建 inode → pid 映射（带结果缓存）</summary>
    private Dictionary<string, int> BuildInodePidMap()
    {
        var map = new Dictionary<string, int>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories("/proc"))
            {
                var pidStr = Path.GetFileName(dir);
                if (!int.TryParse(pidStr, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)) continue;

                var fdDir = $"{dir}/fd";
                try
                {
                    foreach (var link in Directory.EnumerateFiles(fdDir))
                    {
                        var target = Path.GetFileName(link);
                        if (!int.TryParse(target, out _)) continue;
                        var real = new FileInfo(link).LinkTarget;
                        if (string.IsNullOrEmpty(real)) continue;

                        // 形如 socket:[123456]
                        if (real.StartsWith("socket:", StringComparison.Ordinal))
                        {
                            var inode = real["socket:[".Length..];
                            if (inode.EndsWith(']')) inode = inode[..^1];
                            map.TryAdd(inode, pid);
                        }
                    }
                }
                catch (IOException)
                {
                    // 进程退出或权限不足
                }
                catch (UnauthorizedAccessException)
                {
                    // 跳过
                }
            }
        }
        catch (IOException)
        {
            // /proc 不可读
        }
        return map;
    }

    private static int? FindPidByInode(string inode)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories("/proc"))
            {
                var pidStr = Path.GetFileName(dir);
                if (!int.TryParse(pidStr, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)) continue;
                var fdDir = $"{dir}/fd";
                try
                {
                    foreach (var link in Directory.EnumerateFiles(fdDir))
                    {
                        var real = new FileInfo(link).LinkTarget;
                        if (!string.IsNullOrEmpty(real) && real.Contains(inode, StringComparison.Ordinal))
                        {
                            return pid;
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        return null;
    }

    private static string ProcessName(int pid)
    {
        try
        {
            var line = File.ReadAllText($"/proc/{pid}/comm");
            return line.Trim();
        }
        catch (IOException)
        {
            return $"pid{pid}";
        }
    }

    private static string ReadCmdLine(int pid)
    {
        try
        {
            var bytes = File.ReadAllBytes($"/proc/{pid}/cmdline");
            var sb = new StringBuilder(bytes.Length);
            foreach (var b in bytes)
            {
                if (b == 0)
                {
                    if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                }
                else
                {
                    sb.Append((char)b);
                }
            }
            return sb.ToString().Trim();
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>解析十六进制地址串（如 0100007F:01BB 或 [ ::1 ]:01BB）</summary>
    private static (string Ip, int Port) ParseHexAddress(string addressPort, bool isV6)
    {
        var colon = addressPort.LastIndexOf(':');
        if (colon <= 0) return ("0.0.0.0", 0);

        var hexIp = addressPort[..colon];
        var hexPort = addressPort[(colon + 1)..];
        int.TryParse(hexPort, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var port);

        string ip;
        if (isV6)
        {
            ip = HexV6ToIp(hexIp);
        }
        else
        {
            ip = hexIp.Length == 8
                ? $"{byte.Parse(hexIp[6..8], NumberStyles.HexNumber)}.{byte.Parse(hexIp[4..6], NumberStyles.HexNumber)}.{byte.Parse(hexIp[2..4], NumberStyles.HexNumber)}.{byte.Parse(hexIp[0..2], NumberStyles.HexNumber)}"
                : "0.0.0.0";
        }

        return (ip, port);
    }

    private static string HexV6ToIp(string hex)
    {
        // /proc/net/tcp6 中 IPv4-mapped 地址形如 0000000000000000FFFF00000100007F
        if (hex.StartsWith("000000000000000000000000", StringComparison.Ordinal) && hex.Length >= 32)
        {
            return $"{byte.Parse(hex[30..32], NumberStyles.HexNumber)}.{byte.Parse(hex[28..30], NumberStyles.HexNumber)}.{byte.Parse(hex[26..28], NumberStyles.HexNumber)}.{byte.Parse(hex[24..26], NumberStyles.HexNumber)}";
        }

        var groups = new string[8];
        for (var i = 0; i < 8; i++)
        {
            groups[i] = hex.Substring(i * 4, 4).TrimStart('0');
            if (groups[i].Length == 0) groups[i] = "0";
        }
        return $"[{string.Join(':', groups)}]";
    }
}

/// <summary>端口占用进程信息</summary>
public sealed class PortProcessInfo
{
    public string Ip { get; set; } = string.Empty;
    public string Inode { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string CommandLine { get; set; } = string.Empty;
}

/// <summary>监听端口条目（前端 PortsResponse 契约）</summary>
public sealed class PortInfo
{
    public int Port { get; set; }
    public string Address { get; set; } = "0.0.0.0";
    public string Protocol { get; set; } = "TCP";
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}