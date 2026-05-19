using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Departments;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DepartmentsController : ControllerBase
{
    private readonly IMediator mediator;
    public DepartmentsController(IMediator mediator) => this.mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<DepartmentDto>>> GetAll([FromQuery] Guid? facultyId, CancellationToken ct)
        => Ok(await mediator.Send(new GetDepartmentsQuery(facultyId), ct));

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, [FromBody] UpdateDepartmentRequest req, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateDepartmentCommand(id, req.Name, req.ShortCode, req.FacultyId), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteDepartmentCommand(id), ct);
        return NoContent();
    }
}

public record UpdateDepartmentRequest(string Name, string ShortCode, Guid FacultyId);
