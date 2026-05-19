using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Subjects.Commands;
using UniScheduler.Application.Features.Subjects.Queries;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SubjectsController : ControllerBase
{
    private readonly IMediator mediator;

    public SubjectsController(IMediator mediator) => this.mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<SubjectDto>>> GetAll([FromQuery] int? academicYear, [FromQuery] Term? term, CancellationToken ct)
        => Ok(await mediator.Send(new GetSubjectsQuery(academicYear, term), ct));

    [HttpPost]
    public async Task<ActionResult<SubjectDto>> Create([FromBody] CreateSubjectCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SubjectDto>> Update(Guid id, [FromBody] UpdateSubjectRequest req, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateSubjectCommand(id, req.Name, req.ShortName, req.AcademicYear, req.Term, req.DepartmentId), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteSubjectCommand(id), ct);
        return NoContent();
    }
}

public record UpdateSubjectRequest(
    string Name, string ShortName,
    int AcademicYear, Term Term,
    Guid? DepartmentId = null);
