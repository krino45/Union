using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/schedule-entries")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ScheduleEntriesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflictDetector;

    public ScheduleEntriesController(IMediator mediator, IApplicationDbContext db, IConflictDetector conflictDetector)
    {
        _mediator = mediator;
        _db = db;
        _conflictDetector = conflictDetector;
    }

    [HttpPost]
    public async Task<ActionResult<ScheduleEntryDto>> Create([FromBody] CreateEntryCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(CheckConflicts), result);
    }

    /// <summary>Creates one class as several parallel sessions (language streams / lab subgroups).</summary>
    [HttpPost("parallel")]
    public async Task<ActionResult<List<ScheduleEntryDto>>> CreateParallel([FromBody] CreateParallelEntriesCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));

    [HttpPost("{id:guid}/move")]
    public async Task<ActionResult<ScheduleEntryDto>> Move(Guid id, [FromBody] MoveRequest req, CancellationToken ct)
        => Ok(await _mediator.Send(new MoveEntryCommand(id, req.DayOfWeek, req.PairNumber, req.WeekType, req.RoomId), ct));

    [HttpPost("{id:guid}/update")]
    public async Task<ActionResult<ScheduleEntryDto>> Update(Guid id, [FromBody] UpdateRequest req, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateEntryCommand(id,
            req.SubjectId, req.TeacherId, req.RoomId, req.GroupIds,
            req.DayOfWeek, req.PairNumber, req.WeekType, req.LessonType, req.IsOnline, req.SubgroupLabel), ct));

    /// <summary>Edits one half of a Both-week lesson, splitting it into Odd + Even rows if needed.</summary>
    [HttpPost("{id:guid}/split-edit")]
    public async Task<ActionResult<ScheduleEntryDto>> SplitEdit(Guid id, [FromBody] SplitEditRequest req, CancellationToken ct)
        => Ok(await _mediator.Send(new SplitAndEditEntryCommand(id,
            req.TargetWeek, req.SubjectId, req.TeacherId, req.RoomId, req.GroupIds,
            req.DayOfWeek, req.PairNumber, req.LessonType, req.IsOnline), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteEntryCommand(id), ct);
        return NoContent();
    }

    /// <summary>Check conflicts for a proposed assignment without persisting it.</summary>
    [HttpGet("conflicts")]
    public async Task<IActionResult> CheckConflicts(
        [FromQuery] Guid scheduleId,
        [FromQuery] Guid? roomId,
        [FromQuery] Guid teacherId,
        [FromQuery] List<Guid> groupIds,
        [FromQuery] RussianDayOfWeek dayOfWeek,
        [FromQuery] int pairNumber,
        [FromQuery] WeekType weekType,
        [FromQuery] bool isOnline,
        [FromQuery] Guid? excludeEntryId,
        [FromQuery] Guid? parallelGroupId,
        [FromQuery] string? subgroupLabel,
        CancellationToken ct)
    {
        var existing = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == scheduleId)
            .ToListAsync(ct);

        bool roomIsDistributed = roomId.HasValue
            && await _db.Rooms.AnyAsync(r => r.Id == roomId && r.IsDistributed, ct);

        var conflicts = _conflictDetector.DetectConflicts(
            excludeEntryId ?? Guid.Empty, scheduleId, roomId, teacherId, groupIds,
            dayOfWeek, pairNumber, weekType, isOnline, existing, parallelGroupId, roomIsDistributed, subgroupLabel);

        return Ok(conflicts);
    }
}

public record MoveRequest(RussianDayOfWeek DayOfWeek, int PairNumber, WeekType WeekType, Guid? RoomId);

public record UpdateRequest(
    Guid SubjectId, Guid TeacherId, Guid? RoomId,
    List<Guid> GroupIds,
    RussianDayOfWeek DayOfWeek, int PairNumber, WeekType WeekType,
    LessonType LessonType, bool IsOnline,
    string? SubgroupLabel = null);

public record SplitEditRequest(
    WeekType TargetWeek,
    Guid SubjectId, Guid TeacherId, Guid? RoomId,
    List<Guid> GroupIds,
    RussianDayOfWeek DayOfWeek, int PairNumber,
    LessonType LessonType, bool IsOnline);
