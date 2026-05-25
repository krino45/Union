using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Api.Auth;
using UniScheduler.Application.Features.Auth.Commands;
using UniScheduler.Application.Features.Auth.Queries;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _env;
    public AuthController(IMediator mediator, IWebHostEnvironment env)
    {
        _mediator = mediator;
        _env = env;
    }

    /// <summary>Login and receive a session cookie (identifier may be e-mail or username).</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginCommand command, CancellationToken ct)
        => IssueSession(await _mediator.Send(command, ct));

    /// <summary>Returns the current session's user, refreshing the cookie. Used to hydrate the SPA on load.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<LoginResult>> Me(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return IssueSession(await _mediator.Send(new RenewTokenCommand(userId), ct));
    }

    /// <summary>Renew the session (kept for compatibility; same effect as /me).</summary>
    [Authorize]
    [HttpPost("renew")]
    public async Task<ActionResult<LoginResult>> Renew(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return IssueSession(await _mediator.Send(new RenewTokenCommand(userId), ct));
    }

    /// <summary>Clear the session cookie.</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookie.Name, AuthCookie.Expired(_env));
        return NoContent();
    }

    /// <summary>Register a new account using an emailed invitation token. Public.</summary>
    [HttpPost("register-from-invitation")]
    public async Task<ActionResult<LoginResult>> RegisterFromInvitation([FromBody] RegisterFromInvitationCommand command, CancellationToken ct)
        => IssueSession(await _mediator.Send(command, ct));

    /// <summary>Accept an invitation as an already-logged-in user (requires the session to belong to the invitee).</summary>
    [Authorize]
    [HttpPost("accept-invitation")]
    public async Task<ActionResult<LoginResult>> AcceptInvitation([FromBody] AcceptInvitationCommand command, CancellationToken ct)
        => IssueSession(await _mediator.Send(command, ct));

    /// <summary>Probe an invitation token; reveals only sanitized info appropriate for the caller's auth state.</summary>
    [AllowAnonymous]
    [HttpGet("invitation/{token}")]
    public async Task<ActionResult<InvitationInfoDto>> GetInvitationInfo(string token, CancellationToken ct)
        => Ok(await _mediator.Send(new GetInvitationInfoQuery(token), ct));

    /// <summary>Request a password-reset link by e-mail. Always returns 204 (no account-existence leak).</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] RequestPasswordResetCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    /// <summary>Set a new password using an emailed reset token. Public.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private ActionResult<LoginResult> IssueSession(LoginResult result)
    {
        Response.Cookies.Append(AuthCookie.Name, result.Token, AuthCookie.Build(_env, DateTimeOffset.UtcNow.AddDays(7)));
        return Ok(result);
    }
}
