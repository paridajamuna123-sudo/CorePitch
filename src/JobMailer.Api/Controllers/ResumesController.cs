using JobMailer.Application.Features.Resumes.Commands.UploadResume;
using JobMailer.Application.Features.Resumes.DTOs;
using JobMailer.Application.Features.Resumes.Queries.GetResumes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace JobMailer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResumesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ResumesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ResumeDocumentDto>> UploadResume(
        [FromForm] UploadResumeFormRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest("A valid PDF file is required.");
        }

        using var stream = request.File.OpenReadStream();
        var command = new UploadResumeCommand(stream, request.File.FileName, request.VersionName, request.IsDefault);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<ResumeDocumentDto>>> GetResumes(
        CancellationToken cancellationToken)
    {
        var resumes = await _mediator.Send(new GetResumesQuery(), cancellationToken);
        return Ok(resumes);
    }
}
