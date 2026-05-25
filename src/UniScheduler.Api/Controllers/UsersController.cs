using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Features.Users.Commands;
using UniScheduler.Application.Features.Users.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll([FromQuery] string? q, CancellationToken ct)
        => Ok(await _mediator.Send(new GetUsersQuery(q), ct));

    [HttpPost]
    public async Task<ActionResult<CreateUserResult>> Create([FromBody] CreateUserCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));
}
