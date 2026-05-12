using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Buildings.Queries;

public record GetBuildingDistancesQuery : IRequest<List<BuildingDistanceDto>>;

public class GetBuildingDistancesQueryHandler : IRequestHandler<GetBuildingDistancesQuery, List<BuildingDistanceDto>>
{
    private readonly IApplicationDbContext _db;
    public GetBuildingDistancesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<BuildingDistanceDto>> Handle(GetBuildingDistancesQuery request, CancellationToken cancellationToken)
        => await _db.BuildingDistances
            .Select(d => new BuildingDistanceDto(
                d.FromBuildingId,
                d.ToBuildingId,
                d.DistanceMeters,
                d.DistanceMeters / 80.0,
                d.DistanceMeters / 80.0 > 10.0))
            .ToListAsync(cancellationToken);
}
