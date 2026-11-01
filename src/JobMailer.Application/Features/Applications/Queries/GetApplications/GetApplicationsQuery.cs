using JobMailer.Application.Common.Interfaces;
using JobMailer.Application.Features.Applications.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Applications.Queries.GetApplications;

public record GetApplicationsQuery : IRequest<List<JobApplicationSummaryDto>>;

public class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, List<JobApplicationSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetApplicationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<JobApplicationSummaryDto>> Handle(GetApplicationsQuery request, CancellationToken cancellationToken)
    {
        return await _context.JobApplications
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new JobApplicationSummaryDto
            {
                Id = a.Id,
                CompanyName = a.CompanyName,
                RecruiterName = a.RecruiterName,
                RecruiterEmail = a.RecruiterEmail,
                JobTitle = a.JobTitle,
                EmailSubject = a.EmailSubject,
                Status = a.Status,
                ResumeDocumentId = a.ResumeDocumentId,
                ResumeFileName = a.ResumeDocument != null ? a.ResumeDocument.DisplayFileName : string.Empty,
                RetryCount = a.RetryCount,
                LastErrorMessage = a.LastErrorMessage,
                SmtpMessageId = a.SmtpMessageId,
                CreatedAtUtc = a.CreatedAtUtc,
                ScheduledSendUtc = a.ScheduledSendUtc,
                SentAtUtc = a.SentAtUtc
            })
            .ToListAsync(cancellationToken);
    }
}
