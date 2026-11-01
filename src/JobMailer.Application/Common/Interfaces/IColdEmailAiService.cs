using JobMailer.Domain.Entities;

namespace JobMailer.Application.Common.Interfaces;

public record GeneratedEmailResult(string Subject, string BodyHtml);

public record ExtractedJobBannerResult(
    string CompanyName,
    string? RecruiterName,
    string RecruiterEmail,
    string SelectedJobTitle,
    List<string> AllDetectedRoles,
    string? ExperienceRequired,
    string? ExtractedSkills,
    string? Location,
    string SuggestedSubject,
    string SuggestedBodyHtml
);

public interface IColdEmailAiService
{
    Task<GeneratedEmailResult> GenerateColdEmailAsync(
        CandidateProfile profile,
        string companyName,
        string? recruiterName,
        string jobTitle,
        string rawJd,
        CancellationToken ct = default);

    Task<ExtractedJobBannerResult> ExtractFromImageAndDraftAsync(
        CandidateProfile profile,
        byte[] imageBytes,
        string mimeType,
        CancellationToken ct = default);
}
