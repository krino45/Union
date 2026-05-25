using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Invitations.Commands;

public record CreateInvitationCommand(
    Guid UniversityId,
    string Email,
    UniversityRole UniversityRole,
    Guid? TeacherId = null) : IRequest<CreateInvitationResult>;

public record CreateInvitationResult(Guid InvitationId, string Email, DateTime ExpiresAt);

public class CreateInvitationCommandHandler : IRequestHandler<CreateInvitationCommand, CreateInvitationResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IEmailSender _email;
    private readonly IAppUrls _urls;

    public CreateInvitationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService user,
        IEmailSender email,
        IAppUrls urls)
    {
        _db = db; _user = user; _email = email; _urls = urls;
    }

    public async Task<CreateInvitationResult> Handle(CreateInvitationCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");

        if (request.UniversityRole == UniversityRole.Admin && !_user.IsSuperAdmin)
            throw new ForbiddenException("Только суперадминистратор может приглашать администраторов.");

        if (request.UniversityRole == UniversityRole.Teacher && !request.TeacherId.HasValue)
            throw new InvalidOperationException("Приглашение преподавателя должно быть привязано к карточке преподавателя.");

        if (!_user.IsSuperAdmin)
        {
            var access = await _db.UserUniversityAccesses
                .FirstOrDefaultAsync(a => a.UserId == userId && a.UniversityId == request.UniversityId, cancellationToken);
            if (access == null || access.Role != UniversityRole.Admin)
                throw new ForbiddenException("Нет прав администратора в этом университете.");
        }

        Teacher? teacher = null;
        if (request.TeacherId.HasValue)
        {
            teacher = await _db.Teachers
                .FirstOrDefaultAsync(t => t.Id == request.TeacherId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Teacher), request.TeacherId.Value);
            if (teacher.UniversityId != request.UniversityId)
                throw new InvalidOperationException("Преподаватель принадлежит другому университету.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email)
            ? (teacher?.Email ?? string.Empty)
            : request.Email;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new InvalidOperationException("Некорректный e-mail.");

        var token = GenerateToken();
        var otpHash = HashToken(token);

        var invitation = new Invitation
        {
            Email = email.Trim().ToLowerInvariant(),
            UniversityId = request.UniversityId,
            UniversityRole = request.UniversityRole,
            SystemRole = request.UniversityRole == UniversityRole.Admin ? "Admin" : "Teacher",
            TeacherId = request.TeacherId,
            OtpHash = otpHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            InvitedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(cancellationToken);

        // Send email
        var registerUrl = $"{_urls.BaseUrl}/register?token={token}";
        var university = await _db.Universities.FindAsync(new object[] { request.UniversityId }, cancellationToken);
        var teacherSuffix = teacher != null ? $" как преподаватель <strong>{teacher.LastName} {teacher.FirstName}</strong>" : string.Empty;
        var body = $@"
            <p>Здравствуйте!</p>
            <p>Вас пригласили в систему «Юниран» — университет <strong>{university?.Name}</strong>{teacherSuffix}.</p>
            <p>Перейдите по ссылке, чтобы принять приглашение (действует 7 дней):</p>
            <p><a href=""{registerUrl}"">{registerUrl}</a></p>
            <p>Если у вас уже есть аккаунт в системе, ссылка предложит подтвердить и привязать его. Иначе будет создан новый.</p>
            <p>Если вы не ждали это приглашение, просто проигнорируйте письмо.</p>";
        await _email.SendAsync(invitation.Email, "Приглашение в Юниран", body, cancellationToken);

        return new CreateInvitationResult(invitation.Id, invitation.Email, invitation.ExpiresAt);
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        // URL-safe base64
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public record CancelInvitationCommand(Guid InvitationId) : IRequest;

public class CancelInvitationCommandHandler : IRequestHandler<CancelInvitationCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public CancelInvitationCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(CancelInvitationCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");
        var inv = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == request.InvitationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invitation), request.InvitationId);

        if (!_user.IsSuperAdmin)
        {
            var access = await _db.UserUniversityAccesses
                .FirstOrDefaultAsync(a => a.UserId == userId && a.UniversityId == inv.UniversityId, cancellationToken);
            if (access == null || access.Role != UniversityRole.Admin)
                throw new ForbiddenException("Нет прав администратора.");
        }

        _db.Invitations.Remove(inv);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
