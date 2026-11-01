using JobMailer.Application.Common.Interfaces;

namespace JobMailer.Application.Features.Applications.DTOs;

public class ExtractedJobBannerDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? RecruiterName { get; set; }
    public string RecruiterEmail { get; set; } = string.Empty;
    public string SelectedJobTitle { get; set; } = string.Empty;
    public List<string> AllDetectedRoles { get; set; } = new();
    public string? ExperienceRequired { get; set; }
    public string? ExtractedSkills { get; set; }
    public string? Location { get; set; }
    public string SuggestedSubject { get; set; } = string.Empty;
    public string SuggestedBodyHtml { get; set; } = string.Empty;
    public bool IsDuplicateWarning { get; set; }
    public string? DuplicateWarningMessage { get; set; }
    public Guid DefaultResumeDocumentId { get; set; }
    public string DefaultResumeFileName { get; set; } = string.Empty;
}
