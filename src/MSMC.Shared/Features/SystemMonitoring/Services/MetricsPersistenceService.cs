// -----------------------------------------------------------------------------
// 文件名: MetricsPersistenceService.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Services
// 功能描述: 系统监控指标持久化服务 —— 将 CPU/内存使用率趋势数据以自定义二进制
//           格式（.msmcd）追加写入磁盘，支持按天加载、跨天切割与旧文件清理
// 依赖组件: System.IO, Serilog, io.NET.ZTR_OS.Models
// 设计模式: 仓储模式（时间序列持久化），仅追加写入（append-only）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

using System.IO;
using System.Text;
using io.NET.ZTR_OS.Features.SystemMonitoring.Models;
using Serilog;
using io.NET.ZTR_OS.Features.Startup.Services;

/// <summary>
/// 系统监控指标持久化服务实现 —— 自定义二进制格式（.msmcd）追加写入
/// </summary>
/// <remarks>
/// 二进制格式布局：
/// <list type="bullet">
/// <item>文件头 32 字节：魔数(4B) + 版本(2B) + 采样间隔秒(2B) + 记录数(4B) + 保留(20B)</item>
/// <item>每条记录 16 字节：Unix毫秒时间戳(8B) + CPU使用率(4B float) + 内存使用率(4B float)</item>
/// </list>
/// 写入为仅追加模式（O(1)），跨天时自动切割新文件。
/// </remarks>
public class MetricsPersistenceService : IMetricsPersistenceService
{
    /// <summary>数据文件根目录（%AppData%/io.NET.ZTR_OS/metrics/）</summary>
    private static readonly string MetricsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "io.NET.ZTR_OS", "metrics");

    /// <summary>文件扩展名</summary>
    private const string FileExtension = ".msmcd";

    /// <summary>文件头魔数："MSMC" 的 ASCII 字节</summary>
    private static readonly byte[] MagicBytes = [0x4D, 0x53, 0x4D, 0x43];

    /// <summary>文件头大小（字节）</summary>
    private const int HeaderSize = 32;

    /// <summary>每条记录大小（字节）</summary>
    private const int RecordSize = 16;

    /// <summary>文件格式版本</summary>
    private const ushort FormatVersion = 1;

    /// <summary>采样间隔（秒）</summary>
    private const ushort SampleIntervalSeconds = 2;

    /// <summary>当前打开的文件写入器</summary>
    private FileStream? _currentStream;

    /// <summary>当前文件对应的日期（用于跨天检测）</summary>
    private DateOnly _currentDate;

    /// <summary>当前文件的记录计数</summary>
    private uint _currentRecordCount;

    /// <summary>写入缓冲区（避免每次 Append 都触发系统调用）</summary>
    private readonly byte[] _writeBuffer = new byte[RecordSize];

    /// <summary>读写锁</summary>
    private readonly object _lock = new();

    /// <summary>时间服务（仅使用其公共辅助方法 ToUnixTimeMilliseconds / FromUnixTimeMilliseconds，
    /// 不依赖已移除的 NTP 偏移覆盖逻辑）</summary>
    private readonly TimeService _timeService;

    /// <summary>是否已释放</summary>
    private bool _disposed;

    public MetricsPersistenceService(TimeService timeService)
    {
        _timeService = timeService;
    }

    /// <summary>
    /// 追加一个监控数据点到当前日期的持久化文件
    /// </summary>
    public void Append(DateTime timestamp, double cpuUsagePercent, double memoryUsagePercent)
    {
        lock (_lock)
        {
            if (_disposed) return;

            try
            {
                var date = DateOnly.FromDateTime(timestamp);

                // 跨天切割：日期变更时关闭旧文件，打开新文件
                if (_currentStream == null || date != _currentDate)
                {
                    CloseCurrentFile();
                    OpenNewFile(date);
                }

                // 编码记录：8 字节时间戳 + 4 字节 CPU + 4 字节内存
                // v3: 使用系统本地时区正确转换为 UTC Unix 毫秒，替代硬编码 UTC+8 偏移
                //     避免非东八区环境或夏令时切换导致的时间漂移（因果链原则）
                var timestampMs = new DateTimeOffset(
                        DateTime.SpecifyKind(timestamp, DateTimeKind.Local))
                    .ToUnixTimeMilliseconds();
                var cpuFloat = (float)Math.Round(cpuUsagePercent, 2);
                var memFloat = (float)Math.Round(memoryUsagePercent, 2);

                // 手动写入 little-endian，避免 BinaryWriter 开销
                _writeBuffer[0] = (byte)(timestampMs >> 0);
                _writeBuffer[1] = (byte)(timestampMs >> 8);
                _writeBuffer[2] = (byte)(timestampMs >> 16);
                _writeBuffer[3] = (byte)(timestampMs >> 24);
                _writeBuffer[4] = (byte)(timestampMs >> 32);
                _writeBuffer[5] = (byte)(timestampMs >> 40);
                _writeBuffer[6] = (byte)(timestampMs >> 48);
                _writeBuffer[7] = (byte)(timestampMs >> 56);

                var cpuBits = BitConverter.SingleToInt32Bits(cpuFloat);
                _writeBuffer[8] = (byte)(cpuBits >> 0);
                _writeBuffer[9] = (byte)(cpuBits >> 8);
                _writeBuffer[10] = (byte)(cpuBits >> 16);
                _writeBuffer[11] = (byte)(cpuBits >> 24);

                var memBits = BitConverter.SingleToInt32Bits(memFloat);
                _writeBuffer[12] = (byte)(memBits >> 0);
                _writeBuffer[13] = (byte)(memBits >> 8);
                _writeBuffer[14] = (byte)(memBits >> 16);
                _writeBuffer[15] = (byte)(memBits >> 24);

                _currentStream!.Write(_writeBuffer, 0, RecordSize);
                _currentStream.Flush(flushToDisk: false); // 写入 OS 缓冲，不强制 fsync
                _currentRecordCount++;

                // 每 100 条记录更新一次文件头中的记录数
                if (_currentRecordCount % 100 == 0)
                {
                    UpdateRecordCount();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "追加监控数据点到持久化文件失败");
            }
        }
    }

    /// <summary>
    /// 加载指定日期的所有监控数据点
    /// </summary>
    public List<MetricsHistoryPoint> LoadDay(DateTime date)
    {
        var targetDate = DateOnly.FromDateTime(date);
        var filePath = GetFilePath(targetDate);

        if (!File.Exists(filePath))
            return [];

        try
        {
            return ReadFile(filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "读取日期 {Date} 的监控数据文件失败", targetDate);
            return [];
        }
    }

    /// <summary>
    /// 加载最近 N 天的监控数据点
    /// </summary>
    public List<MetricsHistoryPoint> LoadRecentDays(int days)
    {
        var result = new List<MetricsHistoryPoint>();
        // v2: 直接用 DateTime.Now（不再经过 NTP 偏移覆盖）
        var today = DateOnly.FromDateTime(DateTime.Now);

        for (int i = days - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayData = LoadDay(date.ToDateTime(TimeOnly.MinValue));
            result.AddRange(dayData);
        }

        return result;
    }

    /// <summary>
    /// 清理超过保留天数的旧数据文件
    /// </summary>
    public void CleanupOldFiles(int retainDays = 30)
    {
        try
        {
            if (!Directory.Exists(MetricsDir))
                return;

            // v2: 直接用 DateTime.Now（不再经过 NTP 偏移覆盖）
            var cutoff = DateOnly.FromDateTime(DateTime.Now.AddDays(-retainDays));
            var files = Directory.GetFiles(MetricsDir, $"*{FileExtension}");

            int deletedCount = 0;
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (DateOnly.TryParseExact(fileName, "yyyyMMdd", out var fileDate))
                {
                    if (fileDate < cutoff)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "删除旧监控数据文件失败: {File}", file);
                        }
                    }
                }
            }

            if (deletedCount > 0)
                Log.Information("[CLEAN] 已清理 {Count} 个过期监控数据文件（保留 {Days} 天）", deletedCount, retainDays);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "清理旧监控数据文件失败");
        }
    }

    /// <summary>
    /// 获取指定日期对应的文件路径
    /// </summary>
    private static string GetFilePath(DateOnly date) =>
        Path.Combine(MetricsDir, $"{date:yyyyMMdd}{FileExtension}");

    /// <summary>
    /// 打开指定日期的新文件，写入文件头
    /// </summary>
    private void OpenNewFile(DateOnly date)
    {
        Directory.CreateDirectory(MetricsDir);
        var filePath = GetFilePath(date);

        if (File.Exists(filePath))
        {
            // 文件已存在——追加模式，读取现有记录数
            _currentStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            _currentRecordCount = ReadRecordCount(_currentStream);
            _currentStream.Seek(0, SeekOrigin.End); // 移动到末尾准备追加
        }
        else
        {
            // 新文件——写入文件头
            _currentStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            WriteHeader(_currentStream);
            _currentRecordCount = 0;
        }

        _currentDate = date;
        Log.Debug("[FS] 打开监控数据文件: {File}，已有 {Count} 条记录", filePath, _currentRecordCount);
    }

    /// <summary>
    /// 关闭当前打开的文件，更新记录数
    /// </summary>
    private void CloseCurrentFile()
    {
        if (_currentStream != null)
        {
            try
            {
                // 确保记录数写入文件头
                UpdateRecordCount();
                _currentStream.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "关闭监控数据文件失败");
            }
            _currentStream = null;
        }
    }

    /// <summary>
    /// 写入文件头（32 字节）
    /// </summary>
    private static void WriteHeader(FileStream stream)
    {
        var header = new byte[HeaderSize];
        // 魔数
        MagicBytes.CopyTo(header, 0);
        // 版本 (little-endian)
        header[4] = (byte)(FormatVersion >> 0);
        header[5] = (byte)(FormatVersion >> 8);
        // 采样间隔秒 (little-endian)
        header[6] = (byte)(SampleIntervalSeconds >> 0);
        header[7] = (byte)(SampleIntervalSeconds >> 8);
        // 记录数初始为 0（[8..11] 已默认 0）
        // 保留区域 [12..31] 已默认 0

        stream.Write(header, 0, HeaderSize);
    }

    /// <summary>
    /// 从文件头读取记录数
    /// </summary>
    private static uint ReadRecordCount(FileStream stream)
    {
        if (stream.Length < HeaderSize)
            return 0;

        var buffer = new byte[4];
        stream.Position = 8; // 记录数字段偏移
        stream.ReadExactly(buffer, 0, 4);
        return (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
    }

    /// <summary>
    /// 更新文件头中的记录数
    /// </summary>
    private void UpdateRecordCount()
    {
        if (_currentStream == null || _currentStream.Length < HeaderSize)
            return;

        try
        {
            _currentStream.Position = 8;
            _currentStream.WriteByte((byte)(_currentRecordCount >> 0));
            _currentStream.WriteByte((byte)(_currentRecordCount >> 8));
            _currentStream.WriteByte((byte)(_currentRecordCount >> 16));
            _currentStream.WriteByte((byte)(_currentRecordCount >> 24));
            _currentStream.Position = _currentStream.Length; // 回到末尾
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "更新监控数据文件记录数失败");
        }
    }

    /// <summary>
    /// 从文件读取所有数据点
    /// </summary>
    private List<MetricsHistoryPoint> ReadFile(string filePath)
    {
        var result = new List<MetricsHistoryPoint>();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length < HeaderSize)
            return result;

        // 读取文件头，校验魔数
        var headerBuffer = new byte[HeaderSize];
        stream.ReadExactly(headerBuffer, 0, HeaderSize);

        if (headerBuffer[0] != MagicBytes[0] || headerBuffer[1] != MagicBytes[1] ||
            headerBuffer[2] != MagicBytes[2] || headerBuffer[3] != MagicBytes[3])
        {
            Log.Warning("监控数据文件魔数不匹配: {File}", filePath);
            return result;
        }

        var recordCount = (uint)(headerBuffer[8] | (headerBuffer[9] << 8) | (headerBuffer[10] << 16) | (headerBuffer[11] << 24));

        // 实际可读记录数取 min(文件头记录数, 文件物理大小可容纳的记录数)
        var maxPhysicalRecords = (stream.Length - HeaderSize) / RecordSize;
        var count = (int)Math.Min(recordCount, maxPhysicalRecords);

        var recordBuffer = new byte[RecordSize];

        for (int i = 0; i < count; i++)
        {
            stream.ReadExactly(recordBuffer, 0, RecordSize);

            var timestampMs = (long)(
                (uint)(recordBuffer[0] | (recordBuffer[1] << 8) | (recordBuffer[2] << 16) | (recordBuffer[3] << 24)) |
                ((long)(uint)(recordBuffer[4] | (recordBuffer[5] << 8) | (recordBuffer[6] << 16) | (recordBuffer[7] << 24)) << 32));

            var cpuBits = (int)(recordBuffer[8] | (recordBuffer[9] << 8) | (recordBuffer[10] << 16) | (recordBuffer[11] << 24));
            var memBits = (int)(recordBuffer[12] | (recordBuffer[13] << 8) | (recordBuffer[14] << 16) | (recordBuffer[15] << 24));

            // v3: 将 UTC Unix 毫秒转换为本地时区时间，替代硬编码 +8 小时偏移
            var timestamp = DateTimeOffset
                .FromUnixTimeMilliseconds(timestampMs)
                .LocalDateTime;
            var cpu = Math.Round(BitConverter.Int32BitsToSingle(cpuBits), 2);
            var mem = Math.Round(BitConverter.Int32BitsToSingle(memBits), 2);

            result.Add(new MetricsHistoryPoint(timestamp, cpu, mem));
        }

        return result;
    }

    /// <summary>
    /// 释放资源，关闭当前文件
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            CloseCurrentFile();
        }

        Log.Information("[CLEAN] MetricsPersistenceService 资源释放完成");
    }
}
