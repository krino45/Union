using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ExcelController : ControllerBase
{
    private readonly IExcelExportService _export;

    public ExcelController(IExcelExportService export)
    {
        _export = export;
    }

    [HttpGet("export/{scheduleId:guid}")]
    public async Task<IActionResult> Export(Guid scheduleId, [FromQuery] Guid? groupId, [FromQuery] Guid? teacherId, CancellationToken ct)
    {
        var bytes = await _export.ExportScheduleAsync(scheduleId, groupId, teacherId, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "schedule.xlsx");
    }
}
