// -----------------------------------------------------------------------------
// 文件名: NotificationConfigService.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Services
// 功能描述: 通知配置持久化服务 —— JSON 文件读写，原子操作
// 设计模式: 三链原则 - 因果链：UI变更 → 触发保存；执行链：原子写入+备份；返回链：日志记录
// -----------------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using io.NET.ZTR_OS.Features.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

/// <summary>
/// 通知配置持久化服务接口
/// </summary>
public interface INotificationConfigService
{
    NotificationChannelConfig Load();
    void Save(NotificationChannelConfig config);
    Task SaveAsync(NotificationChannelConfig config, CancellationToken ct = default);
}

/// <summary>
/// 通知配置持久化服务
/// </summary>
public class NotificationConfigService : INotificationConfigService
{
    private readonly ILogger<NotificationConfigService> _logger;
    private readonly string _configPath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public NotificationConfigService(ILogger<NotificationConfigService> logger, string configPath)
    {
        _logger = logger;
        _configPath = configPath;
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    public NotificationChannelConfig Load()
    {
        _logger.LogInformation("[NotifCfg] Loading config from {Path}", _configPath);
        
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation("[NotifCfg] Config file not found, returning default");
                return new NotificationChannelConfig();
            }

            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<NotificationChannelConfig>(json, _jsonOptions);
            
            if (config == null)
            {
                _logger.LogWarning("[NotifCfg] Deserialization returned null, returning default");
                return new NotificationChannelConfig();
            }
            
            _logger.LogInformation("[NotifCfg] Config loaded successfully");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotifCfg] Failed to load config, returning default");
            return new NotificationChannelConfig();
        }
    }

    /// <summary>
    /// 保存配置（原子写入：先写临时文件再重命名）
    /// </summary>
    public void Save(NotificationChannelConfig config)
    {
        _logger.LogInformation("[NotifCfg] Saving config to {Path}", _configPath);
        
        try
        {
            // 确保目录存在
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            var tempPath = _configPath + ".tmp";
            
            // 先写临时文件
            File.WriteAllText(tempPath, json);
            
            // 原子替换（先删除旧文件，再移动新文件）
            if (File.Exists(_configPath))
            {
                File.Delete(_configPath);
            }
            File.Move(tempPath, _configPath);
            
            _logger.LogInformation("[NotifCfg] Config saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotifCfg] Failed to save config");
            throw;
        }
    }

    /// <summary>
    /// 异步保存配置
    /// </summary>
    public async Task SaveAsync(NotificationChannelConfig config, CancellationToken ct = default)
    {
        _logger.LogInformation("[NotifCfg] Async saving config...");
        
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            var tempPath = _configPath + ".tmp";
            
            await File.WriteAllTextAsync(tempPath, json, ct);
            
            if (File.Exists(_configPath))
            {
                File.Delete(_configPath);
            }
            File.Move(tempPath, _configPath);
            
            _logger.LogInformation("[NotifCfg] Config async saved successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[NotifCfg] Config save cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotifCfg] Failed to async save config");
            throw;
        }
    }
}
