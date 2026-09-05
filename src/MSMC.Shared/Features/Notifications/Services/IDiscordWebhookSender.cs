// -----------------------------------------------------------------------------
// 文件名: IDiscordWebhookSender.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Services
// 功能描述: Discord Webhook 发送服务接口
// -----------------------------------------------------------------------------

namespace io.NET.ZTR_OS.Features.Notifications.Services;

public interface IDiscordWebhookSender
{
    Task<bool> SendEmbedAsync(string webhookUrl, EmbeddedMessage embed, CancellationToken ct = default);
    Task<bool> SendTextAsync(string webhookUrl, string message, CancellationToken ct = default);
}
