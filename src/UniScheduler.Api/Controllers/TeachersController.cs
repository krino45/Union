using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Teachers.Commands;
using UniScheduler.Application.Features.Teachers.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class TeachersController : ControllerBase
{
    private readonly IMediator mediator;

    public TeachersController(IMediator mediator) => this.mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<TeacherDto>>> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetTeachersQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<TeacherDto>> Create([FromBody] CreateTeacherCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TeacherDto>> Update(Guid id, [FromBody] UpdateTeacherRequest req, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateTeacherCommand(id, req.FirstName, req.LastName, req.MiddleName, req.Email), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteTeacherCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/subjects")]
    public async Task<IActionResult> SetSubjects(Guid id, [FromBody] List<TeacherSubjectAssignment> subjects, CancellationToken ct)
    {
        await mediator.Send(new SetTeacherSubjectsCommand(id, subjects), ct);
        return NoContent();
    }
}

public record UpdateTeacherRequest(string FirstName, string LastName, string MiddleName, string Email);
