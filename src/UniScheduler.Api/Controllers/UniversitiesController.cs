using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Features.Invitations.Commands;
using UniScheduler.Application.Features.Invitations.Queries;
using UniScheduler.Application.Features.Universities.Commands;
using UniScheduler.Application.Features.Universities.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UniversitiesController : ControllerBase
{
    private readonly IMediator _mediator;
    public UniversitiesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<List<UniversityDto>>> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetUniversitiesQuery(), ct));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<UniversityDto>> Create([FromBody] CreateUniversityCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUniversityCommand command, CancellationToken ct)
    {
        await _mediator.Send(command with { Id = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteUniversityCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/users")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<List<UniversityUserDto>>> GetUsers(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetUniversityUsersQuery(id), ct));

    [HttpPost("{id:guid}/users")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> AssignUser(Guid id, [FromBody] AssignUniversityUserCommand command, CancellationToken ct)
    {
        await _mediator.Send(command with { UniversityId = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/users/{userId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> RevokeUser(Guid id, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new RevokeUniversityUserCommand(id, userId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/grant-self")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GrantSelf(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new GrantSelfUniversityAccessCommand(id), ct);
        return NoContent();
    }

    // ─── Invitations (admins can invite Teachers; SuperAdmin can invite Admins) ────────

    [HttpGet("{id:guid}/invitations")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<InvitationDto>>> ListInvitations(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetInvitationsQuery(id), ct));

    [HttpPost("{id:guid}/invitations")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<CreateInvitationResult>> CreateInvitation(
        Guid id,
        [FromBody] CreateInvitationBody body,
        CancellationToken ct)
        => Ok(await _mediator.Send(new CreateInvitationCommand(id, body.Email, body.UniversityRole, body.TeacherId), ct));

    [HttpDelete("invitations/{invitationId:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> CancelInvitation(Guid invitationId, CancellationToken ct)
    {
        await _mediator.Send(new CancelInvitationCommand(invitationId), ct);
        return NoContent();
    }
}

public record CreateInvitationBody(string Email, UniScheduler.Domain.Enums.UniversityRole UniversityRole, Guid? TeacherId = null);
