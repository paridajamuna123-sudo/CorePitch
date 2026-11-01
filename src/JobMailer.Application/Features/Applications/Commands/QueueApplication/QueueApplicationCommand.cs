using FluentValidation;
using JobMailer.Application.Common.Interfaces;
using JobMailer.Domain.Entities;
using JobMailer.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Applications.Commands.QueueApplication;

public record QueueApplicationCommand : IRequest<Guid>
{
    public string CompanyName { get; init; } = string.Empty;
    public string? RecruiterName { get; init; }
    public string RecruiterEmail { get; init; } = string.Empty;
    public string JobTitle { get; init; } = string.Empty;
    public string RawJobDescription { get; init; } = string.Empty;
    public string EmailSubject { get; init; } = string.Empty;
    public string EmailBodyHtml { get; init; } = string.Empty;
    public Guid? ResumeDocumentId { get; init; }
    public DateTime? ScheduledSendUtc { get; init; }

    // Direct custom resume upload support
    public Stream? CustomResumeStream { get; init; }
    public string? CustomResumeFileName { get; init; }
}

public class QueueApplicationCommandValidator : AbstractValidator<QueueApplicationCommand>
{
    public QueueApplicationCommandValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required.");
        RuleFor(x => x.RecruiterEmail).NotEmpty().EmailAddress().WithMessage("Valid recruiter email is required.");
        RuleFor(x => x.JobTitle).NotEmpty().WithMessage("Job title is required.");
        RuleFor(x => x.EmailSubject).NotEmpty().WithMessage("Email subject cannot be empty.");
        RuleFor(x => x.EmailBodyHtml).NotEmpty().WithMessage("Email body HTML cannot be empty.");
        RuleFor(x => x).Must(x => (x.ResumeDocumentId.HasValue && x.ResumeDocumentId.Value != Guid.Empty) || x.CustomResumeStream != null)
            .WithMessage("Either an existing ResumeDocumentId or a custom resume file must be provided.");
    }
}

public class QueueApplicationCommandHandler : IRequestHandler<QueueApplicationCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public QueueApplicationCommandHandler(IApplicationDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<Guid> Handle(QueueApplicationCommand request, CancellationToken cancellationToken)
    {
        Guid resumeId;

        if (request.CustomResumeStream != null && !string.IsNullOrWhiteSpace(request.CustomResumeFileName))
        {
            var storagePath = await _fileStorage.SavePdfAsync(
                request.CustomResumeStream, 
                request.CustomResumeFileName, 
                cancellationToken);

            var newResume = new ResumeDocument
            {
                VersionName = $"Custom for {request.CompanyName}",
                DisplayFileName = request.CustomResumeFileName,
                StoragePath = storagePath,
                IsDefault = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.ResumeDocuments.Add(newResume);
            await _context.SaveChangesAsync(cancellationToken);
            resumeId = newResume.Id;
        }
        else if (request.ResumeDocumentId.HasValue && request.ResumeDocumentId.Value != Guid.Empty)
        {
            var resumeExists = await _context.ResumeDocuments
                .AnyAsync(r => r.Id == request.ResumeDocumentId.Value, cancellationToken);

            if (!resumeExists)
            {
                throw new KeyNotFoundException($"ResumeDocument with ID {request.ResumeDocumentId} does not exist.");
            }

            resumeId = request.ResumeDocumentId.Value;
        }
        else
        {
            throw new ArgumentException("A valid ResumeDocumentId or custom resume file is required.");
        }

        var application = new JobApplication
        {
            CompanyName = request.CompanyName.Trim(),
            RecruiterName = request.RecruiterName?.Trim(),
            RecruiterEmail = request.RecruiterEmail.Trim(),
            JobTitle = request.JobTitle.Trim(),
            RawJobDescription = request.RawJobDescription,
            EmailSubject = request.EmailSubject.Trim(),
            EmailBodyHtml = request.EmailBodyHtml,
            ResumeDocumentId = resumeId,
            Status = ApplicationStatus.Queued,
            ScheduledSendUtc = request.ScheduledSendUtc,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.JobApplications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        return application.Id;
    }
}
