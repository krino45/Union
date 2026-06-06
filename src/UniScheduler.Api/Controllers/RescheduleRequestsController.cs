using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.RescheduleRequests.Commands;
using UniScheduler.Application.Features.RescheduleRequests.Queries;
using UniScheduler.Application.Features.Rooms.Queries;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/reschedule-requests")]
[Authorize]
public class RescheduleRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    public RescheduleRequestsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<RescheduleRequestDto>>> GetAll(
        [FromQuery] RescheduleStatus? status, [FromQuery] Guid? teacherId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRescheduleRequestsQuery(status, teacherId), ct));

    // Rooms free at a proposed slot - lets teachers suggest a feasible room.
    [HttpGet("available-rooms")]
    public async Task<ActionResult<List<RoomDto>>> AvailableRooms(
        [FromQuery] Guid scheduleId, [FromQuery] RussianDayOfWeek dayOfWeek, [FromQuery] int pairNumber,
        [FromQuery] WeekType weekType, [FromQuery] Guid? excludeEntryId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAvailableRoomsQuery(scheduleId, dayOfWeek, pairNumber, weekType, excludeEntryId), ct));

    [HttpPost]
    public async Task<ActionResult<RescheduleRequestDto>> Create([FromBody] CreateRescheduleRequestCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest req, CancellationToken ct)
    {
        await _mediator.Send(new ApproveRescheduleRequestCommand(id, req.NewDay, req.NewPair, req.NewWeekType, req.NewRoomId, req.NewIsOnline, req.AdminNote), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest req, CancellationToken ct)
    {
        await _mediator.Send(new RejectRescheduleRequestCommand(id, req.AdminNote ?? string.Empty), ct);
        return NoContent();
    }
}

public record ApproveRequest(RussianDayOfWeek NewDay, int NewPair, WeekType NewWeekType, Guid? NewRoomId, bool NewIsOnline, string? AdminNote);
public record RejectRequest(string? AdminNote);
