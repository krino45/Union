using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.StudentGroups.Commands;
using UniScheduler.Application.Features.StudentGroups.Queries;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/student-groups")]
[Authorize(Roles = "Admin")]
public class StudentGroupsController : ControllerBase
{
    private readonly IMediator mediator;

    public StudentGroupsController(IMediator mediator) => this.mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<StudentGroupDto>>> GetAll([FromQuery] int? year, [FromQuery] Guid? facultyId, CancellationToken ct)
        => Ok(await mediator.Send(new GetStudentGroupsQuery(year, facultyId), ct));

    [HttpPost]
    public async Task<ActionResult<StudentGroupDto>> Create([FromBody] CreateStudentGroupCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StudentGroupDto>> Update(Guid id, [FromBody] UpdateStudentGroupRequest req, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateStudentGroupCommand(id, req.Name, req.Year, req.Specialty, req.StudentCount, req.DegreeType, req.FacultyId, req.BlockedDays), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteStudentGroupCommand(id), ct);
        return NoContent();
    }

    [HttpPost("promote")]
    public async Task<IActionResult> Promote([FromBody] PromoteGroupsRequest req, CancellationToken ct)
    {
        var promoted = await mediator.Send(new PromoteGroupsCommand(req.FacultyId), ct);
        return Ok(new { promoted });
    }
}

public record UpdateStudentGroupRequest(string Name, int Year, string Specialty, int StudentCount, DegreeType DegreeType, Guid FacultyId, List<RussianDayOfWeek>? BlockedDays = null);
public record PromoteGroupsRequest(Guid? FacultyId);
