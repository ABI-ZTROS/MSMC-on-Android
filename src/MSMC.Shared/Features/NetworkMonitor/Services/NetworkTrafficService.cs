namespace io.NET.ZTR_OS.Features.NetworkMonitor.Services;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

/// <summary>
/// 每日流量记录（按小时分桶）。
/// </summary>
public class DailyTrafficRecord
{
    public DateTime Date { get; set; }
    public long[] HourlyUpload { get; set; } = new long[24];
    public long[] HourlyDownload { get; set; } = new long[24];

    [JsonIgnore]
    public long TotalUpload => HourlyUpload.Sum();

    [JsonIgnore]
    public long TotalDownload => HourlyDownload.Sum();
}

/// <summary>
/// 网络流量监控服务。
/// 通过 System.Net.NetworkInformation 采样物理网卡收发字节数，
/// 计算实时上传/下载速度，按小时累积每日流量，持久化到 JSON 文件。
/// </summary>
/// <remarks>
/// <para>网卡过滤采用 NetworkInterfaceType 白名单 + 名称黑名单双重策略，
/// 排除 Hyper-V/WSL/Docker/VMware 等虚拟网卡，避免流量虚高。</para>
/// <para>用 Stopwatch 测量采样间隔，规避系统时钟回拨/夏令时跳变导致的负 delta 或假峰值。</para>
/// <para>持久化采用 临时文件 + File.Replace 原子写，避免写入中崩溃损坏 30 天历史。</para>
/// </remarks>
public class NetworkTrafficService : IDisposable
{
    private readonly string _dataFilePath;
    private readonly List<DailyTrafficRecord> _history = [];
    private readonly object _lock = new();

    private long _lastBytesSent;
    private long _lastBytesReceived;
    private string _lastSignature = string.Empty;
    private bool _firstSample = true;

    // Stopwatch 测量采样间隔，规避系统时钟回拨；readonly 不影响调用 Restart()
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private double _currentUploadSpeed;
    private double _currentDownloadSpeed;

    private DateTime _lastSaveTime;
    private readonly TimeSpan _saveInterval = TimeSpan.FromSeconds(60);

    // P1-002: 缓存活跃网卡列表，避免每秒全量枚举
    private NetworkInterface[]? _cachedInterfaces;
    private DateTime _interfaceCacheTime;
    private readonly TimeSpan _interfaceCacheInterval = TimeSpan.FromSeconds(30);
    private readonly object _interfaceCacheLock = new();

    /// <summary>物理网卡类型白名单。仅这些类型的网卡计入流量统计。</summary>
    private static readonly HashSet<NetworkInterfaceType> PhysicalInterfaceTypes = new()
    {
        NetworkInterfaceType.Ethernet,
        NetworkInterfaceType.Wireless80211,
        NetworkInterfaceType.GigabitEthernet,
        NetworkInterfaceType.FastEthernetFx,
        NetworkInterfaceType.FastEthernetT
    };

    /// <summary>虚拟网卡名称关键词黑名单。即便类型在白名单内（如 Hyper-V vEthernet 类型为 Ethernet），按名称二次排除。</summary>
    private static readonly string[] VirtualInterfaceNameMarkers =
        { "Hyper-V", "WSL", "Docker", "VMware", "VirtualBox", "vEthernet", "TAP", "Loopback Pseudo-Interface" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public double CurrentUploadSpeed => _currentUploadSpeed;
    public double CurrentDownloadSpeed => _currentDownloadSpeed;

    public DailyTrafficRecord TodayTraffic
    {
        get
        {
            lock (_lock)
            {
                return _history.FirstOrDefault(r => r.Date == DateTime.Today)
                    ?? new DailyTrafficRecord { Date = DateTime.Today };
            }
        }
    }

    public NetworkTrafficService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "io.NET.ZTR_OS");
        Directory.CreateDirectory(dir);
        _dataFilePath = Path.Combine(dir, "traffic.json");

        Load();
        EnsureTodayRecord();
    }

    private void EnsureTodayRecord()
    {
        if (!_history.Any(r => r.Date == DateTime.Today))
        {
            _history.Add(new DailyTrafficRecord { Date = DateTime.Today });
        }
    }

    /// <summary>
    /// 采样一次：读取物理网卡字节 → 计算速度 → 累积到当前小时桶。
    /// 应每秒调用一次。线程安全（加锁保护 _history）。
    /// </summary>
    /// <remarks>
    /// 用 Stopwatch 测量两次采样的真实间隔，避免 DateTime.Now 受系统时钟回拨影响。
    /// 网卡集合变化（切 Wi-Fi/插拔网卡）时重置基线并跳过本次 delta，避免基线不可比导致的假峰值或负 delta。
    /// 定期保存采用锁内取快照、锁外异步写盘的策略，避免持锁期间同步 I/O 阻塞其他采样线程。
    /// </remarks>
    public void Sample()
    {
        try
        {
            var (bytesSent, bytesReceived, signature) = GetTotalBytes();
            var now = DateTime.Now;

            // 锁内仅更新内存状态；若到保存时间则拷贝快照出锁外异步写盘
            List<DailyTrafficRecord>? snapshotToSave = null;
            lock (_lock)
            {
                // 首次采样或网卡集合变化：重置基线，跳过本次 delta 计算
                if (_firstSample || signature != _lastSignature)
                {
                    _lastBytesSent = bytesSent;
                    _lastBytesReceived = bytesReceived;
                    _lastSignature = signature;
                    _firstSample = false;
                    _stopwatch.Restart();
                    return;
                }

                var elapsed = _stopwatch.Elapsed.TotalSeconds;
                _stopwatch.Restart();
                if (elapsed < 0.1)
                    return;

                var sentDelta = bytesSent - _lastBytesSent;
                var receivedDelta = bytesReceived - _lastBytesReceived;

                // 网卡字节计数器可能溢出或重置，delta 为负时忽略
                if (sentDelta < 0) sentDelta = 0;
                if (receivedDelta < 0) receivedDelta = 0;

                _currentUploadSpeed = sentDelta / elapsed;
                _currentDownloadSpeed = receivedDelta / elapsed;

                // 累积到当前小时桶
                EnsureTodayRecord();
                var today = _history.First(r => r.Date == DateTime.Today);
                var hour = now.Hour;
                if (hour is >= 0 and < 24)
                {
                    today.HourlyUpload[hour] += sentDelta;
                    today.HourlyDownload[hour] += receivedDelta;
                }

                _lastBytesSent = bytesSent;
                _lastBytesReceived = bytesReceived;

                // 定期保存：锁内仅拷贝快照，实际 I/O 推迟到锁外异步执行
                if (now - _lastSaveTime > _saveInterval)
                {
                    snapshotToSave = _history.ToList();
                    _lastSaveTime = now;
                }
            }

            // 锁外异步写盘 —— 不阻塞后续 Sample 调用，避免持锁期间同步 I/O
            if (snapshotToSave is not null)
            {
                _ = Task.Run(() => SaveSnapshot(snapshotToSave));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "网络流量采样失败");
        }
    }

    /// <summary>
    /// 将快照写入磁盘（原子写）—— 在锁外执行，不持有 <see cref="_lock"/>。
    /// </summary>
    /// <param name="snapshot">已拷贝的历史快照，调用方负责在锁内生成</param>
    private void SaveSnapshot(List<DailyTrafficRecord> snapshot)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot, JsonOpts);
            var tmpPath = _dataFilePath + ".tmp";

            // 先写临时文件
            File.WriteAllText(tmpPath, json);

            // 原子替换：目标存在用 File.Replace，不存在（首次写入）用 File.Move
            if (File.Exists(_dataFilePath))
                File.Replace(tmpPath, _dataFilePath, destinationBackupFileName: null);
            else
                File.Move(tmpPath, _dataFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "保存流量数据失败");
        }
    }

    /// <summary>
    /// 获取今日流量记录（含 24 小时数据）。线程安全。
    /// </summary>
    public DailyTrafficRecord GetTodayTraffic()
    {
        lock (_lock)
        {
            EnsureTodayRecord();
            return _history.First(r => r.Date == DateTime.Today);
        }
    }

    /// <summary>
    /// 获取最近 N 天的流量记录。线程安全。
    /// </summary>
    public List<DailyTrafficRecord> GetRecentDays(int days)
    {
        lock (_lock)
        {
            var cutoff = DateTime.Today.AddDays(-days + 1);
            return _history.Where(r => r.Date >= cutoff).OrderBy(r => r.Date).ToList();
        }
    }

    /// <summary>
    /// 获取活跃物理网卡的总收发字节数及参与统计的网卡签名。
    /// P1-002: 缓存网卡列表 30 秒，避免每秒全量枚举 GetAllNetworkInterfaces。
    /// </summary>
    /// <returns>(发送字节, 接收字节, 网卡 Id 集合签名)。签名用于检测网卡集合变化触发基线重置。</returns>
    private (long sent, long received, string signature) GetTotalBytes()
    {
        long totalSent = 0;
        long totalReceived = 0;
        var ids = new List<string>();

        var interfaces = GetActiveInterfaces();

        foreach (var ni in interfaces)
        {
            try
            {
                var stats = ni.GetIPv4Statistics();
                totalSent += stats.BytesSent;
                totalReceived += stats.BytesReceived;
                ids.Add(ni.Id);
            }
            catch
            {
                // 部分虚拟网卡可能抛异常，忽略
            }
        }

        // 网卡 Id 排序后 Join 为签名，集合变化（增删网卡）时签名不同
        ids.Sort(StringComparer.Ordinal);
        var signature = string.Join("|", ids);

        return (totalSent, totalReceived, signature);
    }

    /// <summary>
    /// 获取活跃物理网卡列表（带缓存，线程安全）。
    /// </summary>
    /// <remarks>
    /// 过滤策略：NetworkInterfaceType 白名单 + 名称关键词黑名单双重过滤，
    /// 排除 Hyper-V/WSL/Docker/VMware 等虚拟网卡，避免流量虚高 5-10 倍。
    /// </remarks>
    private NetworkInterface[] GetActiveInterfaces()
    {
        lock (_interfaceCacheLock)
        {
            // 缓存有效则直接返回
            if (_cachedInterfaces is not null
                && DateTime.Now - _interfaceCacheTime < _interfaceCacheInterval)
            {
                return _cachedInterfaces;
            }

            // 重新枚举：仅保留 Up 状态的物理网卡
            _cachedInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && IsPhysicalInterface(ni))
                .ToArray();
            _interfaceCacheTime = DateTime.Now;

            return _cachedInterfaces;
        }
    }

    /// <summary>
    /// 判断网卡是否为物理网卡：类型在白名单内且名称不含虚拟网卡关键词。
    /// </summary>
    private static bool IsPhysicalInterface(NetworkInterface ni)
    {
        if (!PhysicalInterfaceTypes.Contains(ni.NetworkInterfaceType))
            return false;

        var name = ni.Name;
        foreach (var marker in VirtualInterfaceNameMarkers)
        {
            if (name.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_dataFilePath))
                return;

            var json = File.ReadAllText(_dataFilePath);
            var records = JsonSerializer.Deserialize<List<DailyTrafficRecord>>(json, JsonOpts);
            if (records is not null)
            {
                // 保留最近 30 天
                var cutoff = DateTime.Today.AddDays(-30);
                _history.AddRange(records.Where(r => r.Date >= cutoff));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载流量历史数据失败");
        }
    }

    /// <summary>
    /// 原子写持久化：锁内拷贝快照，锁外执行原子写。
    /// 避免写入中崩溃导致 30 天历史损坏。
    /// </summary>
    /// <remarks>
    /// 公共入口供 Dispose/外部主动保存调用；周期性保存由 <see cref="Sample"/> 锁外异步触发。
    /// </remarks>
    public void Save()
    {
        List<DailyTrafficRecord> snapshot;
        lock (_lock)
        {
            snapshot = _history.ToList();
        }
        SaveSnapshot(snapshot);
    }

    public void Dispose()
    {
        Save();
    }
}
