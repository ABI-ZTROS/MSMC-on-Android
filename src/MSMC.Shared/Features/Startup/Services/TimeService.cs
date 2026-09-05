using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace io.NET.ZTR_OS.Features.Startup.Services;

/// <summary>
/// 时间服务（v2 拆分版）
/// 
/// 设计原则（对应故障复盘）：
///   1. 壁钟时间（文件名/时间戳） → 直接用 DateTime.Now / DateTimeOffset，
///      不再叠加 NTP 偏移（用户自己的系统时间 + Windows W32Time 已经够了）。
///   2. NTP 查询保留，但只做「偏差诊断」，超过阈值弹窗/日志警告用户。
///   3. 不再启动 1 小时重同步 Timer —— 避免后台 NTP 失败噪声。
///   
/// 缓存 TTL 等单调时钟场景统一走 Environment.TickCount64（由调用方直接用，不经过本类）。
/// </summary>
public class TimeService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<TimeService>();

    private static readonly string[] NtpServers =
    {
        "ntp.ntsc.ac.cn",
        "cn.ntp.org.cn",
        "ntp.aliyun.com",
        "time.windows.com",
    };

    private const int NtpPort = 123;
    private const int NtpTimeoutMs = 3000;

    /// <summary>
    /// NTP 偏差诊断阈值：超过 ±60 秒视为「系统时钟不准」，
    /// SynchronizeAsync 返回 false 并记录 Warning 日志。
    /// </summary>
    private static readonly TimeSpan LargeClockOffsetThreshold = TimeSpan.FromSeconds(60);

    /// <summary>NTP 偏移合理性上限：超过 ±1 天视为解析异常，直接丢弃该样本</summary>
    private const long MaxReasonableOffsetMs = 86_400_000L;

    private readonly object _lock = new();

    /// <summary>
    /// 最近一次诊断到的时钟偏差（仅供 UI / 日志显示；不会被叠加到 Now / NowUnixMilliseconds）
    /// </summary>
    private long _lastDiagnosedOffsetMs;
    private bool _isSynchronized;
    private DateTime _lastSyncTime = DateTime.MinValue;

    /// <summary>
    /// 最近一次 NTP 诊断是否成功完成（仅作信息展示，不影响任何时间返回值）
    /// </summary>
    public bool IsSynchronized
    {
        get { lock (_lock) return _isSynchronized; }
    }

    /// <summary>
    /// 最近一次 NTP 诊断到的时钟偏差（仅诊断用，不会被叠加到 Now）
    /// </summary>
    public TimeSpan ClockOffset
    {
        get { lock (_lock) return TimeSpan.FromMilliseconds(_lastDiagnosedOffsetMs); }
    }

    /// <summary>
    /// 壁钟时间：直接返回系统 DateTime.Now。
    /// 与 v1 的本质区别：**不再叠加 NTP 偏移**，避免天文数字偏移导致 1900 年文件名 / TTL 溢出。
    /// </summary>
    public DateTime Now => DateTime.Now;

    /// <summary>
    /// 壁钟 Unix 毫秒时间戳：直接使用 DateTimeOffset.UtcNow 计算，不经过 NTP 偏移。
    /// </summary>
    public long NowUnixMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// NTP 诊断完成事件（无论偏差是否超阈值均触发，不保证成功）
    /// </summary>
    public event EventHandler? SynchronizationCompleted;

    /// <summary>
    /// 启动一次 NTP 偏差诊断。
    ///   - 启动时后台调用（Task.Run），失败不阻塞启动；
    ///   - 诊断出的偏差只用于 ClockOffset / 日志 / 弹窗警告，**不修改 Now 返回值**；
    ///   - 超过 LargeClockOffsetThreshold (±60s) 返回 false 并打 Warning。
    /// </summary>
    public async Task<bool> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var offsets = new List<long>();
        var successful = 0;

        foreach (var server in NtpServers)
        {
            try
            {
                var offset = await QueryNtpOffsetAsync(server, cancellationToken);
                offsets.Add(offset);
                successful++;
                Log.Debug("NTP 服务器 {Server} 诊断偏移: {Offset}ms", server, offset);
            }
            catch (Exception ex)
            {
                // 校验失败（InvalidOperationException）只打 Debug 简洁消息，不打堆栈；
                // 网络超时等其他异常打完整 Warning
                if (ex is InvalidOperationException)
                    Log.Debug("NTP 服务器 {Server} 不可用：{Message}", server, ex.Message);
                else
                    Log.Warning(ex, "NTP 服务器 {Server} 查询失败（仅诊断，不影响时间）", server);
            }

            if (successful >= 2)
                break;
        }

        if (offsets.Count == 0)
        {
            Log.Information("NTP 时钟诊断跳过：所有服务器不可达或响应异常（不影响系统时间，直接使用本地时钟）");
            lock (_lock)
            {
                _isSynchronized = false;
                _lastDiagnosedOffsetMs = 0;
                _lastSyncTime = DateTime.Now;
            }
            OnSynchronizationCompleted();
            return false;
        }

        offsets.Sort();
        var medianOffset = offsets[offsets.Count / 2];

        lock (_lock)
        {
            // 只记录，不用于覆盖时间
            _lastDiagnosedOffsetMs = medianOffset;
            _isSynchronized = true;
            _lastSyncTime = DateTime.Now;
        }

        if (Math.Abs(medianOffset) > LargeClockOffsetThreshold.TotalMilliseconds)
        {
            Log.Warning(
                "[WARN] 系统时钟与 NTP 标准时间偏差较大: {Offset}ms（±{Threshold}s）。" +
                "请检查 Windows 日期/时间设置，或手动执行「立即同步」。" +
                "MSMC 已使用系统本地时间，不会被此偏差覆盖。",
                medianOffset, (int)LargeClockOffsetThreshold.TotalSeconds);
        }
        else
        {
            Log.Information("[TIME] NTP 时钟偏差诊断完成，偏差 {Offset}ms（成功 {Count} 个服务器，系统时钟正常）",
                medianOffset, offsets.Count);
        }

        OnSynchronizationCompleted();

        // 返回 true = 诊断成功且时钟偏差在阈值内；false 表示诊断成功但偏差超阈值
        return Math.Abs(medianOffset) <= LargeClockOffsetThreshold.TotalMilliseconds;
    }

    private static async Task<long> QueryNtpOffsetAsync(string server, CancellationToken cancellationToken)
    {
        var ntpData = new byte[48];
        ntpData[0] = 0x1B;

        using var udpClient = new UdpClient();
        udpClient.Client.ReceiveTimeout = NtpTimeoutMs;

        var sendTime = DateTime.UtcNow;
        await udpClient.SendAsync(ntpData, ntpData.Length, server, NtpPort);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(NtpTimeoutMs);

        var receiveResult = await udpClient.ReceiveAsync(cts.Token);
        var receiveTime = DateTime.UtcNow;

        var buffer = receiveResult.Buffer;

        // ── NTP 响应包合法性校验 ──
        // 国内网络环境下 UDP 123 经常被运营商劫持/拦截，返回畸形包；
        // 不校验的话会把垃圾数据解析成 1900 年时间戳，产生天文数字偏移。
        if (!TryValidateNtpResponse(buffer, server, out var validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        var transmitTimestamp = ParseNtpTimestamp(buffer, 40);

        // TransmitTimestamp 应该在 1900-01-01 之后合理的时间范围内；
        // 如果解析出来接近 1900 纪元起点，说明时间戳字段为空/损坏
        if (transmitTimestamp < new DateTime(1900, 1, 2, 0, 0, 0, DateTimeKind.Utc))
        {
            throw new InvalidOperationException(
                $"NTP TransmitTimestamp={transmitTimestamp:O} 异常（接近 1900 纪元起点），" +
                $"{server} 可能返回了畸形包");
        }

        var roundTrip = (receiveTime - sendTime).TotalMilliseconds;
        var offset = (transmitTimestamp - sendTime).TotalMilliseconds - roundTrip / 2;

        var offsetMs = (long)offset;
        if (Math.Abs(offsetMs) > MaxReasonableOffsetMs)
        {
            throw new InvalidOperationException(
                $"NTP 偏移 {offsetMs}ms 超出合理范围 (±{MaxReasonableOffsetMs}ms)");
        }

        return offsetMs;
    }

    /// <summary>
    /// 校验 NTP 响应包的合法性，过滤运营商劫持/防火墙返回的畸形包。
    /// </summary>
    /// <remarks>
    /// NTP 响应包格式（RFC 5905）：
    ///   byte[0]: LI(2bit) | VN(3bit) | Mode(3bit)，Server 响应的 Mode 必须为 4
    ///   byte[1]: Stratum（1=主参考源, 2-15=二级参考源, 0=未同步, 16=不可达）
    /// </remarks>
    private static bool TryValidateNtpResponse(byte[] buffer, string server, out string error)
    {
        if (buffer.Length < 48)
        {
            error = $"NTP 响应包长度不足（{buffer.Length} bytes，需要 48），{server} 可能被劫持";
            return false;
        }

        // byte[0]: LI(2bit) | VN(3bit) | Mode(3bit)
        // 合法的 NTP Server 响应 Mode 必须是 4
        var mode = buffer[0] & 0x07;
        if (mode != 4)
        {
            error = $"非 NTP 响应包（Mode={mode}，期望 4），{server} 可能被运营商劫持";
            return false;
        }

        // byte[1]: Stratum（1=主参考源, 2-15=二级参考源, 0=未同步, 16=不可达）
        var stratum = buffer[1];
        if (stratum == 0 || stratum >= 16)
        {
            error = $"NTP 服务器 {server} Stratum={stratum}（未同步或不可达）";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static DateTime ParseNtpTimestamp(byte[] buffer, int offset)
    {
        var seconds = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        var fraction = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset + 4));
        // 用浮点运算避免 uint * 1000 溢出（当前 NTP 秒数约 39.7 亿，接近 uint 上限）
        var milliseconds = seconds * 1000.0 + fraction * 1000.0 / 0x100000000L;
        return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(milliseconds);
    }

    private void OnSynchronizationCompleted()
    {
        SynchronizationCompleted?.Invoke(this, EventArgs.Empty);
    }

    // ──────────────────────────────────────────────────────────────
    // 以下辅助方法保留原有 API 签名，但内部实现不再依赖 NTP 偏移
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 将任意 DateTime 转换为北京时间（UTC+8）。
    /// 不涉及 NTP 偏移，仅做时区转换。
    /// </summary>
    public DateTime ToBeijingTime(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
            return dateTime.AddHours(8);

        if (dateTime.Kind == DateTimeKind.Local)
            return dateTime.ToUniversalTime().AddHours(8);

        return dateTime;
    }

    /// <summary>
    /// 将**北京时间**（UTC+8，无 DST）转换为 Unix 毫秒时间戳。
    /// 内部直接用 DateTimeOffset 构造，不经过 NTP 偏移。
    /// </summary>
    public long ToUnixTimeMilliseconds(DateTime beijingTime)
    {
        // 先把北京时间（UTC+8）转换为 UTC，再求距 Unix 纪元的毫秒数
        var utc = DateTime.SpecifyKind(beijingTime.AddHours(-8), DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Unix 毫秒时间戳 → 北京时间（UTC+8）。
    /// 内部直接用 DateTimeOffset，不经过 NTP 偏移。
    /// </summary>
    public DateTime FromUnixTimeMilliseconds(long unixMs)
    {
        return DateTimeOffset
            .FromUnixTimeMilliseconds(unixMs)
            .UtcDateTime
            .AddHours(8);
    }

    /// <summary>
    /// 今天的日期（北京时间，用 Now 直接取）。
    /// </summary>
    public DateOnly Today => DateOnly.FromDateTime(Now);
}
