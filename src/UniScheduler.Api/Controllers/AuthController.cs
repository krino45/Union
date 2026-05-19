using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Features.Auth.Commands;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Login and receive a JWT token.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    /// <summary>Renew a valid JWT.</summary>
    [Authorize]
    [HttpPost("renew")]
    public async Task<ActionResult<LoginResult>> Renew(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();
        return Ok(await _mediator.Send(new RenewTokenCommand(userId), ct));
    }
}
