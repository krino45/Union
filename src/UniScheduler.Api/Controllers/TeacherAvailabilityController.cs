using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.TeacherAvailability.Commands;
using UniScheduler.Application.Features.TeacherAvailability.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/teacher-availability")]
[Authorize]
public class TeacherAvailabilityController : ControllerBase
{
    private readonly IMediator _mediator;
    public TeacherAvailabilityController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<TeacherAvailabilityDto>>> GetAll([FromQuery] Guid? teacherId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTeacherAvailabilityQuery(teacherId), ct));

    [HttpPost]
    public async Task<ActionResult<TeacherAvailabilityDto>> Create([FromBody] CreateTeacherAvailabilityCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TeacherAvailabilityDto>> Update(Guid id, [FromBody] UpdateTeacherAvailabilityRequest req, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateTeacherAvailabilityCommand(id, req.DayOfWeek, req.PairNumber, req.WeekType, req.Reason, req.IsRecurring, req.ValidFrom, req.ValidTo), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteTeacherAvailabilityCommand(id), ct);
        return NoContent();
    }
}

public record UpdateTeacherAvailabilityRequest(
    UniScheduler.Domain.Enums.RussianDayOfWeek DayOfWeek, int PairNumber,
    UniScheduler.Domain.Enums.WeekType WeekType, string? Reason,
    bool IsRecurring, DateOnly? ValidFrom, DateOnly? ValidTo);
