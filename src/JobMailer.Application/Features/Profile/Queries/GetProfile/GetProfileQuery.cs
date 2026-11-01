using JobMailer.Application.Common.Interfaces;
using JobMailer.Application.Features.Profile.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Profile.Queries.GetProfile;

public record GetProfileQuery : IRequest<CandidateProfileDto?>;

public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, CandidateProfileDto?>
{
    private readonly IApplicationDbContext _context;

    public GetProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CandidateProfileDto?> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _context.CandidateProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (profile == null) return null;

        return new CandidateProfileDto
        {
            Id = profile.Id,
            FullName = profile.FullName,
            Title = profile.Title,
            YearsOfExperience = profile.YearsOfExperience,
            CoreSkillsCsv = profile.CoreSkillsCsv,
            KeyAchievements = profile.KeyAchievements,
            LinkedInUrl = profile.LinkedInUrl,
            GitHubUrl = profile.GitHubUrl,
            PortfolioUrl = profile.PortfolioUrl,
            UpdatedAtUtc = profile.UpdatedAtUtc
        };
    }
}
