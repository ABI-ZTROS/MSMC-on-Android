// -----------------------------------------------------------------------------
// 文件名: DiscordWebhookSender.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Services
// 功能描述: Discord Webhook 发送器 —— 指数退避重试 + 429 速率限制处理
// 设计模式: 三链原则 - 执行链：重试/兜底；返回链：结构化日志
// -----------------------------------------------------------------------------

using System.Net.Http;
using System.Text;
using System.Text.Json;
using io.NET.ZTR_OS.Features.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

/// <summary>
/// Discord Webhook 发送服务
/// </summary>
public class DiscordWebhookSender : IDiscordWebhookSender
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            UserAgent = { new System.Net.Http.Headers.ProductInfoHeaderValue("MSMC", "1.0") }
        }
    };

    private readonly ILogger<DiscordWebhookSender> _logger;
    private readonly NotificationChannelConfig _config;

    public DiscordWebhookSender(ILogger<DiscordWebhookSender> logger, NotificationChannelConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// 发送嵌入消息到 Discord
    /// </summary>
    public async Task<bool> SendEmbedAsync(string webhookUrl, EmbeddedMessage embed, CancellationToken ct = default)
    {
        int maxAttempts = _config.RetryMaxAttempts;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var payload = new
                {
                    username = _config.Discord.BotName,
                    embeds = new[] { embed }
                };
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Discord] Webhook sent successfully (Attempt {Attempt})", attempt);
                    return true;
                }

                // 429 Too Many Requests — 尊重速率限制
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    int retryAfter = response.Headers.RetryAfter?.Delta.HasValue == true
                        ? (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds
                        : 5;
                    _logger.LogWarning("[Discord] Rate limited, waiting {Seconds}s...", retryAfter);
                    await Task.Delay(retryAfter * 1000, ct);
                    continue;
                }

                _logger.LogError("[Discord] Failed to send. Status: {StatusCode}, Reason: {Reason}",
                    (int)response.StatusCode, response.ReasonPhrase);
                return false;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                int delay = (int)Math.Pow(2, attempt) * _config.RetryBaseDelayMs;
                _logger.LogWarning(ex, "[Discord] HTTP failure (Attempt {Attempt}), retrying in {Delay}ms", attempt, delay);
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Discord] Send cancelled by user.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Discord] Unexpected error on attempt {Attempt}", attempt);
                return false;
            }
        }

        _logger.LogError("[Discord] Webhook failed after {MaxRetries} attempts", maxAttempts);
        return false;
    }

    /// <summary>
    /// 发送纯文本消息
    /// </summary>
    public async Task<bool> SendTextAsync(string webhookUrl, string message, CancellationToken ct = default)
    {
        var embed = new EmbeddedMessage
        {
            Description = message,
            Color = 0x3498db // MSMC theme blue
        };
        return await SendEmbedAsync(webhookUrl, embed, ct);
    }
}

/// <summary>
/// Discord 嵌入消息
/// </summary>
public class EmbeddedMessage
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int Color { get; set; } = 0x3498db;
    public List<EmbedField> Fields { get; set; } = new();
    public DateTimeOffset? Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Discord 嵌入字段
/// </summary>
public class EmbedField
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Inline { get; set; } = true;
}
