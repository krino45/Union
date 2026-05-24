using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.FloorPlan;

// Translates a FloorPlan.FloorPlanJson snapshot into FloorPlanNodes/FloorPlanEdges/EntranceConnections
// tables for a building, replacing whatever is there, then recomputes derived BuildingDistances.
// Persists its own changes. Used by Publish and Activate flows.
internal static class FloorPlanMaterializer
{
    public static async Task ReplaceAsync(IApplicationDbContext db, Guid buildingId, string snapshotJson, CancellationToken ct)
    {
        var oldConnections = await db.EntranceConnections.Where(c => c.FromBuildingId == buildingId).ToListAsync(ct);
        db.EntranceConnections.RemoveRange(oldConnections);

        var oldEdges = await db.FloorPlanEdges.Where(e => e.BuildingId == buildingId).ToListAsync(ct);
        db.FloorPlanEdges.RemoveRange(oldEdges);

        var oldNodes = await db.FloorPlanNodes.Where(n => n.BuildingId == buildingId).ToListAsync(ct);
        db.FloorPlanNodes.RemoveRange(oldNodes);

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        opts.Converters.Add(new JsonStringEnumConverter());
        var snapshot = string.IsNullOrWhiteSpace(snapshotJson)
            ? null
            : JsonSerializer.Deserialize<SaveFloorPlanRequest>(snapshotJson, opts);

        if (snapshot != null)
        {
            var nodeByClientId = new Dictionary<Guid, FloorPlanNode>();
            foreach (var n in snapshot.Nodes)
            {
                var node = new FloorPlanNode
                {
                    BuildingId = buildingId,
                    Floor = n.Floor,
                    X = n.X,
                    Y = n.Y,
                    NodeType = n.NodeType,
                    RoomId = n.RoomId,
                    Label = n.Label
                };
                db.FloorPlanNodes.Add(node);
                nodeByClientId[n.Id] = node;
            }

            foreach (var e in snapshot.Edges)
            {
                if (!nodeByClientId.TryGetValue(e.FromNodeId, out var from) ||
                    !nodeByClientId.TryGetValue(e.ToNodeId, out var to)) continue;
                db.FloorPlanEdges.Add(new FloorPlanEdge
                {
                    BuildingId = buildingId,
                    FromNodeId = from.Id,
                    ToNodeId = to.Id,
                    DistanceMeters = e.DistanceMeters
                });
            }

            foreach (var n in snapshot.Nodes)
            {
                if (n.NodeType != FloorPlanNodeType.Entrance || n.Connections == null) continue;
                if (!nodeByClientId.TryGetValue(n.Id, out var node)) continue;
                foreach (var c in n.Connections)
                {
                    if (c.ToBuildingId == buildingId || c.DistanceMeters <= 0) continue;
                    db.EntranceConnections.Add(new EntranceConnection
                    {
                        FromNode = node,
                        FromBuildingId = buildingId,
                        ToBuildingId = c.ToBuildingId,
                        DistanceMeters = c.DistanceMeters
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);

        await BuildingDistanceRecomputer.RecomputeForBuildingAsync(db, buildingId, ct);
        await db.SaveChangesAsync(ct);
    }
}
