using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.Features.Auth.Commands;
using UniScheduler.Application.Features.Auth.Queries;

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

    /// <summary>Register a new account using an emailed invitation token. Public.</summary>
    [HttpPost("register-from-invitation")]
    public async Task<ActionResult<LoginResult>> RegisterFromInvitation([FromBody] RegisterFromInvitationCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    /// <summary>Accept an invitation as an already-logged-in user (requires the session to belong to the invitee).</summary>
    [Authorize]
    [HttpPost("accept-invitation")]
    public async Task<ActionResult<LoginResult>> AcceptInvitation([FromBody] AcceptInvitationCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    /// <summary>Probe an invitation token; reveals only sanitized info appropriate for the caller's auth state.</summary>
    [AllowAnonymous]
    [HttpGet("invitation/{token}")]
    public async Task<ActionResult<InvitationInfoDto>> GetInvitationInfo(string token, CancellationToken ct)
        => Ok(await _mediator.Send(new GetInvitationInfoQuery(token), ct));
}
