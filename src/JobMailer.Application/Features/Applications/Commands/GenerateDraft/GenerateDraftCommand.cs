using FluentValidation;
using JobMailer.Application.Common.Interfaces;
using JobMailer.Application.Features.Applications.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Applications.Commands.GenerateDraft;

public record GenerateDraftCommand : IRequest<DraftEmailDto>
{
    public string CompanyName { get; init; } = string.Empty;
    public string? RecruiterName { get; init; }
    public string RecruiterEmail { get; init; } = string.Empty;
    public string JobTitle { get; init; } = string.Empty;
    public string RawJobDescription { get; init; } = string.Empty;
}

public class GenerateDraftCommandValidator : AbstractValidator<GenerateDraftCommand>
{
    public GenerateDraftCommandValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required.");
        RuleFor(x => x.RecruiterEmail).NotEmpty().EmailAddress().WithMessage("Valid recruiter email is required.");
        RuleFor(x => x.JobTitle).NotEmpty().WithMessage("Job title is required.");
        RuleFor(x => x.RawJobDescription).NotEmpty().WithMessage("Job description is required.");
    }
}

public class GenerateDraftCommandHandler : IRequestHandler<GenerateDraftCommand, DraftEmailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IColdEmailAiService _aiService;

    public GenerateDraftCommandHandler(IApplicationDbContext context, IColdEmailAiService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    public async Task<DraftEmailDto> Handle(GenerateDraftCommand request, CancellationToken cancellationToken)
    {
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);

        var existingApp = await _context.JobApplications
            .AsNoTracking()
            .Where(a => a.CreatedAtUtc >= sixtyDaysAgo &&
                        (a.RecruiterEmail.ToLower() == request.RecruiterEmail.ToLower() ||
                         a.CompanyName.ToLower() == request.CompanyName.ToLower()))
            .FirstOrDefaultAsync(cancellationToken);

        bool isDuplicate = existingApp != null;
        string? warningMessage = null;
        if (isDuplicate)
        {
            warningMessage = $"An application was already sent/recorded for '{existingApp!.CompanyName}' or '{existingApp.RecruiterEmail}' on {existingApp.CreatedAtUtc:yyyy-MM-dd} (within the last 60 days).";
        }

        var profile = await _context.CandidateProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (profile == null)
        {
            profile = new Domain.Entities.CandidateProfile
            {
                FullName = "Candidate",
                Title = "Software Engineer",
                YearsOfExperience = 5,
                CoreSkillsCsv = ".NET, C#, SQL Server, Angular",
                KeyAchievements = "Engineered enterprise scalable systems."
            };
        }

        var resume = await _context.ResumeDocuments
            .AsNoTracking()
            .Where(r => r.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        if (resume == null)
        {
            resume = await _context.ResumeDocuments
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var generated = await _aiService.GenerateColdEmailAsync(
            profile,
            request.CompanyName,
            request.RecruiterName,
            request.JobTitle,
            request.RawJobDescription,
            cancellationToken);

        return new DraftEmailDto
        {
            Subject = generated.Subject,
            BodyHtml = generated.BodyHtml,
            ResumeDocumentId = resume?.Id ?? Guid.Empty,
            ResumeFileName = resume?.DisplayFileName ?? "Resume.pdf",
            IsDuplicateWarning = isDuplicate,
            DuplicateWarningMessage = warningMessage
        };
    }
}
