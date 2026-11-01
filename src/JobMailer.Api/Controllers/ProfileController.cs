using JobMailer.Application.Features.Profile.Commands.UpdateProfile;
using JobMailer.Application.Features.Profile.DTOs;
using JobMailer.Application.Features.Profile.Queries.GetProfile;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace JobMailer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<CandidateProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await _mediator.Send(new GetProfileQuery(), cancellationToken);
        if (profile == null)
        {
            return NotFound("Candidate profile has not been created yet.");
        }
        return Ok(profile);
    }

    [HttpPut]
    public async Task<ActionResult<CandidateProfileDto>> UpdateProfile(
        [FromBody] UpdateCandidateProfileDto dto,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateProfileCommand(dto), cancellationToken);
        return Ok(updated);
    }
}
