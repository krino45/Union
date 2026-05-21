using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Invitations.Commands;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Auth.Commands;

// Accept an invitation as an already-logged-in user. The session must belong to the invitee
// - If invitation has a TeacherId, the current user AppUser.TeacherId must match it
// - If invitation has no TeacherId, accepting while logged in is not supported here
public record AcceptInvitationCommand(string Token) : IRequest<LoginResult>;

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, LoginResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwt;
    private readonly ICurrentUserService _user;

    public AcceptInvitationCommandHandler(IApplicationDbContext db, IJwtService jwt, ICurrentUserService user)
    { _db = db; _jwt = jwt; _user = user; }

    public async Task<LoginResult> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new UnauthorizedAccessException("Требуется аутентификация.");

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new UnauthorizedAccessException("Токен приглашения отсутствует.");

        var otpHash = CreateInvitationCommandHandler.HashToken(request.Token);
        var inv = await _db.Invitations
            .Include(i => i.University)
            .FirstOrDefaultAsync(i => i.OtpHash == otpHash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Приглашение не найдено или уже использовано.");

        if (inv.ConsumedAt != null)
            throw new UnauthorizedAccessException("Это приглашение уже использовано.");
        if (inv.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Срок действия приглашения истёк.");

        var currentUser = await _db.AppUsers
            .Include(u => u.UniversityAccesses).ThenInclude(a => a.University)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Пользователь не найден.");

        if (inv.TeacherId.HasValue)
        {
            if (currentUser.TeacherId != inv.TeacherId)
                throw new ForbiddenException("Это приглашение не предназначено для вашего аккаунта.");
        }
        else
        {
            throw new ForbiddenException("Это приглашение можно принять только через регистрацию.");
        }

        // Grant or upgrade the UserUniversityAccess
        var existing = await _db.UserUniversityAccesses
            .FirstOrDefaultAsync(a => a.UserId == currentUser.Id && a.UniversityId == inv.UniversityId, cancellationToken);
        if (existing != null)
        {
            existing.Role = inv.UniversityRole;
        }
        else
        {
            _db.UserUniversityAccesses.Add(new UserUniversityAccess
            {
                UserId = currentUser.Id,
                UniversityId = inv.UniversityId,
                Role = inv.UniversityRole
            });
        }

        // If the inviting admin set a higher system role, upgrade (Teacher → Admin, but never demote a SuperAdmin).
        if (inv.SystemRole == "Admin" && currentUser.Role == "Teacher")
            currentUser.Role = "Admin";

        inv.ConsumedAt = DateTime.UtcNow;
        inv.ConsumedByUserId = currentUser.Id;

        await _db.SaveChangesAsync(cancellationToken);

        var refreshed = await _db.AppUsers
            .Include(u => u.UniversityAccesses).ThenInclude(a => a.University)
            .FirstAsync(u => u.Id == currentUser.Id, cancellationToken);

        var token = _jwt.GenerateToken(refreshed);
        var universities = refreshed.UniversityAccesses
            .Select(a => new UniversityAccessDto(
                a.UniversityId, a.University.Name, a.University.ShortName, a.University.LogoUrl, a.Role.ToString()))
            .ToList();

        return new LoginResult(token, refreshed.Username, refreshed.Role, refreshed.Id, refreshed.TeacherId, universities);
    }
}
