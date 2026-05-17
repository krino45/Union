using UniScheduler.Api.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Application.Features.Schedules.Queries;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SchedulesController : ControllerBase
{
    private readonly IMediator mediator;
    private readonly IGenerationJobQueue jobQueue;

    public SchedulesController(IMediator mediator, IGenerationJobQueue jobQueue)
    {
        this.mediator = mediator;
        this.jobQueue = jobQueue;
    }

    [HttpGet]
    public async Task<ActionResult<List<ScheduleDto>>> GetAll([FromQuery] ScheduleStatus? status, CancellationToken ct)
        => Ok(await mediator.Send(new GetSchedulesQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScheduleDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetScheduleByIdQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<ScheduleDto>> Create([FromBody] CreateScheduleCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteScheduleCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        await mediator.Send(new PublishScheduleCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/generate")]
    public IActionResult Generate(Guid id, [FromQuery] int timeoutSeconds = 60)
    {
        var jobId = jobQueue.Enqueue(id, timeoutSeconds);
        return Accepted(new { jobId, scheduleId = id, status = "queued" });
    }

    [HttpGet("{id:guid}/generate/status")]
    public IActionResult GetGenerationStatus(Guid id)
    {
        var status = jobQueue.GetStatus(id);
        return Ok(status);
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<ScheduleAuditDto>> Audit(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetScheduleAuditQuery(id), ct));

    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<List<ScheduleEntryDto>>> GetEntries(
        Guid id, [FromQuery] Guid? groupId, [FromQuery] Guid? teacherId, [FromQuery] RussianDayOfWeek? dayOfWeek,
        CancellationToken ct)
        => Ok(await mediator.Send(new GetScheduleEntriesQuery(id, groupId, teacherId, dayOfWeek), ct));
}
