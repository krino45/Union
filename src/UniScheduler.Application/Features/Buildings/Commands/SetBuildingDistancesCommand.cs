using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Buildings.Commands;

public record SetBuildingDistancesCommand(List<SetBuildingDistanceRequest> Distances) : IRequest;

public class SetBuildingDistancesCommandHandler : IRequestHandler<SetBuildingDistancesCommand>
{
    private readonly IApplicationDbContext _db;
    public SetBuildingDistancesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetBuildingDistancesCommand request, CancellationToken cancellationToken)
    {
        // Upsert: replace all distances for the pairs provided
        foreach (var d in request.Distances)
        {
            var existing = await _db.BuildingDistances
                .FirstOrDefaultAsync(x => x.FromBuildingId == d.FromBuildingId && x.ToBuildingId == d.ToBuildingId, cancellationToken);

            if (existing is null)
            {
                _db.BuildingDistances.Add(new BuildingDistance
                {
                    FromBuildingId = d.FromBuildingId,
                    ToBuildingId = d.ToBuildingId,
                    DistanceMeters = d.DistanceMeters
                });
            }
            else
            {
                existing.DistanceMeters = d.DistanceMeters;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
