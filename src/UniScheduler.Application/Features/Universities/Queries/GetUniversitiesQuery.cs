using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Universities.Queries;

public record UniversityDto(Guid Id, string Name, string ShortName, string? LogoUrl, string? City);
public record UniversityUserDto(Guid UserId, string Username, string SystemRole, UniversityRole UniversityRole);

public record GetUniversitiesQuery : IRequest<List<UniversityDto>>;
public record GetUniversityUsersQuery(Guid UniversityId) : IRequest<List<UniversityUserDto>>;

public class GetUniversitiesQueryHandler : IRequestHandler<GetUniversitiesQuery, List<UniversityDto>>
{
    private readonly IApplicationDbContext _db;
    public GetUniversitiesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<UniversityDto>> Handle(GetUniversitiesQuery request, CancellationToken cancellationToken)
        => await _db.Universities
            .OrderBy(u => u.Name)
            .Select(u => new UniversityDto(u.Id, u.Name, u.ShortName, u.LogoUrl, u.City))
            .ToListAsync(cancellationToken);
}

public class GetUniversityUsersQueryHandler : IRequestHandler<GetUniversityUsersQuery, List<UniversityUserDto>>
{
    private readonly IApplicationDbContext _db;
    public GetUniversityUsersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<UniversityUserDto>> Handle(GetUniversityUsersQuery request, CancellationToken cancellationToken)
        => await _db.UserUniversityAccesses
            .Where(a => a.UniversityId == request.UniversityId)
            .Select(a => new UniversityUserDto(a.UserId, a.User.Username, a.User.Role, a.Role))
            .ToListAsync(cancellationToken);
}
