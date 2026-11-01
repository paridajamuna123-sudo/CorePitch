using System.Text.RegularExpressions;
using JobMailer.Application.Common.Interfaces;
using JobMailer.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace JobMailer.Infrastructure.Services;

public class MailKitSmtpDispatcher : IEmailSmtpDispatcher
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<MailKitSmtpDispatcher> _logger;

    public MailKitSmtpDispatcher(
        IOptions<SmtpSettings> settings,
        ILogger<MailKitSmtpDispatcher> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> SendColdEmailAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        byte[] attachmentBytes,
        string attachmentDisplayFileName,
        CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderDisplayName, _settings.SenderEmail));
        message.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = StripHtml(htmlBody)
        };

        if (attachmentBytes != null && attachmentBytes.Length > 0)
        {
            var fileName = string.IsNullOrWhiteSpace(attachmentDisplayFileName) ? "Resume.pdf" : attachmentDisplayFileName;
            builder.Attachments.Add(fileName, attachmentBytes, new ContentType("application", "pdf"));
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        _logger.LogInformation("Connecting to SMTP host {Host}:{Port}...", _settings.Host, _settings.Port);

        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, ct);

        if (!string.IsNullOrWhiteSpace(_settings.SenderEmail) && !string.IsNullOrWhiteSpace(_settings.AppPassword))
        {
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Email successfully dispatched to {ToEmail}. MessageId: {MessageId}", toEmail, message.MessageId);

        return message.MessageId ?? Guid.NewGuid().ToString();
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = Regex.Replace(html, "<[^>]*>", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
