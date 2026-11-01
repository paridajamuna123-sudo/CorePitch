using JobMailer.Application.Common.Interfaces;
using JobMailer.Application.Features.Resumes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Resumes.Queries.GetResumes;

public record GetResumesQuery : IRequest<List<ResumeDocumentDto>>;

public class GetResumesQueryHandler : IRequestHandler<GetResumesQuery, List<ResumeDocumentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetResumesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ResumeDocumentDto>> Handle(GetResumesQuery request, CancellationToken cancellationToken)
    {
        return await _context.ResumeDocuments
            .AsNoTracking()
            .OrderByDescending(r => r.IsDefault)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Select(r => new ResumeDocumentDto
            {
                Id = r.Id,
                VersionName = r.VersionName,
                DisplayFileName = r.DisplayFileName,
                StoragePath = r.StoragePath,
                IsDefault = r.IsDefault,
                CreatedAtUtc = r.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }
}
