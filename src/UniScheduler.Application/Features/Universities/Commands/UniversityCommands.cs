using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Universities.Queries;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Universities.Commands;

public record CreateUniversityCommand(string Name, string ShortName, string? LogoUrl) : IRequest<UniversityDto>;
public record UpdateUniversityCommand(Guid Id, string Name, string ShortName, string? LogoUrl) : IRequest;
public record DeleteUniversityCommand(Guid Id) : IRequest;
public record AssignUniversityUserCommand(Guid UniversityId, Guid UserId, UniversityRole Role) : IRequest;
public record RevokeUniversityUserCommand(Guid UniversityId, Guid UserId) : IRequest;

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
            LogoUrl = request.LogoUrl
        };
        _db.Universities.Add(university);
        await _db.SaveChangesAsync(cancellationToken);
        return new UniversityDto(university.Id, university.Name, university.ShortName, university.LogoUrl);
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
