using JobMailer.Application.Common.Interfaces;
using JobMailer.Application.Features.Applications.DTOs;
using JobMailer.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Applications.Commands.ExtractFromImage;

public record ExtractFromImageCommand(
    byte[] ImageBytes,
    string MimeType,
    byte[]? ResumePdfBytes = null,
    string? ResumeFileName = null,
    Guid? ResumeDocumentId = null
) : IRequest<ExtractedJobBannerDto>;

public class ExtractFromImageCommandHandler : IRequestHandler<ExtractFromImageCommand, ExtractedJobBannerDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IColdEmailAiService _aiService;
    private readonly IResumePdfParserService _pdfParser;
    private readonly IFileStorageService _fileStorage;

    public ExtractFromImageCommandHandler(
        IApplicationDbContext context, 
        IColdEmailAiService aiService,
        IResumePdfParserService pdfParser,
        IFileStorageService fileStorage)
    {
        _context = context;
        _aiService = aiService;
        _pdfParser = pdfParser;
        _fileStorage = fileStorage;
    }

    public async Task<ExtractedJobBannerDto> Handle(ExtractFromImageCommand request, CancellationToken cancellationToken)
    {
        // 1. If resume bytes are passed directly or via ResumeDocumentId, parse real candidate details
        byte[]? resumeBytes = request.ResumePdfBytes;
        Guid activeResumeId = request.ResumeDocumentId ?? Guid.Empty;
        string activeResumeName = request.ResumeFileName ?? "Resume.pdf";

        if ((resumeBytes == null || resumeBytes.Length == 0) && request.ResumeDocumentId.HasValue && request.ResumeDocumentId.Value != Guid.Empty)
        {
            var doc = await _context.ResumeDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.ResumeDocumentId.Value, cancellationToken);
            if (doc != null)
            {
                resumeBytes = await _fileStorage.GetPdfBytesAsync(doc.StoragePath, cancellationToken);
                activeResumeId = doc.Id;
                activeResumeName = doc.DisplayFileName;
            }
        }

        var profile = await _context.CandidateProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile == null)
        {
            profile = new CandidateProfile();
            _context.CandidateProfiles.Add(profile);
        }

        if (resumeBytes != null && resumeBytes.Length > 0)
        {
            var parsed = _pdfParser.ParsePdfText(resumeBytes);
            if (!string.IsNullOrWhiteSpace(parsed.FullName) && parsed.FullName != "Candidate")
            {
                profile.FullName = parsed.FullName;
            }
            if (!string.IsNullOrWhiteSpace(parsed.Title))
            {
                profile.Title = parsed.Title;
            }
            if (parsed.YearsOfExperience > 0)
            {
                profile.YearsOfExperience = parsed.YearsOfExperience;
            }
            if (!string.IsNullOrWhiteSpace(parsed.CoreSkillsCsv))
            {
                profile.CoreSkillsCsv = parsed.CoreSkillsCsv;
            }
            if (!string.IsNullOrWhiteSpace(parsed.KeyAchievements))
            {
                profile.KeyAchievements = parsed.KeyAchievements;
            }
            if (!string.IsNullOrWhiteSpace(parsed.LinkedInUrl))
            {
                profile.LinkedInUrl = parsed.LinkedInUrl;
            }
            if (!string.IsNullOrWhiteSpace(parsed.GitHubUrl))
            {
                profile.GitHubUrl = parsed.GitHubUrl;
            }
            profile.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        // If no active resume selected, try finding default
        if (activeResumeId == Guid.Empty)
        {
            var defaultDoc = await _context.ResumeDocuments
                .AsNoTracking()
                .Where(r => r.IsDefault)
                .FirstOrDefaultAsync(cancellationToken)
                ?? await _context.ResumeDocuments
                    .AsNoTracking()
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

            if (defaultDoc != null)
            {
                activeResumeId = defaultDoc.Id;
                activeResumeName = defaultDoc.DisplayFileName;
            }
        }

        var extracted = await _aiService.ExtractFromImageAndDraftAsync(
            profile,
            request.ImageBytes,
            request.MimeType,
            cancellationToken);

        // Check for duplicate application in last 60 days
        bool isDuplicate = false;
        string? duplicateMsg = null;

        if (!string.IsNullOrWhiteSpace(extracted.RecruiterEmail) || !string.IsNullOrWhiteSpace(extracted.CompanyName))
        {
            var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);
            var existingApp = await _context.JobApplications
                .AsNoTracking()
                .Where(a => a.CreatedAtUtc >= sixtyDaysAgo &&
                            ((!string.IsNullOrWhiteSpace(extracted.RecruiterEmail) && a.RecruiterEmail.ToLower() == extracted.RecruiterEmail.ToLower()) ||
                             (!string.IsNullOrWhiteSpace(extracted.CompanyName) && a.CompanyName.ToLower() == extracted.CompanyName.ToLower())))
                .FirstOrDefaultAsync(cancellationToken);

            if (existingApp != null)
            {
                isDuplicate = true;
                duplicateMsg = $"An application was already sent/recorded for '{existingApp.CompanyName}' ({existingApp.RecruiterEmail}) on {existingApp.CreatedAtUtc:yyyy-MM-dd} (within the last 60 days).";
            }
        }

        return new ExtractedJobBannerDto
        {
            CompanyName = extracted.CompanyName,
            RecruiterName = extracted.RecruiterName,
            RecruiterEmail = extracted.RecruiterEmail,
            SelectedJobTitle = extracted.SelectedJobTitle,
            AllDetectedRoles = extracted.AllDetectedRoles,
            ExperienceRequired = extracted.ExperienceRequired,
            ExtractedSkills = extracted.ExtractedSkills,
            Location = extracted.Location,
            SuggestedSubject = extracted.SuggestedSubject,
            SuggestedBodyHtml = extracted.SuggestedBodyHtml,
            IsDuplicateWarning = isDuplicate,
            DuplicateWarningMessage = duplicateMsg,
            DefaultResumeDocumentId = activeResumeId,
            DefaultResumeFileName = activeResumeName
        };
    }
}
