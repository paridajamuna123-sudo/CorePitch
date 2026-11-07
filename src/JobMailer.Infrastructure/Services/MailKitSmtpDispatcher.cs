using System.Net.Http.Json;
using System.Text.Json;
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
    private static readonly HttpClient _httpClient = new HttpClient(new SmtpWebhookRedirectHandler())
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

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
        // Check for Google Apps Script Webhook URL (from settings or env var)
        var webhookUrl = !string.IsNullOrWhiteSpace(_settings.GmailWebhookUrl)
            ? _settings.GmailWebhookUrl
            : Environment.GetEnvironmentVariable("SmtpSettings__GmailWebhookUrl");

        if (!string.IsNullOrWhiteSpace(webhookUrl))
        {
            return await SendViaGmailWebhookAsync(
                webhookUrl,
                toEmail,
                toName,
                subject,
                htmlBody,
                attachmentBytes,
                attachmentDisplayFileName,
                ct);
        }

        // Fallback to standard SMTP dispatch (Port 587)
        return await SendViaSmtpAsync(
            toEmail,
            toName,
            subject,
            htmlBody,
            attachmentBytes,
            attachmentDisplayFileName,
            ct);
    }

    private async Task<string> SendViaGmailWebhookAsync(
        string webhookUrl,
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        byte[] attachmentBytes,
        string attachmentDisplayFileName,
        CancellationToken ct)
    {
        _logger.LogInformation("Dispatching cold email to {ToEmail} via HTTPS Google Apps Script Webhook...", toEmail);

        var payload = new
        {
            toEmail = toEmail,
            toName = toName,
            subject = subject,
            htmlBody = htmlBody,
            senderDisplayName = _settings.SenderDisplayName,
            attachmentFileName = string.IsNullOrWhiteSpace(attachmentDisplayFileName) ? "Resume.pdf" : attachmentDisplayFileName,
            attachmentBase64 = (attachmentBytes != null && attachmentBytes.Length > 0)
                ? Convert.ToBase64String(attachmentBytes)
                : string.Empty
        };

        var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("Webhook response ({StatusCode}): {Response}", response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Apps Script Webhook returned HTTP {response.StatusCode}: {responseContent}");
        }

        // Check for "success": true in JSON response
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("success", out var successElement) && !successElement.GetBoolean())
            {
                var errorMsg = doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : "Unknown webhook error";
                throw new InvalidOperationException($"Google Apps Script Webhook reported failure: {errorMsg}");
            }
        }
        catch (JsonException)
        {
            // If response is not valid JSON, check plain text contains success
            if (!responseContent.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase) &&
                !responseContent.Contains("\"success\": true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid webhook response: {responseContent}");
            }
        }

        var messageId = $"gas-webhook-{Guid.NewGuid():N}";
        _logger.LogInformation("Email successfully dispatched via HTTPS Webhook to {ToEmail}. Virtual MessageId: {MessageId}", toEmail, messageId);
        return messageId;
    }

    private async Task<string> SendViaSmtpAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        byte[] attachmentBytes,
        string attachmentDisplayFileName,
        CancellationToken ct)
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

        _logger.LogInformation("Email successfully dispatched via SMTP to {ToEmail}. MessageId: {MessageId}", toEmail, message.MessageId);

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

    // Custom HTTP Client Handler that automatically follows redirects on POST (handling 302 to GET/POST for Google Apps Script Web App redirects)
    private class SmtpWebhookRedirectHandler : HttpClientHandler
    {
        public SmtpWebhookRedirectHandler()
        {
            AllowAutoRedirect = true;
            MaxAutomaticRedirections = 10;
        }
    }
}
