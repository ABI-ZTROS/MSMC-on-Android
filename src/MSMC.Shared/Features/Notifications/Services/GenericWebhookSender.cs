// -----------------------------------------------------------------------------
// 文件名: GenericWebhookSender.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Services
// 功能描述: 通用 Webhook 发送器 —— 支持自定义 URL、Header、Payload
// 设计模式: 三链原则 - 因果链：配置触发 HTTP POST；执行链：指数退避+429处理；返回链：状态码日志
// -----------------------------------------------------------------------------

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using io.NET.ZTR_OS.Features.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

/// <summary>
/// 通用 Webhook 发送服务
/// </summary>
public class GenericWebhookSender
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            UserAgent = { new System.Net.Http.Headers.ProductInfoHeaderValue("MSMC", "1.0") }
        }
    };

    private readonly ILogger<GenericWebhookSender> _logger;
    private readonly NotificationChannelConfig _config;

    public GenericWebhookSender(ILogger<GenericWebhookSender> logger, NotificationChannelConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// 发送通用 Webhook 通知
    /// </summary>
    public async Task<bool> SendAsync(NotificationEvent evt, CancellationToken ct = default)
    {
        if (!_config.GenericWebhook.Enabled || string.IsNullOrEmpty(_config.GenericWebhook.Url))
        {
            _logger.LogDebug("[GenericWebhook] Channel not enabled or URL not configured");
            return false;
        }

        _logger.LogInformation("[GenericWebhook] Sending event {EventType} to {Url}", 
            evt.EventType, _config.GenericWebhook.Url);

        int maxAttempts = _config.RetryMaxAttempts;
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var payload = BuildPayload(evt);
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // 添加自定义 Header
                if (!string.IsNullOrEmpty(_config.GenericWebhook.AuthorizationHeader))
                {
                    content.Headers.TryAddWithoutValidation("Authorization", _config.GenericWebhook.AuthorizationHeader);
                }

                var response = await _httpClient.PostAsync(_config.GenericWebhook.Url, content, ct);
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    int retryAfter = response.Headers.RetryAfter?.Delta.HasValue == true 
                        ? (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds 
                        : 5;
                    _logger.LogWarning("[GenericWebhook] Rate limited (429), waiting {RetryAfter}s (attempt {Attempt})", 
                        retryAfter, attempt);
                    await Task.Delay(retryAfter * 1000, ct);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[GenericWebhook] Sent successfully (attempt {Attempt})", attempt);
                    return true;
                }
                
                _logger.LogWarning("[GenericWebhook] Received {StatusCode} (attempt {Attempt})", 
                    response.StatusCode, attempt);
                    
                if (!IsRetryableStatus(response.StatusCode))
                {
                    break; // 4xx 非限流错误不重试
                }
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                int delay = (int)Math.Pow(2, attempt) * _config.RetryBaseDelayMs;
                _logger.LogWarning(ex, "[GenericWebhook] Request failed (attempt {Attempt}), retrying in {Delay}ms", 
                    attempt, delay);
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[GenericWebhook] Request cancelled");
                return false;
            }
        }

        _logger.LogError("[GenericWebhook] Failed to send after {MaxAttempts} attempts", maxAttempts);
        return false;
    }

    private object BuildPayload(NotificationEvent evt)
    {
        // 如果配置了自定义模板，可以在这里处理
        return new
        {
            eventType = evt.EventType.ToString(),
            title = evt.Title,
            message = evt.Message,
            sourceModule = evt.SourceModule,
            serverId = evt.TargetServerId,
            timestamp = DateTimeOffset.UtcNow,
            // 附加的上下文字段
            data = evt.EventType switch
            {
                NotificationEventType.ServerCrashed => new { severity = "critical" },
                NotificationEventType.ServerStarted => new { severity = "info" },
                NotificationEventType.BackupCompleted => new { severity = "success" },
                _ => new { severity = "" }
            }
        };
    }

    private static bool IsRetryableStatus(System.Net.HttpStatusCode status)
    {
        return status == System.Net.HttpStatusCode.InternalServerError
            || status == System.Net.HttpStatusCode.BadGateway
            || status == System.Net.HttpStatusCode.ServiceUnavailable
            || status == System.Net.HttpStatusCode.GatewayTimeout;
    }
}
