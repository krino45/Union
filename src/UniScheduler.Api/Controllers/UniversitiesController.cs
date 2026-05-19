using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Features.Universities.Commands;
using UniScheduler.Application.Features.Universities.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class UniversitiesController : ControllerBase
{
    private readonly IMediator _mediator;
    public UniversitiesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<UniversityDto>>> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetUniversitiesQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<UniversityDto>> Create([FromBody] CreateUniversityCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUniversityCommand command, CancellationToken ct)
    {
        await _mediator.Send(command with { Id = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteUniversityCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/users")]
    public async Task<ActionResult<List<UniversityUserDto>>> GetUsers(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetUniversityUsersQuery(id), ct));

    [HttpPost("{id:guid}/users")]
    public async Task<IActionResult> AssignUser(Guid id, [FromBody] AssignUniversityUserCommand command, CancellationToken ct)
    {
        await _mediator.Send(command with { UniversityId = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/users/{userId:guid}")]
    public async Task<IActionResult> RevokeUser(Guid id, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new RevokeUniversityUserCommand(id, userId), ct);
        return NoContent();
    }
}
