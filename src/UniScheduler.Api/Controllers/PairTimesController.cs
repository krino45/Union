using MediatR;
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
}
