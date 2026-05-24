using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Application.Features.Schedules.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/solver-settings")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class SolverSettingsController : ControllerBase
{
    private readonly IMediator mediator;
    public SolverSettingsController(IMediator mediator) => this.mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<SolverWeights>> Get(CancellationToken ct)
        => Ok(await mediator.Send(new GetSolverSettingsQuery(), ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SolverWeights weights, CancellationToken ct)
    {
        await mediator.Send(new UpdateSolverSettingsCommand(weights), ct);
        return NoContent();
    }
}
