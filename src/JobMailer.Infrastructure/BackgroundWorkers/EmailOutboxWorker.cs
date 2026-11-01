using JobMailer.Application.Common.Interfaces;
using JobMailer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobMailer.Infrastructure.BackgroundWorkers;

public class EmailOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailOutboxWorker> _logger;
    private readonly Random _random = new();

    public EmailOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailOutboxWorker started. Polling for queued emails...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedAnEmail = false;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var storageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                var smtpDispatcher = scope.ServiceProvider.GetRequiredService<IEmailSmtpDispatcher>();

                var now = DateTime.UtcNow;

                var candidateJob = await context.JobApplications
                    .Include(j => j.ResumeDocument)
                    .Where(j => j.Status == ApplicationStatus.Queued &&
                               (j.ScheduledSendUtc == null || j.ScheduledSendUtc <= now))
                    .OrderBy(j => j.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (candidateJob != null)
                {
                    _logger.LogInformation("Found queued application {JobId} for {Company} ({Recipient}). Processing...",
                        candidateJob.Id, candidateJob.CompanyName, candidateJob.RecruiterEmail);

                    candidateJob.MarkAsSending();
                    await context.SaveChangesAsync(stoppingToken);

                    try
                    {
                        byte[] resumeBytes = Array.Empty<byte>();
                        string displayFileName = "Resume.pdf";

                        if (candidateJob.ResumeDocument != null)
                        {
                            displayFileName = candidateJob.ResumeDocument.DisplayFileName;
                            resumeBytes = await storageService.GetPdfBytesAsync(
                                candidateJob.ResumeDocument.StoragePath, stoppingToken);
                        }

                        var messageId = await smtpDispatcher.SendColdEmailAsync(
                            toEmail: candidateJob.RecruiterEmail,
                            toName: candidateJob.RecruiterName,
                            subject: candidateJob.EmailSubject,
                            htmlBody: candidateJob.EmailBodyHtml,
                            attachmentBytes: resumeBytes,
                            attachmentDisplayFileName: displayFileName,
                            ct: stoppingToken);

                        candidateJob.MarkAsSent(messageId);
                        await context.SaveChangesAsync(stoppingToken);
                        processedAnEmail = true;

                        _logger.LogInformation("Application {JobId} sent successfully via SMTP message {MessageId}.",
                            candidateJob.Id, messageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send email for application {JobId}. Recording failure.", candidateJob.Id);
                        candidateJob.RecordFailure(ex.Message);
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in EmailOutboxWorker execution cycle.");
            }

            if (processedAnEmail)
            {
                // Anti-spam jitter delay between 45 and 90 seconds
                var jitterSeconds = _random.Next(45, 91);
                _logger.LogInformation("Anti-spam delay: waiting {Jitter} seconds before next outbox dispatch...", jitterSeconds);
                await Task.Delay(TimeSpan.FromSeconds(jitterSeconds), stoppingToken);
            }
            else
            {
                // Default polling interval: 15 seconds
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}
