using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.FloorPlan;

// Translates a FloorPlan.FloorPlanJson snapshot into FloorPlanNodes/FloorPlanEdges tables
// for a building, replacing whatever is there. Used by Publish and Activate flows.
internal static class FloorPlanMaterializer
{
    public static async Task ReplaceAsync(IApplicationDbContext db, Guid buildingId, string snapshotJson, CancellationToken ct)
    {
        var oldEdges = await db.FloorPlanEdges.Where(e => e.BuildingId == buildingId).ToListAsync(ct);
        db.FloorPlanEdges.RemoveRange(oldEdges);

        var oldNodes = await db.FloorPlanNodes.Where(n => n.BuildingId == buildingId).ToListAsync(ct);
        db.FloorPlanNodes.RemoveRange(oldNodes);

        if (string.IsNullOrWhiteSpace(snapshotJson)) return;

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        opts.Converters.Add(new JsonStringEnumConverter());
        var snapshot = JsonSerializer.Deserialize<SaveFloorPlanRequest>(snapshotJson, opts);
        if (snapshot == null) return;

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
    }
}
