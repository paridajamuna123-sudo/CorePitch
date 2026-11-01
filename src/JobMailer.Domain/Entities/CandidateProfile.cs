namespace JobMailer.Domain.Entities;

public class CandidateProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double YearsOfExperience { get; set; }
    public string CoreSkillsCsv { get; set; } = string.Empty;
    public string KeyAchievements { get; set; } = string.Empty;
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
