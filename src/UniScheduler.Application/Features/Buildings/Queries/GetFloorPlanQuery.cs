using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Buildings.Queries;

public record GetFloorPlanQuery(Guid BuildingId) : IRequest<FloorPlanDto>;

public class GetFloorPlanQueryHandler : IRequestHandler<GetFloorPlanQuery, FloorPlanDto>
{
    private readonly IApplicationDbContext _db;
    public GetFloorPlanQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<FloorPlanDto> Handle(GetFloorPlanQuery request, CancellationToken cancellationToken)
    {
        var connectionsByNode = (await _db.EntranceConnections
                .Where(c => c.FromBuildingId == request.BuildingId)
                .Select(c => new { c.FromNodeId, c.ToBuildingId, c.DistanceMeters })
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.FromNodeId)
            .ToDictionary(g => g.Key, g => g.Select(c => new EntranceConnectionDto(c.ToBuildingId, c.DistanceMeters)).ToList());

        var rawNodes = await _db.FloorPlanNodes
            .Where(n => n.BuildingId == request.BuildingId)
            .Select(n => new { n.Id, n.BuildingId, n.Floor, n.X, n.Y, n.NodeType, n.RoomId, n.Label })
            .ToListAsync(cancellationToken);

        var nodes = rawNodes
            .Select(n => new FloorPlanNodeDto(n.Id, n.BuildingId, n.Floor, n.X, n.Y, n.NodeType, n.RoomId, n.Label,
                connectionsByNode.TryGetValue(n.Id, out var conns) ? conns : new List<EntranceConnectionDto>()))
            .ToList();

        var edges = await _db.FloorPlanEdges
            .Where(e => e.BuildingId == request.BuildingId)
            .Select(e => new FloorPlanEdgeDto(e.Id, e.BuildingId, e.FromNodeId, e.ToNodeId, e.DistanceMeters))
            .ToListAsync(cancellationToken);

        return new FloorPlanDto(request.BuildingId, nodes, edges);
    }
}
