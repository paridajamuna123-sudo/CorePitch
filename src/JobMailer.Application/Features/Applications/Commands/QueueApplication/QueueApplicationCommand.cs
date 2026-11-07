using FluentValidation;
using JobMailer.Application.Common.Interfaces;
using JobMailer.Domain.Entities;
using JobMailer.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly IEmailSmtpDispatcher _emailDispatcher;
    private readonly ILogger<QueueApplicationCommandHandler> _logger;

    public QueueApplicationCommandHandler(
        IApplicationDbContext context, 
        IFileStorageService fileStorage,
        IEmailSmtpDispatcher emailDispatcher,
        ILogger<QueueApplicationCommandHandler> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _emailDispatcher = emailDispatcher;
        _logger = logger;
    }

    public async Task<Guid> Handle(QueueApplicationCommand request, CancellationToken cancellationToken)
    {
        Guid resumeId;
        string resumeStoragePath;
        string resumeDisplayFileName;

        if (request.CustomResumeStream != null && !string.IsNullOrWhiteSpace(request.CustomResumeFileName))
        {
            resumeDisplayFileName = request.CustomResumeFileName;
            resumeStoragePath = await _fileStorage.SavePdfAsync(
                request.CustomResumeStream, 
                request.CustomResumeFileName, 
                cancellationToken);

            var newResume = new ResumeDocument
            {
                VersionName = $"Custom for {request.CompanyName}",
                DisplayFileName = resumeDisplayFileName,
                StoragePath = resumeStoragePath,
                IsDefault = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.ResumeDocuments.Add(newResume);
            await _context.SaveChangesAsync(cancellationToken);
            resumeId = newResume.Id;
        }
        else if (request.ResumeDocumentId.HasValue && request.ResumeDocumentId.Value != Guid.Empty)
        {
            var resume = await _context.ResumeDocuments
                .FirstOrDefaultAsync(r => r.Id == request.ResumeDocumentId.Value, cancellationToken);

            if (resume == null)
            {
                throw new KeyNotFoundException($"ResumeDocument with ID {request.ResumeDocumentId} does not exist.");
            }

            resumeId = resume.Id;
            resumeStoragePath = resume.StoragePath;
            resumeDisplayFileName = resume.DisplayFileName;
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

        // Immediate dispatch if not scheduled for a future time
        var now = DateTime.UtcNow;
        if (!request.ScheduledSendUtc.HasValue || request.ScheduledSendUtc.Value <= now)
        {
            try
            {
                _logger.LogInformation("Attempting immediate email dispatch for application {ApplicationId} to {Recipient}...", 
                    application.Id, application.RecruiterEmail);

                application.MarkAsSending();
                await _context.SaveChangesAsync(cancellationToken);

                byte[] resumeBytes = await _fileStorage.GetPdfBytesAsync(resumeStoragePath, cancellationToken);

                var messageId = await _emailDispatcher.SendColdEmailAsync(
                    toEmail: application.RecruiterEmail,
                    toName: application.RecruiterName,
                    subject: application.EmailSubject,
                    htmlBody: application.EmailBodyHtml,
                    attachmentBytes: resumeBytes,
                    attachmentDisplayFileName: resumeDisplayFileName,
                    ct: cancellationToken);

                application.MarkAsSent(messageId);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Application {ApplicationId} dispatched immediately. MessageId: {MessageId}", 
                    application.Id, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Immediate email dispatch failed for application {ApplicationId}. Marking as failed for retry/review.", application.Id);
                application.RecordFailure(ex.Message);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return application.Id;
    }
}
