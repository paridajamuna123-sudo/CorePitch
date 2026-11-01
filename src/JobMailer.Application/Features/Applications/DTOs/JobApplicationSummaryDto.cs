using JobMailer.Domain.Enums;

namespace JobMailer.Application.Features.Applications.DTOs;

public class JobApplicationSummaryDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? RecruiterName { get; set; }
    public string RecruiterEmail { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string EmailSubject { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public string StatusText => Status.ToString();
    public Guid ResumeDocumentId { get; set; }
    public string ResumeFileName { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? SmtpMessageId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ScheduledSendUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
