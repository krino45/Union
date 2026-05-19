using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<List<UserDto>>> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetUsersQuery(), ct));
}
