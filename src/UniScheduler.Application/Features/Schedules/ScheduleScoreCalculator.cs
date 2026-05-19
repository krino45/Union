using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules;

/// <summary>
/// Distance/timing data needed for the S4 (walking) penalty.
/// Build via <see cref="ScheduleScoreCalculator.BuildScoreContext"/>.
/// </summary>
public record ScoreContext(
    IReadOnlyDictionary<(Guid, Guid), int> RoomDistances,
    IReadOnlyDictionary<(Guid, Guid), int> BuildingDistances,
    IReadOnlyDictionary<Guid, Guid> RoomToBuilding,
    IReadOnlyList<int> BreakMinutes
);

/// <summary>
/// Replicates the solvers soft-penalty objective function against a given assignment.
/// Hard conflict add 1000 each since the solver would never produce em.
/// </summary>
public static class ScheduleScoreCalculator
{
    private const double WalkSpeedMperMin = 80.0;

    /// <summary>
    /// Builds the context needed for S4 from raw DB entities.
    /// All parameters are loaded cheaply, pass empty collections when not available.
    /// </summary>
    public static ScoreContext BuildScoreContext(
        IEnumerable<FloorPlanNode> nodes,
        IEnumerable<FloorPlanEdge> edges,
        IEnumerable<BuildingDistance> buildingDistances,
        IEnumerable<Room> rooms,
        IEnumerable<PairTimeSlot> pairSlots)
    {
        var roomDists = ComputeRoomDistances(nodes, edges);

        var bldDists = new Dictionary<(Guid, Guid), int>();
        foreach (var d in buildingDistances)
        {
            bldDists[(d.FromBuildingId, d.ToBuildingId)] = d.DistanceMeters;
            bldDists[(d.ToBuildingId, d.FromBuildingId)] = d.DistanceMeters;
        }

        var roomToBuilding = rooms.ToDictionary(r => r.Id, r => r.BuildingId);
        var breakMins = ComputeBreakMinutes(pairSlots);

        return new ScoreContext(roomDists, bldDists, roomToBuilding, breakMins);
    }

    public static int Compute(IReadOnlyList<ScheduleEntry> entries, ScoreContext? ctx = null)
    {
        int score = 0;

        // Hard conflicts — +1000 per unique conflicted slot
        var cfSeen = new HashSet<string>();
        for (int i = 0; i < entries.Count; i++)
        for (int j = i + 1; j < entries.Count; j++)
        {
            var a = entries[i]; var b = entries[j];
            if (!Overlaps(a, b)) continue;

            if (!a.IsOnline && !b.IsOnline && a.RoomId.HasValue && a.RoomId == b.RoomId)
                if (cfSeen.Add($"r:{a.RoomId}:{a.DayOfWeek}:{a.PairNumber}"))
                    score += 1000;

            if (a.TeacherId == b.TeacherId)
                if (cfSeen.Add($"t:{a.TeacherId}:{a.DayOfWeek}:{a.PairNumber}"))
                    score += 1000;

            var ag = a.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            var bg = b.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            foreach (var gid in ag.Intersect(bg))
                if (cfSeen.Add($"g:{gid}:{a.DayOfWeek}:{a.PairNumber}"))
                    score += 1000;
        }

        var byGroup   = GroupBy(entries, e => e.StudentGroups.Select(sg => sg.StudentGroupId));
        var byTeacher = entries.GroupBy(e => e.TeacherId).ToDictionary(g => g.Key, g => g.ToList());

        var weekVariants = new[] { WeekType.Odd, WeekType.Even };

        foreach (var (_, ge) in byGroup)
        foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
        foreach (var wv in weekVariants)
        {
            var slot = ge
                .Where(e => e.DayOfWeek == day && Affects(e.WeekType, wv))
                .ToList();
            if (slot.Count == 0) continue;

            var pairs = slot.Select(e => e.PairNumber).Distinct().OrderBy(p => p).ToList();

            // S3: active group-day  (+60)
            score += 60;

            // S1: student windows  (+100 per gap)
            if (pairs.Count >= 2)
                score += (pairs[^1] - pairs[0] + 1 - pairs.Count) * 100;

            // S5: SanPIN daily overload  (+300 per pair over 4)
            score += Math.Max(0, pairs.Count - 4) * 300;

            // S6: consecutive same (subject, lessonType)  (+70 per consecutive pair)
            foreach (var grp in slot.GroupBy(e => (e.SubjectId, e.LessonType)))
            {
                var sp = grp.Select(e => e.PairNumber).Distinct().OrderBy(p => p).ToList();
                for (int k = 0; k < sp.Count - 1; k++)
                    if (sp[k + 1] == sp[k] + 1) score += 70;
            }

            // S4: walking penalty for consecutive pairs
            if (ctx != null)
            {
                for (int pi = 0; pi < pairs.Count - 1; pi++)
                {
                    int p1 = pairs[pi], p2 = pairs[pi + 1];
                    if (p2 != p1 + 1) continue;

                    // breakMinutes[i] = break between pair i+1 and pair i+2 (1-indexed pairs)
                    int breakIdx = p1 - 1;
                    if (breakIdx >= ctx.BreakMinutes.Count) continue;
                    double allowedTravelMin = ctx.BreakMinutes[breakIdx];
                    if (allowedTravelMin <= 0) continue;

                    var atP1 = slot.Where(e => e.PairNumber == p1 && !e.IsOnline && e.RoomId.HasValue).ToList();
                    var atP2 = slot.Where(e => e.PairNumber == p2 && !e.IsOnline && e.RoomId.HasValue).ToList();

                    foreach (var ea in atP1)
                    foreach (var eb in atP2)
                    {
                        int dist = TravelDistance(ea.RoomId!.Value, eb.RoomId!.Value, ctx);
                        if (dist == 0) continue;
                        double walkMins = dist / WalkSpeedMperMin;
                        if (walkMins > allowedTravelMin) continue; // hard constraint, not S4

                        long penalty = Math.Max(1L, (long)(walkMins / allowedTravelMin * 120));
                        score += (int)penalty;
                    }
                }
            }
        }

        // S2: teacher windows  (+80 per gap)
        foreach (var (_, te) in byTeacher)
        foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
        foreach (var wv in weekVariants)
        {
            var pairs = te
                .Where(e => e.DayOfWeek == day && Affects(e.WeekType, wv))
                .Select(e => e.PairNumber).Distinct().OrderBy(p => p).ToList();
            if (pairs.Count >= 2)
                score += (pairs[^1] - pairs[0] + 1 - pairs.Count) * 80;
        }

        return score;
    }

    private static int TravelDistance(Guid roomA, Guid roomB, ScoreContext ctx)
    {
        if (!ctx.RoomToBuilding.TryGetValue(roomA, out var bldA) ||
            !ctx.RoomToBuilding.TryGetValue(roomB, out var bldB))
            return 0;
        if (bldA == bldB)
            return ctx.RoomDistances.TryGetValue((roomA, roomB), out int d) ? d : 0;
        return ctx.BuildingDistances.TryGetValue((bldA, bldB), out int bd) ? bd : int.MaxValue / 2;
    }

    // Runs Dijkstra on the floor plan graph; returns room-to-room distances.
    internal static IReadOnlyDictionary<(Guid, Guid), int> ComputeRoomDistances(
        IEnumerable<FloorPlanNode> nodes, IEnumerable<FloorPlanEdge> edges)
    {
        var nodeList = nodes.ToList();
        if (nodeList.Count == 0) return new Dictionary<(Guid, Guid), int>();

        var adj = nodeList.ToDictionary(n => n.Id, _ => new List<(Guid, int)>());
        foreach (var e in edges)
        {
            if (adj.ContainsKey(e.FromNodeId) && adj.ContainsKey(e.ToNodeId))
            {
                adj[e.FromNodeId].Add((e.ToNodeId, e.DistanceMeters));
                adj[e.ToNodeId].Add((e.FromNodeId, e.DistanceMeters));
            }
        }

        var roomNodes = nodeList
            .Where(n => n.NodeType == FloorPlanNodeType.Room && n.RoomId.HasValue)
            .ToList();

        var result = new Dictionary<(Guid, Guid), int>();
        var allIds = nodeList.Select(n => n.Id).ToArray();

        foreach (var src in roomNodes)
        {
            var dist = Dijkstra(src.Id, adj, allIds);
            foreach (var tgt in roomNodes)
            {
                if (tgt.Id == src.Id) continue;
                if (dist.TryGetValue(tgt.Id, out int d) && d < int.MaxValue / 2)
                    result[(src.RoomId!.Value, tgt.RoomId!.Value)] = d;
            }
        }
        return result;
    }

    // Returns break[i] = minutes between pair i+1 and pair i+2 (pairs are 1-indexed).
    internal static List<int> ComputeBreakMinutes(IEnumerable<PairTimeSlot> pairSlots)
    {
        var ordered = pairSlots.OrderBy(s => s.PairNumber).ToList();
        var breaks = new List<int>();
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var gap = (int)(ordered[i + 1].StartTime - ordered[i].EndTime).TotalMinutes;
            breaks.Add(Math.Max(0, gap));
        }
        return breaks;
    }

    private static Dictionary<Guid, int> Dijkstra(Guid source, Dictionary<Guid, List<(Guid, int)>> adj, Guid[] allIds)
    {
        var dist = allIds.ToDictionary(id => id, _ => int.MaxValue);
        dist[source] = 0;
        var pq = new PriorityQueue<Guid, int>();
        pq.Enqueue(source, 0);
        while (pq.TryDequeue(out var u, out int d))
        {
            if (d > dist[u]) continue;
            foreach (var (v, w) in adj[u])
            {
                int nd = dist[u] + w;
                if (nd < dist[v]) { dist[v] = nd; pq.Enqueue(v, nd); }
            }
        }
        return dist;
    }

    private static bool Overlaps(ScheduleEntry a, ScheduleEntry b)
    {
        if (a.DayOfWeek != b.DayOfWeek || a.PairNumber != b.PairNumber) return false;
        return a.WeekType == WeekType.Both || b.WeekType == WeekType.Both || a.WeekType == b.WeekType;
    }

    private static bool Affects(WeekType wt, WeekType variant)
        => wt == WeekType.Both || wt == variant;

    private static Dictionary<Guid, List<ScheduleEntry>> GroupBy(
        IReadOnlyList<ScheduleEntry> entries,
        Func<ScheduleEntry, IEnumerable<Guid>> keySelector)
    {
        var result = new Dictionary<Guid, List<ScheduleEntry>>();
        foreach (var e in entries)
        foreach (var key in keySelector(e))
        {
            if (!result.TryGetValue(key, out var list))
                result[key] = list = [];
            list.Add(e);
        }
        return result;
    }
}
