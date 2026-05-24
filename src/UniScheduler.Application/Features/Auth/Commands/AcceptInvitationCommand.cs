using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Invitations.Commands;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Auth.Commands;

// Accept an invitation as an already-logged-in user.
// Caller account email MUST equal the invitation email.
//  - Invitation with TeacherId: caller must either already be linked to that teacher,
//    OR not be linked to any teacher. If linked to a different teacher, refuse.
//    Also refuse if the target teacher is already linked to someone else.
//  - Invitation without TeacherId: just grant/upgrade uni access.
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

        var inviteEmail = inv.Email.Trim().ToLowerInvariant();
        var myEmail = currentUser.Email?.Trim().ToLowerInvariant();
        if (myEmail == null || myEmail != inviteEmail)
            throw new ForbiddenException(
                $"Это приглашение отправлено на {inv.Email}. Войдите в аккаунт с этим e-mail или зарегистрируйте новый по ссылке из письма.");

        if (inv.TeacherId.HasValue)
        {
            if (currentUser.TeacherId == inv.TeacherId)
            {
                // Already correctly bound.
            }
            else if (currentUser.TeacherId == null)
            {
                // Bind. But guard against a race where the teacher is already linked to a different account.
                var teacherAlreadyLinked = await _db.AppUsers
                    .AnyAsync(u => u.TeacherId == inv.TeacherId && u.Id != currentUser.Id, cancellationToken);
                if (teacherAlreadyLinked)
                    throw new ForbiddenException("К указанному преподавателю уже привязан другой аккаунт.");
                currentUser.TeacherId = inv.TeacherId;
            }
            else
            {
                throw new ForbiddenException("Ваш аккаунт уже привязан к другому преподавателю.");
            }
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

        return new LoginResult(token, refreshed.Username, refreshed.Role, refreshed.Id, refreshed.TeacherId, refreshed.Email, universities);
    }
}
