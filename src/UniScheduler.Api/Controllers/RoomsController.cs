using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Rooms.Commands;
using UniScheduler.Application.Features.Rooms.Queries;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class RoomsController : ControllerBase
{
    private readonly IMediator _mediator;
    public RoomsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<RoomDto>>> GetAll(
        [FromQuery] Guid? buildingId, [FromQuery] RoomType? type, [FromQuery] int? minCapacity, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRoomsQuery(buildingId, type, minCapacity), ct));

    [HttpGet("distance")]
    public async Task<ActionResult<RoomDistanceDto>> Distance(
        [FromQuery] Guid from, [FromQuery] Guid to, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRoomDistanceQuery(from, to), ct));

    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create([FromBody] CreateRoomCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoomDto>> Update(Guid id, [FromBody] UpdateRoomRequest req, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateRoomCommand(id, req.BuildingId, req.Number, req.RoomType, req.Capacity, req.HasProjector, req.HasComputers, req.HasLab, req.IsOnline, req.Floor, req.AllowedLessonTypes, req.IsEnabled, req.DepartmentId), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteRoomCommand(id), ct);
        return NoContent();
    }
}

public record UpdateRoomRequest(Guid BuildingId, string Number, RoomType RoomType, int Capacity, bool HasProjector, bool HasComputers, bool HasLab, bool IsOnline, int Floor = 1, List<LessonType>? AllowedLessonTypes = null, bool IsEnabled = true, Guid? DepartmentId = null);
