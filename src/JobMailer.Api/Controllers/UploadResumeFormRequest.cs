using JobMailer.Application.Features.Resumes.DTOs;

namespace JobMailer.Api.Controllers;

public class UploadResumeFormRequest
{
    public required IFormFile File { get; set; }
    public string VersionName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
