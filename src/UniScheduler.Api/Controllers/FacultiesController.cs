using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Faculties.Commands;
using UniScheduler.Application.Features.Faculties.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class FacultiesController : ControllerBase
{
    private readonly IMediator mediator;

    public FacultiesController(IMediator mediator) => this.mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<FacultyDto>>> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetFacultiesQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<FacultyDto>> Create([FromBody] CreateFacultyCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FacultyDto>> Update(Guid id, [FromBody] UpdateFacultyRequest req, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateFacultyCommand(id, req.Name, req.ShortCode), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteFacultyCommand(id), ct);
        return NoContent();
    }
}

public record UpdateFacultyRequest(string Name, string ShortCode);
