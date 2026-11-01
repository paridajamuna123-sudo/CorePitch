using JobMailer.Application.Features.Applications.Commands.ExtractFromImage;
using JobMailer.Application.Features.Applications.Commands.GenerateDraft;
using JobMailer.Application.Features.Applications.Commands.QueueApplication;
using JobMailer.Application.Features.Applications.DTOs;
using JobMailer.Application.Features.Applications.Queries.GetApplications;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace JobMailer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApplicationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("extract-from-image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ExtractedJobBannerDto>> ExtractFromImage(
        [FromForm] ExtractImageFormRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ImageFile == null || request.ImageFile.Length == 0)
        {
            return BadRequest("A valid job post screenshot or banner image is required.");
        }

        using var memoryStream = new MemoryStream();
        await request.ImageFile.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();

        byte[]? resumeBytes = null;
        string? resumeFileName = null;
        if (request.ResumePdf != null && request.ResumePdf.Length > 0)
        {
            using var rStream = new MemoryStream();
            await request.ResumePdf.CopyToAsync(rStream, cancellationToken);
            resumeBytes = rStream.ToArray();
            resumeFileName = request.ResumePdf.FileName;
        }

        var command = new ExtractFromImageCommand(
            imageBytes, 
            request.ImageFile.ContentType, 
            resumeBytes, 
            resumeFileName, 
            request.ResumeDocumentId);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }

    [HttpPost("generate-draft")]
    public async Task<ActionResult<DraftEmailDto>> GenerateDraft(
        [FromBody] GenerateDraftCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("queue")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Guid>> QueueApplication(
        [FromForm] QueueApplicationFormRequest request,
        CancellationToken cancellationToken)
    {
        Stream? customResumeStream = null;
        string? customResumeFileName = null;

        if (request.CustomResumePdf != null && request.CustomResumePdf.Length > 0)
        {
            customResumeStream = request.CustomResumePdf.OpenReadStream();
            customResumeFileName = request.CustomResumePdf.FileName;
        }

        var command = new QueueApplicationCommand
        {
            CompanyName = request.CompanyName,
            RecruiterName = request.RecruiterName,
            RecruiterEmail = request.RecruiterEmail,
            JobTitle = request.JobTitle,
            RawJobDescription = request.RawJobDescription ?? string.Empty,
            EmailSubject = request.EmailSubject,
            EmailBodyHtml = request.EmailBodyHtml,
            ResumeDocumentId = request.ResumeDocumentId,
            ScheduledSendUtc = request.ScheduledSendUtc,
            CustomResumeStream = customResumeStream,
            CustomResumeFileName = customResumeFileName
        };

        var applicationId = await _mediator.Send(command, cancellationToken);
        return Ok(new { Id = applicationId });
    }

    [HttpGet]
    public async Task<ActionResult<List<JobApplicationSummaryDto>>> GetApplications(
        CancellationToken cancellationToken)
    {
        var applications = await _mediator.Send(new GetApplicationsQuery(), cancellationToken);
        return Ok(applications);
    }
}
