using UniScheduler.Api.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Application.Features.Schedules.Queries;
using UniScheduler.Domain.Enums;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// Reads are open to any authenticated user (tenant-scoped by X-University-Id query filter)
// every write is restricted to admins
[Authorize]
public class SchedulesController : ControllerBase
{
    private const string AdminOnly = "Admin,SuperAdmin";

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

    [Authorize(Roles = AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<ScheduleDto>> Create([FromBody] CreateScheduleCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [Authorize(Roles = AdminOnly)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteScheduleCommand(id), ct);
        return NoContent();
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        await mediator.Send(new PublishScheduleCommand(id), ct);
        return NoContent();
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPatch("{id:guid}/access")]
    public async Task<IActionResult> SetAccess(Guid id, [FromBody] SetScheduleAccessRequest req, CancellationToken ct)
    {
        await mediator.Send(new SetScheduleAccessCommand(id, req.IsOpenToAdmins), ct);
        return NoContent();
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPatch("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameScheduleRequest req, CancellationToken ct)
    {
        await mediator.Send(new RenameScheduleCommand(id, req.Name), ct);
        return NoContent();
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ArchiveScheduleCommand(id), ct);
        return NoContent();
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken ct)
    {
        await mediator.Send(new UnarchiveScheduleCommand(id), ct);
        return NoContent();
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/update-score")]
    public async Task<ActionResult<int>> UpdateScore(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateBaseScoreCommand(id), ct));

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/validate-edit")]
    public async Task<ActionResult<List<ValidationIssue>>> ValidateEdit(Guid id, [FromBody] ValidateEditBody body, CancellationToken ct)
    {
        var q = new ValidateEditQuery(id, body.EntryId, body.SubjectId, body.TeacherId, body.RoomId,
            body.GroupIds, body.DayOfWeek, body.PairNumber, body.WeekType, body.LessonType, body.IsOnline,
            body.SubgroupLabel);
        return Ok(await mediator.Send(q, ct));
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/generate")]
    public IActionResult Generate(Guid id, [FromBody] GenerateRequest? body)
    {
        var current = jobQueue.GetStatus(id);
        if (current.Status == "queued" || current.Status == "running")
            return Conflict(new { error = "Генерация уже выполняется для этого расписания", status = current.Status, stage = current.Stage });
        var jobId = jobQueue.Enqueue(id, body?.TimeoutSeconds ?? 120);
        return Accepted(new { jobId, scheduleId = id, status = "queued" });
    }

    [Authorize(Roles = AdminOnly)]
    [HttpGet("{id:guid}/generate/status")]
    public IActionResult GetGenerationStatus(Guid id)
    {
        var status = jobQueue.GetStatus(id);
        return Ok(status);
    }

    [Authorize(Roles = AdminOnly)]
    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<ScheduleAuditDto>> Audit(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetScheduleAuditQuery(id), ct));

    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<List<ScheduleEntryDto>>> GetEntries(
        Guid id, [FromQuery] Guid? groupId, [FromQuery] Guid? teacherId, [FromQuery] RussianDayOfWeek? dayOfWeek,
        CancellationToken ct)
        => Ok(await mediator.Send(new GetScheduleEntriesQuery(id, groupId, teacherId, dayOfWeek), ct));

    [Authorize(Roles = AdminOnly)]
    [HttpGet("{id:guid}/plan-progress")]
    public async Task<ActionResult<List<PlanProgressItem>>> PlanProgress(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetPlanProgressQuery(id), ct));

    [HttpGet("{id:guid}/export/json")]
    public async Task<IActionResult> ExportJson(Guid id, CancellationToken ct)
    {
        var entries = await mediator.Send(new GetScheduleEntriesQuery(id), ct);
        var items = entries.Select(e => new JsonEntryImport(
            e.SubjectShortName ?? e.SubjectName,
            e.TeacherDisplayName.Split(' ').FirstOrDefault() ?? "",
            e.TeacherDisplayName.Split(' ').Skip(1).FirstOrDefault() ?? "",
            e.StudentGroups.Select(g => g.Name).ToList(),
            e.BuildingShortCode,
            e.RoomNumber,
            e.DayOfWeek, e.PairNumber, e.WeekType, e.LessonType, e.IsOnline
        )).ToList();

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Write enums (day/week/lesson type) as readable strings so the file is self-documenting
            // and easy to hand-author for a real-university import. Import accepts strings or numbers.
            Converters = { new JsonStringEnumConverter() },
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        var json = JsonSerializer.Serialize(new { scheduleId = id, entries = items }, opts);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"schedule-{id:N}.json");
    }

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/backfill/preview")]
    public async Task<ActionResult<BackfillPreviewDto>> BackfillPreview(
        Guid id, [FromBody] BackfillTargets? targets, CancellationToken ct)
        => Ok(await mediator.Send(new PreviewBackfillQuery(id, targets ?? new BackfillTargets()), ct));

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/backfill/apply")]
    public async Task<ActionResult<BackfillResultDto>> BackfillApply(
        Guid id, [FromBody] BackfillTargets? targets, CancellationToken ct)
        => Ok(await mediator.Send(new ApplyBackfillCommand(id, targets ?? new BackfillTargets()), ct));

    [Authorize(Roles = AdminOnly)]
    [HttpPost("{id:guid}/import/json")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ImportFromJsonResult>> ImportJson(
        Guid id, [FromBody] ImportFromJsonBody body, CancellationToken ct)
    {
        var cmd = new ImportFromJsonCommand(id, body.Replace, body.Entries);
        var result = await mediator.Send(cmd, ct);
        return Ok(result);
    }
}

public record ImportFromJsonBody(bool Replace, List<JsonEntryImport> Entries);
public record GenerateRequest(int TimeoutSeconds = 120);
public record SetScheduleAccessRequest(bool IsOpenToAdmins);
public record RenameScheduleRequest(string Name);
public record ValidateEditBody(
    Guid? EntryId,
    Guid SubjectId,
    Guid TeacherId,
    Guid? RoomId,
    List<Guid> GroupIds,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    WeekType WeekType,
    LessonType LessonType,
    bool IsOnline,
    string? SubgroupLabel = null);
