// -----------------------------------------------------------------------------
// 文件名: EmailNotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Services
// 功能描述: 邮件通知服务 —— SMTP 发送邮件
// 设计模式: 三链原则 - 因果链：事件触发邮件发送；执行链：SMTP异常处理；返回链：收件人日志
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using io.NET.ZTR_OS.Features.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

/// <summary>
/// 邮件通知服务
/// </summary>
public class EmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly NotificationChannelConfig _config;

    public EmailNotificationService(ILogger<EmailNotificationService> logger, NotificationChannelConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// 发送邮件通知
    /// </summary>
    public async Task<bool> SendAsync(NotificationEvent evt, CancellationToken ct = default)
    {
        if (!_config.Email.Enabled || string.IsNullOrEmpty(_config.Email.SmtpHost))
        {
            _logger.LogDebug("[Email] Channel not enabled or SMTP not configured");
            return false;
        }

        _logger.LogInformation("[Email] Sending event {EventType} via SMTP to {To}", 
            evt.EventType, _config.Email.ToAddresses);

        try
        {
            using var client = CreateSmtpClient();
            
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_config.Email.FromAddress, "MSMC Notification"),
                Subject = $"[MSMC] {evt.Title} - {evt.EventType}",
                Body = BuildEmailBody(evt),
                IsBodyHtml = false,
                Priority = GetPriority(evt.EventType)
            };

            foreach (var toAddress in _config.Email.ToAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                mailMessage.To.Add(toAddress.Trim());
            }

            await client.SendMailAsync(mailMessage, ct);
            
            _logger.LogInformation("[Email] Sent successfully");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[Email] Send cancelled");
            return false;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "[Email] SMTP error: {StatusCode}", ex.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] Failed to send email");
            return false;
        }
    }

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_config.Email.SmtpHost, _config.Email.SmtpPort)
        {
            EnableSsl = _config.Email.UseTls,
            Timeout = 30000
        };

        if (!string.IsNullOrEmpty(_config.Email.Username))
        {
            client.Credentials = new NetworkCredential(_config.Email.Username, _config.Email.Password);
        }

        return client;
    }

    private string BuildEmailBody(NotificationEvent evt)
    {
        return $"MSMC Notification\r\n" +
               $"=====================\r\n\r\n" +
               $"Event Type: {evt.EventType}\r\n" +
               $"Title: {evt.Title}\r\n" +
               $"Message: {evt.Message}\r\n" +
               $"Source Module: {evt.SourceModule}\r\n" +
               $"Server ID: {evt.TargetServerId ?? "(N/A)"}\r\n" +
               $"Timestamp: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}\r\n\r\n" +
               $"This is an automated message from MSMC.\r\n" +
               $"Please do not reply to this email.";
    }

    private static MailPriority GetPriority(NotificationEventType type)
    {
        return type switch
        {
            NotificationEventType.ServerCrashed => MailPriority.High,
            NotificationEventType.BackupFailed => MailPriority.High,
            _ => MailPriority.Normal
        };
    }
}
