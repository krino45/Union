using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Invitations.Commands;

namespace UniScheduler.Application.Features.Auth.Commands;

// Step 1: user enters their e-mail; if an account with that e-mail exists we mail a reset link.
// Always succeeds silently so the endpoint can't be used to probe which e-mails are registered.
public record RequestPasswordResetCommand(string Email) : IRequest;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand>
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly IApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly IAppUrls _urls;

    public RequestPasswordResetCommandHandler(IApplicationDbContext db, IEmailSender email, IAppUrls urls)
    {
        _db = db; _email = email; _urls = urls;
    }

    public async Task Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return;

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user == null) return; // no leak: pretend success

        var token = GenerateToken();
        user.PasswordResetTokenHash = CreateInvitationCommandHandler.HashToken(token);
        user.PasswordResetExpiresAt = DateTime.UtcNow.Add(Lifetime);
        await _db.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{_urls.BaseUrl}/reset-password?token={token}";
        var body = $@"
            <p>Здравствуйте!</p>
            <p>Поступил запрос на сброс пароля для аккаунта <strong>{user.Username}</strong> в системе «Юниран».</p>
            <p>Перейдите по ссылке, чтобы задать новый пароль (действует 1 час):</p>
            <p><a href=""{resetUrl}"">{resetUrl}</a></p>
            <p>Если вы не запрашивали сброс пароля, просто проигнорируйте это письмо — пароль останется прежним.</p>";
        await _email.SendAsync(email, "Сброс пароля — Юниран", body, cancellationToken);
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

// Step 2: consume the emailed token and set a new password.
public record ResetPasswordCommand(string Token, string NewPassword) : IRequest;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    public ResetPasswordCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new UnauthorizedAccessException("Ссылка для сброса пароля недействительна.");
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            throw new InvalidOperationException("Пароль должен содержать минимум 6 символов.");

        var hash = CreateInvitationCommandHandler.HashToken(request.Token);
        var user = await _db.AppUsers
            .FirstOrDefaultAsync(u => u.PasswordResetTokenHash == hash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Ссылка для сброса пароля недействительна или уже использована.");

        if (user.PasswordResetExpiresAt == null || user.PasswordResetExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Срок действия ссылки для сброса пароля истёк.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresAt = null;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
