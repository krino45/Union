using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ExcelController : ControllerBase
{
    private readonly IExcelExportService _export;
    private readonly IExcelImportService _import;

    public ExcelController(IExcelExportService export, IExcelImportService import)
    {
        _export = export;
        _import = import;
    }

    [HttpGet("export/{scheduleId:guid}")]
    public async Task<IActionResult> Export(Guid scheduleId, [FromQuery] Guid? groupId, [FromQuery] Guid? teacherId, CancellationToken ct)
    {
        var bytes = await _export.ExportScheduleAsync(scheduleId, groupId, teacherId, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "schedule.xlsx");
    }

    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportPreviewDto>> Import([FromQuery] Guid scheduleId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var preview = await _import.ParseAsync(stream, scheduleId, ct);
        return Ok(preview);
    }

    [HttpPost("import/confirm")]
    public async Task<IActionResult> ConfirmImport([FromQuery] Guid scheduleId, [FromBody] ImportPreviewDto preview, CancellationToken ct)
    {
        var count = await _import.CommitAsync(preview, scheduleId, ct);
        return Ok(new { entriesImported = count });
    }
}
