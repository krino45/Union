using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Buildings.Commands;
using UniScheduler.Application.Features.Buildings.Queries;
using UniScheduler.Application.Features.FloorPlan.Commands;
using UniScheduler.Application.Features.FloorPlan.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class BuildingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public BuildingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<BuildingDto>>> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetBuildingsQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<BuildingDto>> Create([FromBody] CreateBuildingCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BuildingDto>> Update(Guid id, [FromBody] UpdateBuildingRequest req, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateBuildingCommand(id, req.ShortCode, req.Address, req.NumberOfFloors, req.NumberOfBasementFloors), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteBuildingCommand(id), ct);
        return NoContent();
    }

    [HttpGet("distances")]
    public async Task<ActionResult<List<BuildingDistanceDto>>> GetDistances(CancellationToken ct)
        => Ok(await _mediator.Send(new GetBuildingDistancesQuery(), ct));

    [HttpPut("distances")]
    public async Task<IActionResult> SetDistances([FromBody] List<SetBuildingDistanceRequest> distances, CancellationToken ct)
    {
        await _mediator.Send(new SetBuildingDistancesCommand(distances), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/floorplan")]
    public async Task<ActionResult<FloorPlanDto>> GetFloorPlan(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFloorPlanQuery(id), ct));

    [HttpPut("{id:guid}/floorplan")]
    public async Task<IActionResult> SaveFloorPlan(Guid id, [FromBody] SaveFloorPlanRequest req, CancellationToken ct)
    {
        await _mediator.Send(new SaveFloorPlanCommand(id, req), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/floorplan/draft")]
    public async Task<ActionResult<FloorPlanDraftDto>> GetFloorPlanDraft(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFloorPlanDraftQuery(id), ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPut("{id:guid}/floorplan/draft")]
    public async Task<IActionResult> SaveFloorPlanDraft(Guid id, [FromBody] SaveFloorPlanDraftRequest req, CancellationToken ct)
    {
        await _mediator.Send(new SaveFloorPlanDraftCommand(id, req.DraftJson), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/floorplan/draft")]
    public async Task<IActionResult> DeleteFloorPlanDraft(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteFloorPlanDraftCommand(id), ct);
        return NoContent();
    }
}

public record UpdateBuildingRequest(string ShortCode, string Address, int NumberOfFloors = 5, int NumberOfBasementFloors = 0);
public record SaveFloorPlanDraftRequest(string DraftJson);
