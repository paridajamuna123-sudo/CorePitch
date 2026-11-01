namespace JobMailer.Domain.Entities;

public class ResumeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VersionName { get; set; } = string.Empty;
    public string DisplayFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
