using System.Text;
using System.Text.RegularExpressions;
using JobMailer.Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace JobMailer.Infrastructure.Services;

public class ResumePdfParserService : IResumePdfParserService
{
    public ParsedResumeResult ParsePdfText(byte[] pdfBytes)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            return new ParsedResumeResult(
                "Candidate",
                "Software Engineer",
                3.0,
                ".NET, C#, Angular",
                "Experienced software professional.",
                null, null, null, null, string.Empty);
        }

        var sb = new StringBuilder();
        try
        {
            using var stream = new MemoryStream(pdfBytes);
            using var document = PdfDocument.Open(stream);

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
        }
        catch
        {
            // If pdf text reading throws, return basic fallback
        }

        var fullText = sb.ToString();
        var lines = fullText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // 1. Extract Full Name: Usually the first non-trivial line
        string fullName = "Candidate";
        foreach (var line in lines.Take(5))
        {
            if (line.Length > 2 && line.Length < 50 && 
                !line.Contains("@") && 
                !line.Contains("http") && 
                !Regex.IsMatch(line, @"(?i)(resume|curriculum|vitae|page|contact|email|phone)"))
            {
                fullName = line;
                break;
            }
        }

        // 2. Extract Email
        string? email = null;
        var emailMatch = Regex.Match(fullText, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        if (emailMatch.Success)
        {
            email = emailMatch.Value;
        }

        // 3. Extract Links
        string? linkedIn = null;
        var linkedinMatch = Regex.Match(fullText, @"(?:https?:\/\/)?(?:www\.)?linkedin\.com\/in\/[a-zA-Z0-9_-]+");
        if (linkedinMatch.Success)
        {
            linkedIn = linkedinMatch.Value.StartsWith("http") ? linkedinMatch.Value : "https://" + linkedinMatch.Value;
        }

        string? github = null;
        var githubMatch = Regex.Match(fullText, @"(?:https?:\/\/)?(?:www\.)?github\.com\/[a-zA-Z0-9_-]+");
        if (githubMatch.Success)
        {
            github = githubMatch.Value.StartsWith("http") ? githubMatch.Value : "https://" + githubMatch.Value;
        }

        // 4. Extract Experience (years)
        double years = 4.0;
        var expMatch = Regex.Match(fullText, @"(\d+(?:\.\d+)?)\+?\s*(?:years|yrs)", RegexOptions.IgnoreCase);
        if (expMatch.Success && double.TryParse(expMatch.Groups[1].Value, out var parsedYears) && parsedYears < 40)
        {
            years = parsedYears;
        }

        // 5. Extract Title: check top 10 lines
        string title = "Software Engineer";
        var titleMatch = Regex.Match(fullText, @"(?i)(Full[- ]?Stack (?:Developer|Engineer)|\.NET (?:Developer|Engineer|Core)|Software Engineer|Frontend Developer|Backend Engineer|Angular Developer|Tech Lead|Senior Software Engineer)");
        if (titleMatch.Success)
        {
            title = titleMatch.Value;
        }

        // 6. Extract Skills
        var skillsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var commonSkills = new[]
        {
            ".NET", ".NET 8", ".NET Core", "C#", "ASP.NET Core Web API", "Entity Framework Core", "SQL Server",
            "Angular", "Angular 17", "Angular 18", "TypeScript", "JavaScript", "HTML5", "CSS3", "Tailwind CSS",
            "React", "Node.js", "Docker", "Kubernetes", "Azure", "AWS", "Git", "CQRS", "MediatR", "Microservices",
            "RESTful API", "RabbitMQ", "Kafka", "Redis", "MongoDB", "PostgreSQL", "CI/CD"
        };

        foreach (var skill in commonSkills)
        {
            if (Regex.IsMatch(fullText, $@"\b{Regex.Escape(skill)}\b", RegexOptions.IgnoreCase))
            {
                skillsSet.Add(skill);
            }
        }

        string coreSkillsCsv = skillsSet.Count > 0 
            ? string.Join(", ", skillsSet) 
            : ".NET, C#, ASP.NET Core, SQL Server, Angular";

        // 7. Key Achievements: capture strong sentences or bullet points
        var achievements = new List<string>();
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"(?i)(developed|built|engineered|architected|designed|implemented|optimized|led|delivered)\b") && line.Length > 30)
            {
                achievements.Add(line);
                if (achievements.Count >= 3) break;
            }
        }

        string keyAchievements = achievements.Count > 0 
            ? string.Join("; ", achievements) 
            : $"Delivered enterprise scalable solutions with {coreSkillsCsv}.";

        return new ParsedResumeResult(
            FullName: fullName,
            Title: title,
            YearsOfExperience: years,
            CoreSkillsCsv: coreSkillsCsv,
            KeyAchievements: keyAchievements,
            Email: email,
            LinkedInUrl: linkedIn,
            GitHubUrl: github,
            PortfolioUrl: null,
            RawText: fullText
        );
    }
}
