using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Invitations.Commands;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Auth.Commands;

public record RegisterFromInvitationCommand(string Token, string Username, string Password) : IRequest<LoginResult>;

public class RegisterFromInvitationCommandHandler : IRequestHandler<RegisterFromInvitationCommand, LoginResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwt;
    public RegisterFromInvitationCommandHandler(IApplicationDbContext db, IJwtService jwt)
    { _db = db; _jwt = jwt; }

    public async Task<LoginResult> Handle(RegisterFromInvitationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new UnauthorizedAccessException("Токен приглашения отсутствует.");
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            throw new InvalidOperationException("Логин должен содержать минимум 3 символа.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new InvalidOperationException("Пароль должен содержать минимум 6 символов.");

        var otpHash = CreateInvitationCommandHandler.HashToken(request.Token);
        var inv = await _db.Invitations
            .Include(i => i.University)
            .FirstOrDefaultAsync(i => i.OtpHash == otpHash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Приглашение не найдено или уже использовано.");

        if (inv.ConsumedAt != null)
            throw new UnauthorizedAccessException("Это приглашение уже использовано.");
        if (inv.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Срок действия приглашения истёк.");

        // If invitation is linked to a Teacher entity that already has a registered AppUser,
        // registration is not allowed — the existing user must use AcceptInvitationCommand.
        if (inv.TeacherId.HasValue)
        {
            var existingLinked = await _db.AppUsers
                .AnyAsync(u => u.TeacherId == inv.TeacherId, cancellationToken);
            if (existingLinked)
                throw new InvalidOperationException("Этот преподаватель уже привязан к аккаунту. Войдите под своим логином и примите приглашение.");
        }

        if (await _db.AppUsers.AnyAsync(u => u.Username == request.Username, cancellationToken))
            throw new InvalidOperationException("Пользователь с таким логином уже существует.");

        var email = inv.Email.Trim().ToLowerInvariant();
        if (await _db.AppUsers.AnyAsync(u => u.Email == email, cancellationToken))
            throw new InvalidOperationException("Аккаунт с таким e-mail уже существует. Войдите и примите приглашение.");

        var user = new AppUser
        {
            Username = request.Username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = inv.SystemRole,
            TeacherId = inv.TeacherId
        };
        _db.AppUsers.Add(user);

        _db.UserUniversityAccesses.Add(new UserUniversityAccess
        {
            User = user,
            UniversityId = inv.UniversityId,
            Role = inv.UniversityRole
        });

        inv.ConsumedAt = DateTime.UtcNow;
        inv.ConsumedBy = user;

        await _db.SaveChangesAsync(cancellationToken);

        var token = _jwt.GenerateToken(user);
        var universities = new List<UniversityAccessDto>
        {
            new UniversityAccessDto(inv.UniversityId, inv.University.Name, inv.University.ShortName, inv.University.LogoUrl, inv.UniversityRole.ToString())
        };
        return new LoginResult(token, user.Username, user.Role, user.Id, user.TeacherId, user.Email, universities);
    }
}
