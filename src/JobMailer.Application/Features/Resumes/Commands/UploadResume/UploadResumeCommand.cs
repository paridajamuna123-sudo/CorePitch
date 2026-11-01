using FluentValidation;
using JobMailer.Application.Common.Interfaces;
using JobMailer.Application.Features.Resumes.DTOs;
using JobMailer.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Resumes.Commands.UploadResume;

public record UploadResumeCommand(
    Stream FileStream,
    string OriginalFileName,
    string VersionName,
    bool IsDefault
) : IRequest<ResumeDocumentDto>;

public class UploadResumeCommandValidator : AbstractValidator<UploadResumeCommand>
{
    public UploadResumeCommandValidator()
    {
        RuleFor(x => x.VersionName).NotEmpty().WithMessage("Resume version name is required.");
        RuleFor(x => x.OriginalFileName).NotEmpty().Must(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only PDF resumes are supported.");
    }
}

public class UploadResumeCommandHandler : IRequestHandler<UploadResumeCommand, ResumeDocumentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IResumePdfParserService _pdfParser;

    public UploadResumeCommandHandler(
        IApplicationDbContext context, 
        IFileStorageService fileStorage,
        IResumePdfParserService pdfParser)
    {
        _context = context;
        _fileStorage = fileStorage;
        _pdfParser = pdfParser;
    }

    public async Task<ResumeDocumentDto> Handle(UploadResumeCommand request, CancellationToken cancellationToken)
    {
        var savedPath = await _fileStorage.SavePdfAsync(request.FileStream, request.OriginalFileName, cancellationToken);
        var fileBytes = await _fileStorage.GetPdfBytesAsync(savedPath, cancellationToken);

        // Automatically parse real candidate credentials from the PDF text
        var parsed = _pdfParser.ParsePdfText(fileBytes);
        var existingProfile = await _context.CandidateProfiles.FirstOrDefaultAsync(cancellationToken);
        if (existingProfile == null)
        {
            existingProfile = new CandidateProfile();
            _context.CandidateProfiles.Add(existingProfile);
        }

        if (!string.IsNullOrWhiteSpace(parsed.FullName) && parsed.FullName != "Candidate")
        {
            existingProfile.FullName = parsed.FullName;
        }
        if (!string.IsNullOrWhiteSpace(parsed.Title))
        {
            existingProfile.Title = parsed.Title;
        }
        if (parsed.YearsOfExperience > 0)
        {
            existingProfile.YearsOfExperience = parsed.YearsOfExperience;
        }
        if (!string.IsNullOrWhiteSpace(parsed.CoreSkillsCsv))
        {
            existingProfile.CoreSkillsCsv = parsed.CoreSkillsCsv;
        }
        if (!string.IsNullOrWhiteSpace(parsed.KeyAchievements))
        {
            existingProfile.KeyAchievements = parsed.KeyAchievements;
        }
        if (!string.IsNullOrWhiteSpace(parsed.LinkedInUrl))
        {
            existingProfile.LinkedInUrl = parsed.LinkedInUrl;
        }
        if (!string.IsNullOrWhiteSpace(parsed.GitHubUrl))
        {
            existingProfile.GitHubUrl = parsed.GitHubUrl;
        }
        existingProfile.UpdatedAtUtc = DateTime.UtcNow;

        if (request.IsDefault)
        {
            var existingDefaults = await _context.ResumeDocuments
                .Where(r => r.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var doc in existingDefaults)
            {
                doc.IsDefault = false;
            }
        }
        else
        {
            var hasAnyResume = await _context.ResumeDocuments.AnyAsync(cancellationToken);
            if (!hasAnyResume)
            {
                var resumeEntity = new ResumeDocument
                {
                    VersionName = request.VersionName.Trim(),
                    DisplayFileName = request.OriginalFileName,
                    StoragePath = savedPath,
                    IsDefault = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _context.ResumeDocuments.Add(resumeEntity);
                await _context.SaveChangesAsync(cancellationToken);

                return new ResumeDocumentDto
                {
                    Id = resumeEntity.Id,
                    VersionName = resumeEntity.VersionName,
                    DisplayFileName = resumeEntity.DisplayFileName,
                    StoragePath = resumeEntity.StoragePath,
                    IsDefault = resumeEntity.IsDefault,
                    CreatedAtUtc = resumeEntity.CreatedAtUtc
                };
            }
        }

        var resume = new ResumeDocument
        {
            VersionName = request.VersionName.Trim(),
            DisplayFileName = request.OriginalFileName,
            StoragePath = savedPath,
            IsDefault = request.IsDefault,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.ResumeDocuments.Add(resume);
        await _context.SaveChangesAsync(cancellationToken);

        return new ResumeDocumentDto
        {
            Id = resume.Id,
            VersionName = resume.VersionName,
            DisplayFileName = resume.DisplayFileName,
            StoragePath = resume.StoragePath,
            IsDefault = resume.IsDefault,
            CreatedAtUtc = resume.CreatedAtUtc
        };
    }
}
