using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Buildings.Queries;

public record GetBuildingsQuery : IRequest<List<BuildingDto>>;

public class GetBuildingsQueryHandler : IRequestHandler<GetBuildingsQuery, List<BuildingDto>>
{
    private readonly IApplicationDbContext _db;
    public GetBuildingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<BuildingDto>> Handle(GetBuildingsQuery request, CancellationToken cancellationToken)
        => await _db.Buildings
            .OrderBy(b => b.ShortCode)
            .Select(b => new BuildingDto(b.Id, b.ShortCode, b.Address, b.StairsDistancePerFloor))
            .ToListAsync(cancellationToken);
}
