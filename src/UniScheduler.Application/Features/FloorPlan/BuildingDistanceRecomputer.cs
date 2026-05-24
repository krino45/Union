using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.FloorPlan;

// Rebuilds the derived BuildingDistance rows for every pair involving `buildingId`,
// taking the shortest EntranceConnection between the two buildings (either direction).
// Stored symmetrically so directional lookups by the validator/scheduler both resolve.
internal static class BuildingDistanceRecomputer
{
    public static async Task RecomputeForBuildingAsync(IApplicationDbContext db, Guid buildingId, CancellationToken ct)
    {
        var conns = await db.EntranceConnections
            .Where(c => c.FromBuildingId == buildingId || c.ToBuildingId == buildingId)
            .ToListAsync(ct);

        var minByOther = new Dictionary<Guid, int>();
        foreach (var c in conns)
        {
            var other = c.FromBuildingId == buildingId ? c.ToBuildingId : c.FromBuildingId;
            if (other == buildingId) continue;
            if (!minByOther.TryGetValue(other, out var cur) || c.DistanceMeters < cur)
                minByOther[other] = c.DistanceMeters;
        }

        var existing = await db.BuildingDistances
            .Where(d => d.FromBuildingId == buildingId || d.ToBuildingId == buildingId)
            .ToListAsync(ct);

        // Drop pairs that no longer have any connection.
        foreach (var d in existing)
        {
            var other = d.FromBuildingId == buildingId ? d.ToBuildingId : d.FromBuildingId;
            if (!minByOther.ContainsKey(other))
                db.BuildingDistances.Remove(d);
        }

        foreach (var (other, meters) in minByOther)
        {
            Upsert(db, existing, buildingId, other, meters);
            Upsert(db, existing, other, buildingId, meters);
        }
    }

    private static void Upsert(IApplicationDbContext db, List<BuildingDistance> existing, Guid from, Guid to, int meters)
    {
        var row = existing.FirstOrDefault(d => d.FromBuildingId == from && d.ToBuildingId == to);
        if (row == null)
        {
            row = new BuildingDistance { FromBuildingId = from, ToBuildingId = to, DistanceMeters = meters };
            db.BuildingDistances.Add(row);
            existing.Add(row);
        }
        else
        {
            row.DistanceMeters = meters;
        }
    }
}
