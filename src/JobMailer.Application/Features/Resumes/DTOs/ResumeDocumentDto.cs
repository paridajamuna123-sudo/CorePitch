using JobMailer.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobMailer.Application.Features.Resumes.DTOs;

public class ResumeDocumentDto
{
    public Guid Id { get; set; }
    public string VersionName { get; set; } = string.Empty;
    public string DisplayFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
