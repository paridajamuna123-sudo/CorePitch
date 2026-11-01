namespace JobMailer.Application.Common.Interfaces;

public interface IEmailSmtpDispatcher
{
    Task<string> SendColdEmailAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        byte[] attachmentBytes,
        string attachmentDisplayFileName,
        CancellationToken ct = default);
}
