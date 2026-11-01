namespace JobMailer.Infrastructure.Configuration;

public class AiSettings
{
    public const string SectionName = "AiSettings";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public string Model { get; set; } = "gemini-2.5-flash";
}
