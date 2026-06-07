using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Features.PairTimes;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/pair-times")]
public class PairTimesController : ControllerBase
{
    private readonly IMediator mediator;
    public PairTimesController(IMediator mediator) => this.mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<PairTimeDto>>> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetPairTimesQuery(), ct));

    [HttpPut]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update([FromBody] List<PairTimeDto> pairs, CancellationToken ct)
    {
        await mediator.Send(new UpdatePairTimesCommand(pairs), ct);
        return NoContent();
    }
}
