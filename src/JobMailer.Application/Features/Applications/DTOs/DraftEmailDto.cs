namespace JobMailer.Application.Features.Applications.DTOs;

public class DraftEmailDto
{
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public Guid ResumeDocumentId { get; set; }
    public string ResumeFileName { get; set; } = string.Empty;
    public bool IsDuplicateWarning { get; set; }
    public string? DuplicateWarningMessage { get; set; }
}
