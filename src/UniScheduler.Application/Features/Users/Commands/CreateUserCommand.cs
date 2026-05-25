using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Users.Commands;

// SuperAdmin-only: create a username account (no email)
public record CreateUserCommand(
    string Username,
    string Password,
    Guid UniversityId,
    UniversityRole Role) : IRequest<CreateUserResult>;

public record CreateUserResult(Guid Id, string Username, string Role);

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IApplicationDbContext _db;
    public CreateUserCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var username = (request.Username ?? string.Empty).Trim();
        if (username.Length < 3)
            throw new InvalidOperationException("Логин должен содержать минимум 3 символа.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new InvalidOperationException("Пароль должен содержать минимум 6 символов.");

        if (await _db.AppUsers.AnyAsync(u => u.Username == username, cancellationToken))
            throw new InvalidOperationException("Пользователь с таким логином уже существует.");

        var universityExists = await _db.Universities.AnyAsync(u => u.Id == request.UniversityId, cancellationToken);
        if (!universityExists)
            throw new NotFoundException(nameof(University), request.UniversityId);

        var systemRole = request.Role == UniversityRole.Admin ? "Admin" : "Teacher";
        var user = new AppUser
        {
            Username = username,
            Email = null,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = systemRole
        };
        _db.AppUsers.Add(user);

        _db.UserUniversityAccesses.Add(new UserUniversityAccess
        {
            User = user,
            UniversityId = request.UniversityId,
            Role = request.Role
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new CreateUserResult(user.Id, user.Username, user.Role);
    }
}
