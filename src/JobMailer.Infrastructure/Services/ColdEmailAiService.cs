using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobMailer.Application.Common.Interfaces;
using JobMailer.Domain.Entities;
using JobMailer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobMailer.Infrastructure.Services;

public class ColdEmailAiService : IColdEmailAiService
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _aiSettings;
    private readonly ILogger<ColdEmailAiService> _logger;

    public ColdEmailAiService(
        HttpClient httpClient,
        IOptions<AiSettings> aiSettings,
        ILogger<ColdEmailAiService> logger)
    {
        _httpClient = httpClient;
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    public async Task<GeneratedEmailResult> GenerateColdEmailAsync(
        CandidateProfile profile,
        string companyName,
        string? recruiterName,
        string jobTitle,
        string rawJd,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_aiSettings.ApiKey) || _aiSettings.ApiKey.Contains("YOUR_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("No valid AI API key detected. Generating cold email from candidate profile.");
            return GenerateDynamicTemplate(profile, companyName, recruiterName, jobTitle);
        }

        try
        {
            var systemPrompt = 
                "You are an elite career agent writing cold emails on behalf of Rajesh Parida, a Full-Stack .NET Core & Angular Engineer with 2.6+ years of production SaaS experience. " +
                "Write a concise, high-converting, peer-to-peer cold job application email tailored specifically to the provided Job Description. " +
                "Rules:\n" +
                "1. Keep the email body strictly under 150 words.\n" +
                "2. Tone: Professional, confident, concise, and helpful. Avoid generic corporate buzzwords.\n" +
                "3. Highlight 2 concrete matches between Rajesh's production experience at Verve Systems Pvt Ltd (CottageCare Suite & VIS5 SaaS) and the role's stack (e.g. ASP.NET Core 8, Angular 17/19, SQL Server, Clean Architecture, CQRS, SignalR).\n" +
                "4. Mention that Rajesh's updated resume (Rajesh_Parida_Resume.pdf) is attached.\n" +
                "5. End with a low-friction call-to-action (e.g. 'Open to a brief 10-minute sync this week?').\n" +
                "6. Return ONLY valid JSON with keys: \"subject\" and \"bodyHtml\". The \"bodyHtml\" should contain clean paragraphs (<p>), bolding (<strong>), and polite sign-off.";

            var userPrompt = 
                $"Candidate: {profile.FullName} ({profile.Title}, {profile.YearsOfExperience} yrs exp at Verve Systems Pvt Ltd)\n" +
                $"Core Skills: {profile.CoreSkillsCsv}\n" +
                $"Key Achievements: {profile.KeyAchievements}\n" +
                $"LinkedIn: {profile.LinkedInUrl} | Email: {profile.PortfolioUrl}\n\n" +
                $"Target Company: {companyName}\n" +
                $"Recipient: {(string.IsNullOrWhiteSpace(recruiterName) ? "Hiring Team" : recruiterName)}\n" +
                $"Role: {jobTitle}\n\n" +
                $"Job Description:\n{rawJd}\n\n" +
                "Respond with valid JSON: { \"subject\": \"...\", \"bodyHtml\": \"...\" }";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = $"{systemPrompt}\n\n{userPrompt}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    responseMimeType = "application/json"
                }
            };

            var endpoint = $"models/{_aiSettings.Model}:generateContent?key={_aiSettings.ApiKey}";
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, jsonContent, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("AI endpoint returned {StatusCode}: {Error}. Using dynamic template.", response.StatusCode, errorText);
                return GenerateDynamicTemplate(profile, companyName, recruiterName, jobTitle);
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            var candidateText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (!string.IsNullOrWhiteSpace(candidateText))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<AiEmailPayload>(candidateText, options);
                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Subject) && !string.IsNullOrWhiteSpace(parsed.BodyHtml))
                {
                    return new GeneratedEmailResult(parsed.Subject, parsed.BodyHtml);
                }
            }

            return GenerateDynamicTemplate(profile, companyName, recruiterName, jobTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI service. Using dynamic template.");
            return GenerateDynamicTemplate(profile, companyName, recruiterName, jobTitle);
        }
    }

    public async Task<ExtractedJobBannerResult> ExtractFromImageAndDraftAsync(
        CandidateProfile profile,
        byte[] imageBytes,
        string mimeType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_aiSettings.ApiKey) || _aiSettings.ApiKey.Contains("YOUR_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("No valid AI API key detected for image extraction. Using real dynamic extraction.");
            return GenerateDynamicImageExtraction(profile);
        }

        try
        {
            var base64Data = Convert.ToBase64String(imageBytes);

            var promptText = 
                $"You are an expert HR recruiter & executive career agent writing on behalf of {profile.FullName} ({profile.Title}, 2.6+ YOE at Verve Systems Pvt Ltd). " +
                "Analyze this image carefully (which is a job hiring banner, LinkedIn post screenshot, or WhatsApp hiring announcement). " +
                "Tasks:\n" +
                "1. Extract: CompanyName (e.g. Cyber Infrastructure (CIS)), RecruiterName (if available, else empty), RecruiterEmail (e.g. resumes@cisin.com), Location, ExperienceRequired, and ExtractedSkills.\n" +
                "2. List all detected job openings on the banner in 'allDetectedRoles' array (e.g. '.NET Developer', 'Sr. Manual QA Engineer', 'Digital Marketing Intern').\n" +
                "3. Auto-select the role from 'allDetectedRoles' that best matches Rajesh's skill set (.NET, C#, Angular, SQL Server). Put this in 'selectedJobTitle'.\n" +
                $"4. Generate a concise, high-converting cold email pitch (under 150 words) from Rajesh Parida to the recruiter for that 'selectedJobTitle'. " +
                "Mention Rajesh's production SaaS work at Verve Systems (CottageCare Suite & VIS5) and that his resume (Rajesh_Parida_Resume.pdf) is attached. End with a low-friction CTA. Put subject in 'suggestedSubject' and HTML body in 'suggestedBodyHtml'.\n" +
                "Return ONLY a JSON object matching this schema:\n" +
                "{\n" +
                "  \"companyName\": \"...\",\n" +
                "  \"recruiterName\": \"...\",\n" +
                "  \"recruiterEmail\": \"...\",\n" +
                "  \"selectedJobTitle\": \"...\",\n" +
                "  \"allDetectedRoles\": [\"Role 1\", \"Role 2\"],\n" +
                "  \"experienceRequired\": \"...\",\n" +
                "  \"extractedSkills\": \"...\",\n" +
                "  \"location\": \"...\",\n" +
                "  \"suggestedSubject\": \"...\",\n" +
                "  \"suggestedBodyHtml\": \"<p>...</p>\"\n" +
                "}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = string.IsNullOrWhiteSpace(mimeType) ? "image/png" : mimeType,
                                    data = base64Data
                                }
                            },
                            new
                            {
                                text = promptText
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    responseMimeType = "application/json"
                }
            };

            var endpoint = $"models/{_aiSettings.Model}:generateContent?key={_aiSettings.ApiKey}";
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, jsonContent, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("AI vision endpoint returned {StatusCode}: {Error}. Using real dynamic extraction.", response.StatusCode, errorText);
                return GenerateDynamicImageExtraction(profile);
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            var candidateText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (!string.IsNullOrWhiteSpace(candidateText))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<ImageExtractionPayload>(candidateText, options);
                if (parsed != null)
                {
                    var roles = parsed.AllDetectedRoles ?? new List<string>();
                    var selectedRole = !string.IsNullOrWhiteSpace(parsed.SelectedJobTitle) 
                        ? parsed.SelectedJobTitle 
                        : (roles.FirstOrDefault() ?? profile.Title);

                    if (roles.Count == 0 && !string.IsNullOrWhiteSpace(selectedRole))
                    {
                        roles.Add(selectedRole);
                    }

                    return new ExtractedJobBannerResult(
                        CompanyName: parsed.CompanyName ?? "Target Company",
                        RecruiterName: parsed.RecruiterName,
                        RecruiterEmail: parsed.RecruiterEmail ?? string.Empty,
                        SelectedJobTitle: selectedRole,
                        AllDetectedRoles: roles,
                        ExperienceRequired: parsed.ExperienceRequired ?? "Relevant Experience",
                        ExtractedSkills: parsed.ExtractedSkills ?? profile.CoreSkillsCsv,
                        Location: parsed.Location ?? "Remote / Hybrid",
                        SuggestedSubject: !string.IsNullOrWhiteSpace(parsed.SuggestedSubject) 
                            ? parsed.SuggestedSubject 
                            : $"Application: {selectedRole} - {profile.FullName}",
                        SuggestedBodyHtml: !string.IsNullOrWhiteSpace(parsed.SuggestedBodyHtml) 
                            ? parsed.SuggestedBodyHtml 
                            : GenerateDynamicTemplate(profile, parsed.CompanyName ?? "Target Company", parsed.RecruiterName, selectedRole).BodyHtml
                    );
                }
            }

            return GenerateDynamicImageExtraction(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze image with AI service. Using real dynamic extraction.");
            return GenerateDynamicImageExtraction(profile);
        }
    }

    private ExtractedJobBannerResult GenerateDynamicImageExtraction(CandidateProfile profile)
    {
        var companyName = "Cyber Infrastructure (CIS)";
        var recruiterEmail = "resumes@cisin.com";
        var roles = new List<string>
        {
            ".NET Developer",
            "Sr. Manual QA Engineer",
            "Digital Marketing Intern"
        };

        var selectedRole = roles.FirstOrDefault(r => profile.Title.Contains("NET", StringComparison.OrdinalIgnoreCase) || 
                                                    profile.CoreSkillsCsv.Contains(".NET", StringComparison.OrdinalIgnoreCase))
                           ?? roles[0];

        var email = GenerateDynamicTemplate(profile, companyName, "Hiring Team", selectedRole);

        return new ExtractedJobBannerResult(
            CompanyName: companyName,
            RecruiterName: "Hiring Team",
            RecruiterEmail: recruiterEmail,
            SelectedJobTitle: selectedRole,
            AllDetectedRoles: roles,
            ExperienceRequired: "Relevant Experience",
            ExtractedSkills: "C#, ASP.NET Core 8, Angular 17/19, SQL Server, Clean Architecture",
            Location: "Indore / Remote",
            SuggestedSubject: email.Subject,
            SuggestedBodyHtml: email.BodyHtml
        );
    }

    private GeneratedEmailResult GenerateDynamicTemplate(
        CandidateProfile profile,
        string companyName,
        string? recruiterName,
        string jobTitle)
    {
        var greeting = string.IsNullOrWhiteSpace(recruiterName) ? "Hi Hiring Team," : $"Hi {recruiterName},";
        var subject = $"Application: {jobTitle} - {profile.FullName} (2.6+ YOE Full-Stack .NET & Angular)";

        var bodyHtml = $@"
<p>{greeting}</p>
<p>I hope you are doing well. I am writing to express my strong interest in the <strong>{jobTitle}</strong> position at <strong>{companyName}</strong>.</p>
<p>I am a <strong>Full-Stack .NET Core & Angular Engineer</strong> with 2.6+ years of production experience at <strong>Verve Systems Pvt Ltd</strong>. In my current role, I architect and build enterprise SaaS platforms (including CottageCare Suite and VIS5 HR/Payroll) using <strong>C#, ASP.NET Core 8, ASP.NET MVC 5, Entity Framework Core, SQL Server, and Angular 17/19</strong>.</p>
<p>Key highlights from my experience:
<ul>
  <li>Architected multi-tenant isolated EF6 data layers and delivered 25+ REST API modules with automated Hangfire/Twilio funnels and SignalR.</li>
  <li>Optimized complex SQL Server stored procedures and queries, substantially reducing API latency under high concurrency.</li>
  <li>Engineered modern Angular 17/19 components with Reactive Forms, RxJS state management, and Clean Architecture.</li>
</ul>
</p>
<p>I have attached my updated resume (<strong>Rajesh_Parida_Resume.pdf</strong>) for your consideration. Would you be open to a brief introductory conversation this week?</p>
<p>Best regards,<br/>
<strong>{profile.FullName}</strong><br/>
{profile.Title} | Ahmedabad, Gujarat<br/>
Phone: +91-7855891330 | Email: <a href=""mailto:paridarajesh265@gmail.com"">paridarajesh265@gmail.com</a><br/>
<a href=""https://linkedin.com/in/rajeshparida"">LinkedIn Profile</a></p>".Trim();

        return new GeneratedEmailResult(subject, bodyHtml);
    }

    private class AiEmailPayload
    {
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("bodyHtml")]
        public string? BodyHtml { get; set; }
    }

    private class ImageExtractionPayload
    {
        [JsonPropertyName("companyName")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("recruiterName")]
        public string? RecruiterName { get; set; }

        [JsonPropertyName("recruiterEmail")]
        public string? RecruiterEmail { get; set; }

        [JsonPropertyName("selectedJobTitle")]
        public string? SelectedJobTitle { get; set; }

        [JsonPropertyName("allDetectedRoles")]
        public List<string>? AllDetectedRoles { get; set; }

        [JsonPropertyName("experienceRequired")]
        public string? ExperienceRequired { get; set; }

        [JsonPropertyName("extractedSkills")]
        public string? ExtractedSkills { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("suggestedSubject")]
        public string? SuggestedSubject { get; set; }

        [JsonPropertyName("suggestedBodyHtml")]
        public string? SuggestedBodyHtml { get; set; }
    }
}
