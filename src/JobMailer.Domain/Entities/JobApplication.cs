using JobMailer.Domain.Enums;

namespace JobMailer.Domain.Entities;

public class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = string.Empty;
    public string? RecruiterName { get; set; }
    public string RecruiterEmail { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string RawJobDescription { get; set; } = string.Empty;
    public string EmailSubject { get; set; } = string.Empty;
    public string EmailBodyHtml { get; set; } = string.Empty;
    public Guid ResumeDocumentId { get; set; }
    public ResumeDocument? ResumeDocument { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
    public int RetryCount { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? SmtpMessageId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledSendUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public void MarkAsSending()
    {
        Status = ApplicationStatus.Sending;
    }

    public void MarkAsSent(string messageId)
    {
        Status = ApplicationStatus.Sent;
        SmtpMessageId = messageId;
        SentAtUtc = DateTime.UtcNow;
        LastErrorMessage = null;
    }

    public void RecordFailure(string error, int maxRetries = 3)
    {
        RetryCount++;
        LastErrorMessage = error;
        if (RetryCount >= maxRetries)
        {
            Status = ApplicationStatus.Failed;
        }
        else
        {
            Status = ApplicationStatus.Queued;
        }
    }
}
