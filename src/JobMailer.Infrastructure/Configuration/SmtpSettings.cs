namespace JobMailer.Infrastructure.Configuration;

public class SmtpSettings
{
    public const string SectionName = "SmtpSettings";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = "JobMailer";
    public string AppPassword { get; set; } = string.Empty;
    public string? GmailWebhookUrl { get; set; }
}
