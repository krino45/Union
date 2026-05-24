using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.FloorPlan;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Buildings.Commands;

public record SaveFloorPlanCommand(Guid BuildingId, SaveFloorPlanRequest Request) : IRequest;

public class SaveFloorPlanCommandHandler : IRequestHandler<SaveFloorPlanCommand>
{
    private readonly IApplicationDbContext _db;
    public SaveFloorPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SaveFloorPlanCommand command, CancellationToken cancellationToken)
    {
        var oldConnections = await _db.EntranceConnections
            .Where(c => c.FromBuildingId == command.BuildingId)
            .ToListAsync(cancellationToken);
        _db.EntranceConnections.RemoveRange(oldConnections);

        var oldEdges = await _db.FloorPlanEdges
            .Where(e => e.BuildingId == command.BuildingId)
            .ToListAsync(cancellationToken);
        _db.FloorPlanEdges.RemoveRange(oldEdges);

        var oldNodes = await _db.FloorPlanNodes
            .Where(n => n.BuildingId == command.BuildingId)
            .ToListAsync(cancellationToken);
        _db.FloorPlanNodes.RemoveRange(oldNodes);

        var req = command.Request;

        // Map client-supplied IDs to server-generated nodes
        var nodeByClientId = new Dictionary<Guid, FloorPlanNode>();

        foreach (var n in req.Nodes)
        {
            var node = new FloorPlanNode
            {
                BuildingId = command.BuildingId,
                Floor = n.Floor,
                X = n.X,
                Y = n.Y,
                NodeType = n.NodeType,
                RoomId = n.RoomId,
                Label = n.Label
            };
            _db.FloorPlanNodes.Add(node);
            nodeByClientId[n.Id] = node;
        }

        foreach (var e in req.Edges)
        {
            if (!nodeByClientId.TryGetValue(e.FromNodeId, out var fromNode) ||
                !nodeByClientId.TryGetValue(e.ToNodeId, out var toNode)) continue;
            _db.FloorPlanEdges.Add(new FloorPlanEdge
            {
                BuildingId = command.BuildingId,
                FromNodeId = fromNode.Id,
                ToNodeId = toNode.Id,
                DistanceMeters = e.DistanceMeters
            });
        }

        foreach (var n in req.Nodes)
        {
            if (n.NodeType != FloorPlanNodeType.Entrance || n.Connections == null) continue;
            if (!nodeByClientId.TryGetValue(n.Id, out var node)) continue;
            foreach (var c in n.Connections)
            {
                if (c.ToBuildingId == command.BuildingId || c.DistanceMeters <= 0) continue;
                _db.EntranceConnections.Add(new EntranceConnection
                {
                    FromNode = node,
                    FromBuildingId = command.BuildingId,
                    ToBuildingId = c.ToBuildingId,
                    DistanceMeters = c.DistanceMeters
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        await BuildingDistanceRecomputer.RecomputeForBuildingAsync(_db, command.BuildingId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
