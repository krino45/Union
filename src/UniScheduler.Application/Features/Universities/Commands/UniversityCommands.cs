using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Universities.Queries;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Universities.Commands;

public record CreateUniversityCommand(string Name, string ShortName, string? LogoUrl, string? City) : IRequest<UniversityDto>;
public record UpdateUniversityCommand(Guid Id, string Name, string ShortName, string? LogoUrl, string? City) : IRequest;
public record DeleteUniversityCommand(Guid Id) : IRequest;
public record AssignUniversityUserCommand(Guid UniversityId, Guid UserId, UniversityRole Role) : IRequest;
public record RevokeUniversityUserCommand(Guid UniversityId, Guid UserId) : IRequest;
public record GrantSelfUniversityAccessCommand(Guid UniversityId) : IRequest;

public class CreateUniversityCommandHandler : IRequestHandler<CreateUniversityCommand, UniversityDto>
{
    private readonly IApplicationDbContext _db;
    public CreateUniversityCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<UniversityDto> Handle(CreateUniversityCommand request, CancellationToken cancellationToken)
    {
        var university = new University
        {
            Name = request.Name,
            ShortName = request.ShortName,
            LogoUrl = request.LogoUrl,
            City = request.City
        };
        _db.Universities.Add(university);
        await _db.SaveChangesAsync(cancellationToken);
        return new UniversityDto(university.Id, university.Name, university.ShortName, university.LogoUrl, university.City);
    }
}

public class UpdateUniversityCommandHandler : IRequestHandler<UpdateUniversityCommand>
{
    private readonly IApplicationDbContext _db;
    public UpdateUniversityCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateUniversityCommand request, CancellationToken cancellationToken)
    {
        var university = await _db.Universities.FindAsync(new object[] { request.Id }, cancellationToken)
            ?? throw new NotFoundException(nameof(University), request.Id);
        university.Name = request.Name;
        university.ShortName = request.ShortName;
        university.LogoUrl = request.LogoUrl;
        university.City = request.City;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public class GrantSelfUniversityAccessCommandHandler : IRequestHandler<GrantSelfUniversityAccessCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public GrantSelfUniversityAccessCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(GrantSelfUniversityAccessCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");
        if (!_user.IsSuperAdmin)
            throw new ForbiddenException("Только суперадминистратор может самостоятельно получить доступ.");

        var existing = await _db.UserUniversityAccesses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.UniversityId == request.UniversityId, cancellationToken);
        if (existing != null) return;

        _db.UserUniversityAccesses.Add(new UserUniversityAccess
        {
            UserId = userId,
            UniversityId = request.UniversityId,
            Role = UniversityRole.Admin
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public class DeleteUniversityCommandHandler : IRequestHandler<DeleteUniversityCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteUniversityCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteUniversityCommand request, CancellationToken cancellationToken)
    {
        var university = await _db.Universities.FindAsync(new object[] { request.Id }, cancellationToken)
            ?? throw new NotFoundException(nameof(University), request.Id);
        _db.Universities.Remove(university);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public class AssignUniversityUserCommandHandler : IRequestHandler<AssignUniversityUserCommand>
{
    private readonly IApplicationDbContext _db;
    public AssignUniversityUserCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(AssignUniversityUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _db.UserUniversityAccesses
            .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.UniversityId == request.UniversityId, cancellationToken);

        if (existing != null)
        {
            existing.Role = request.Role;
        }
        else
        {
            _db.UserUniversityAccesses.Add(new UserUniversityAccess
            {
                UserId = request.UserId,
                UniversityId = request.UniversityId,
                Role = request.Role
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public class RevokeUniversityUserCommandHandler : IRequestHandler<RevokeUniversityUserCommand>
{
    private readonly IApplicationDbContext _db;
    public RevokeUniversityUserCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RevokeUniversityUserCommand request, CancellationToken cancellationToken)
    {
        var access = await _db.UserUniversityAccesses
            .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.UniversityId == request.UniversityId, cancellationToken)
            ?? throw new NotFoundException(nameof(UserUniversityAccess), $"{request.UniversityId}/{request.UserId}");
        _db.UserUniversityAccesses.Remove(access);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
