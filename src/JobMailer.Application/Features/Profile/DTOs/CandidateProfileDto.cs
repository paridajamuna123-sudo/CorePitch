using JobMailer.Application.Common.Interfaces;
using JobMailer.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Profile.DTOs;

public class CandidateProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double YearsOfExperience { get; set; }
    public string CoreSkillsCsv { get; set; } = string.Empty;
    public string KeyAchievements { get; set; } = string.Empty;
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class UpdateCandidateProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double YearsOfExperience { get; set; }
    public string CoreSkillsCsv { get; set; } = string.Empty;
    public string KeyAchievements { get; set; } = string.Empty;
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }
    public string? PortfolioUrl { get; set; }
}
