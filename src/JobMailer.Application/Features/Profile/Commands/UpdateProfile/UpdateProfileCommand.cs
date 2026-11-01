using FluentValidation;
using JobMailer.Application.Common.Interfaces;
using JobMailer.Application.Features.Profile.DTOs;
using JobMailer.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Profile.Commands.UpdateProfile;

public record UpdateProfileCommand(UpdateCandidateProfileDto Dto) : IRequest<CandidateProfileDto>;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.Dto.FullName).NotEmpty().WithMessage("Full name is required.");
        RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("Title is required.");
        RuleFor(x => x.Dto.YearsOfExperience).GreaterThanOrEqualTo(0).WithMessage("Years of experience must be 0 or more.");
    }
}

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, CandidateProfileDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateProfileCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CandidateProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.CandidateProfiles.FirstOrDefaultAsync(cancellationToken);

        if (profile == null)
        {
            profile = new CandidateProfile();
            _context.CandidateProfiles.Add(profile);
        }

        profile.FullName = request.Dto.FullName.Trim();
        profile.Title = request.Dto.Title.Trim();
        profile.YearsOfExperience = request.Dto.YearsOfExperience;
        profile.CoreSkillsCsv = request.Dto.CoreSkillsCsv;
        profile.KeyAchievements = request.Dto.KeyAchievements;
        profile.LinkedInUrl = request.Dto.LinkedInUrl;
        profile.GitHubUrl = request.Dto.GitHubUrl;
        profile.PortfolioUrl = request.Dto.PortfolioUrl;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

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
