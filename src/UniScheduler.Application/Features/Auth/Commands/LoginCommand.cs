using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.Auth.Commands;

public record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

public record UniversityAccessDto(
    Guid UniversityId,
    string UniversityName,
    string ShortName,
    string? LogoUrl,
    string Role);

public record LoginResult(
    string Token,
    string Username,
    string Role,
    Guid UserId,
    Guid? TeacherId,
    IReadOnlyList<UniversityAccessDto> Universities);

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwt;

    public LoginCommandHandler(IApplicationDbContext db, IJwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers
            .Include(u => u.UniversityAccesses)
                .ThenInclude(a => a.University)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var token = _jwt.GenerateToken(user);
        var universities = user.UniversityAccesses
            .Select(a => new UniversityAccessDto(
                a.UniversityId,
                a.University.Name,
                a.University.ShortName,
                a.University.LogoUrl,
                a.Role.ToString()))
            .ToList();

        return new LoginResult(token, user.Username, user.Role, user.Id, user.TeacherId, universities);
    }
}

public record RenewTokenCommand(Guid UserId) : IRequest<LoginResult>;

public class RenewTokenCommandHandler : IRequestHandler<RenewTokenCommand, LoginResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwt;

    public RenewTokenCommandHandler(IApplicationDbContext db, IJwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<LoginResult> Handle(RenewTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers
            .Include(u => u.UniversityAccesses)
                .ThenInclude(a => a.University)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        var token = _jwt.GenerateToken(user);
        var universities = user.UniversityAccesses
            .Select(a => new UniversityAccessDto(
                a.UniversityId,
                a.University.Name,
                a.University.ShortName,
                a.University.LogoUrl,
                a.Role.ToString()))
            .ToList();

        return new LoginResult(token, user.Username, user.Role, user.Id, user.TeacherId, universities);
    }
}
