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

    [HttpGet("{buildingId:guid}/floorplans")]
    public async Task<ActionResult<List<FloorPlanSummaryDto>>> ListFloorPlans(Guid buildingId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFloorPlansQuery(buildingId), ct));

    [HttpPost("{buildingId:guid}/floorplans/from-draft")]
    public async Task<ActionResult> PublishDraftAsFloorPlan(Guid buildingId, [FromBody] PublishDraftRequest req, CancellationToken ct)
    {
        var id = await _mediator.Send(new PublishFloorPlanDraftCommand(req.DraftId, req.Name), ct);
        return Ok(new { id });
    }

    [HttpPatch("{buildingId:guid}/floorplans/{floorPlanId:guid}/activate")]
    public async Task<IActionResult> ActivateFloorPlan(Guid buildingId, Guid floorPlanId, CancellationToken ct)
    {
        await _mediator.Send(new ActivateFloorPlanCommand(floorPlanId), ct);
        return NoContent();
    }

    [HttpDelete("{buildingId:guid}/floorplans/{floorPlanId:guid}")]
    public async Task<IActionResult> DeleteFloorPlan(Guid buildingId, Guid floorPlanId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteFloorPlanCommand(floorPlanId), ct);
        return NoContent();
    }

    // ─── Floor plan drafts (multi-user, named, owner-private by default) ──────────────────

    [HttpGet("{buildingId:guid}/floorplan/drafts")]
    public async Task<ActionResult<List<FloorPlanDraftSummaryDto>>> ListFloorPlanDrafts(Guid buildingId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFloorPlanDraftsQuery(buildingId), ct));

    [HttpPost("{buildingId:guid}/floorplan/drafts")]
    public async Task<ActionResult<Guid>> CreateFloorPlanDraft(Guid buildingId, [FromBody] CreateFloorPlanDraftRequest req, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateFloorPlanDraftCommand(buildingId, req.Name, req.DraftJson ?? string.Empty), ct);
        return Ok(new { id });
    }

    [HttpGet("{buildingId:guid}/floorplan/drafts/{draftId:guid}")]
    public async Task<ActionResult<FloorPlanDraftDto>> GetFloorPlanDraft(Guid buildingId, Guid draftId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFloorPlanDraftQuery(draftId), ct));

    [HttpPut("{buildingId:guid}/floorplan/drafts/{draftId:guid}")]
    public async Task<IActionResult> UpdateFloorPlanDraft(Guid buildingId, Guid draftId, [FromBody] UpdateFloorPlanDraftRequest req, CancellationToken ct)
    {
        await _mediator.Send(new UpdateFloorPlanDraftCommand(draftId, req.DraftJson), ct);
        return NoContent();
    }

    [HttpPatch("{buildingId:guid}/floorplan/drafts/{draftId:guid}/access")]
    public async Task<IActionResult> SetFloorPlanDraftAccess(Guid buildingId, Guid draftId, [FromBody] SetDraftAccessRequest req, CancellationToken ct)
    {
        await _mediator.Send(new SetFloorPlanDraftAccessCommand(draftId, req.IsOpenToAdmins), ct);
        return NoContent();
    }

    [HttpPatch("{buildingId:guid}/floorplan/drafts/{draftId:guid}/name")]
    public async Task<IActionResult> RenameFloorPlanDraft(Guid buildingId, Guid draftId, [FromBody] RenameDraftRequest req, CancellationToken ct)
    {
        await _mediator.Send(new RenameFloorPlanDraftCommand(draftId, req.Name), ct);
        return NoContent();
    }

    [HttpDelete("{buildingId:guid}/floorplan/drafts/{draftId:guid}")]
    public async Task<IActionResult> DeleteFloorPlanDraft(Guid buildingId, Guid draftId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteFloorPlanDraftCommand(draftId), ct);
        return NoContent();
    }
}

public record UpdateBuildingRequest(string ShortCode, string Address, int NumberOfFloors = 5, int NumberOfBasementFloors = 0);
public record PublishDraftRequest(Guid DraftId, string Name);
public record CreateFloorPlanDraftRequest(string Name, string? DraftJson);
public record UpdateFloorPlanDraftRequest(string DraftJson);
public record SetDraftAccessRequest(bool IsOpenToAdmins);
public record RenameDraftRequest(string Name);
