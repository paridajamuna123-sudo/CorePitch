namespace JobMailer.Application.Common.Interfaces;

public record ParsedResumeResult(
    string FullName,
    string Title,
    double YearsOfExperience,
    string CoreSkillsCsv,
    string KeyAchievements,
    string? Email,
    string? LinkedInUrl,
    string? GitHubUrl,
    string? PortfolioUrl,
    string RawText
);

public interface IResumePdfParserService
{
    ParsedResumeResult ParsePdfText(byte[] pdfBytes);
}
