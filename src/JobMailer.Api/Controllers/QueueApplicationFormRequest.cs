namespace JobMailer.Api.Controllers;

public class QueueApplicationFormRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? RecruiterName { get; set; }
    public string RecruiterEmail { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string RawJobDescription { get; set; } = string.Empty;
    public string EmailSubject { get; set; } = string.Empty;
    public string EmailBodyHtml { get; set; } = string.Empty;
    public Guid? ResumeDocumentId { get; set; }
    public DateTime? ScheduledSendUtc { get; set; }
    public IFormFile? CustomResumePdf { get; set; }
}

public class ExtractImageFormRequest
{
    public required IFormFile ImageFile { get; set; }
    public IFormFile? ResumePdf { get; set; }
    public Guid? ResumeDocumentId { get; set; }
}
