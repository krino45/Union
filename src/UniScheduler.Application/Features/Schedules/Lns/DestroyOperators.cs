using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Lns;

// All destroy operators emit a set of requirement indices to free. The optimizer expands
// the set to include parallel siblings (so H_par stays satisfiable) before calling the repair.

internal static class DestroyHelpers
{
    // Bucket members in random order until we reach the target size.
    public static HashSet<int> AccumulateToTarget<TKey>(
        Dictionary<TKey, List<int>> buckets, int target, Random rng) where TKey : notnull
    {
        var result = new HashSet<int>();
        if (buckets.Count == 0) return result;
        var keys = buckets.Keys.ToList();
        Shuffle(keys, rng);
        foreach (var k in keys)
        {
            foreach (var ri in buckets[k]) result.Add(ri);
            if (result.Count >= target) break;
        }
        return result;
    }

    public static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

// TIME op: free several teachers. Targets S2/S3.
public sealed class DestroyTeacherWeek : IDestroyOperator
{
    public string Name => "TeacherWeek";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byTeacher = new Dictionary<Guid, List<int>>();
        foreach (var (ri, entry) in ctx.EntryByRi)
        {
            if (!byTeacher.TryGetValue(entry.TeacherId, out var lst))
                byTeacher[entry.TeacherId] = lst = new List<int>();
            lst.Add(ri);
        }
        return DestroyHelpers.AccumulateToTarget(byTeacher, ctx.TargetDestroySize, ctx.Rng);
    }
}

// Pick a random (day, week) pair and free every ri scheduled on it. Targets S1/S5/S8.
public sealed class DestroyDay : IDestroyOperator
{
    public string Name => "Day";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        // (day, weekType) = list of ri
        var byDay = new Dictionary<(RussianDayOfWeek, WeekType), List<int>>();
        foreach (var (ri, entry) in ctx.EntryByRi)
        {
            var k = (entry.DayOfWeek, entry.WeekType);
            if (!byDay.TryGetValue(k, out var lst)) byDay[k] = lst = new List<int>();
            lst.Add(ri);
        }
        if (byDay.Count == 0) return new HashSet<int>();

        var keys = byDay.Keys.ToList();
        // Slight bias toward Saturday when SaturdayPenalty > 0.
        var saturday = keys.Where(k => k.Item1 == RussianDayOfWeek.Saturday).ToList();
        var pick = (saturday.Count > 0 && ctx.Rng.NextDouble() < 0.2)
            ? saturday[ctx.Rng.Next(saturday.Count)]
            : keys[ctx.Rng.Next(keys.Count)];
        return byDay[pick].ToHashSet();
    }
}

// TIME op: free 2-3 whole (day, week) at once. Heaviest time kick.
public sealed class DestroyMultiDay : IDestroyOperator
{
    public string Name => "MultiDay";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byDay = new Dictionary<(RussianDayOfWeek, WeekType), List<int>>();
        foreach (var (ri, entry) in ctx.EntryByRi)
        {
            var k = (entry.DayOfWeek, entry.WeekType);
            if (!byDay.TryGetValue(k, out var lst)) byDay[k] = lst = new List<int>();
            lst.Add(ri);
        }
        if (byDay.Count == 0) return new HashSet<int>();

        var keys = byDay.Keys.ToList();
        DestroyHelpers.Shuffle(keys, ctx.Rng);
        int take = Math.Min(keys.Count, 2 + ctx.Rng.Next(2)); // 2 or 3 day-buckets
        var result = new HashSet<int>();
        for (int i = 0; i < take; i++)
            foreach (var ri in byDay[keys[i]]) result.Add(ri);
        return result;
    }
}

// TIME op: free groups ENTIRE week. Targets S1/S5.
public sealed class DestroyGroupWeek : IDestroyOperator
{
    public string Name => "GroupWeek";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byGroup = new Dictionary<Guid, List<int>>();
        foreach (var (ri, entry) in ctx.EntryByRi)
            foreach (var sg in entry.StudentGroups)
            {
                if (!byGroup.TryGetValue(sg.StudentGroupId, out var lst))
                    byGroup[sg.StudentGroupId] = lst = new List<int>();
                lst.Add(ri);
            }
        return DestroyHelpers.AccumulateToTarget(byGroup, ctx.TargetDestroySize, ctx.Rng);
    }
}

// TIME op: free whole disciplines at once
public sealed class DestroySubject : IDestroyOperator
{
    public string Name => "Subject";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var bySubject = new Dictionary<Guid, List<int>>();
        foreach (var (ri, entry) in ctx.EntryByRi)
        {
            if (!bySubject.TryGetValue(entry.SubjectId, out var lst))
                bySubject[entry.SubjectId] = lst = new List<int>();
            lst.Add(ri);
        }
        return DestroyHelpers.AccumulateToTarget(bySubject, ctx.TargetDestroySize, ctx.Rng);
    }
}

// Free a random plan's ri set. Useful when one plan is dragging score and benefits from a full
// replan. May exceed TargetDestroySize for large plans.
public sealed class DestroyPlan : IDestroyOperator
{
    public string Name => "Plan";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byPlan = new Dictionary<Guid, List<int>>();
        foreach (var ri in ctx.EntryByRi.Keys)
        {
            if (!ctx.RiToPlanId.TryGetValue(ri, out var planId)) continue;
            if (!byPlan.TryGetValue(planId, out var lst)) byPlan[planId] = lst = new List<int>();
            lst.Add(ri);
        }
        if (byPlan.Count == 0) return new HashSet<int>();
        var keys = byPlan.Keys.ToList();
        var pick = keys[ctx.Rng.Next(keys.Count)];
        return byPlan[pick].ToHashSet();
    }
}

// Free K random ri. Generic diversification.
public sealed class DestroyRandomK : IDestroyOperator
{
    public string Name => "RandomK";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var pool = ctx.EntryByRi.Keys.ToList();
        if (pool.Count == 0) return new HashSet<int>();
        int k = Math.Min(pool.Count, ctx.TargetDestroySize);
        var picked = new HashSet<int>();
        while (picked.Count < k && picked.Count < pool.Count)
            picked.Add(pool[ctx.Rng.Next(pool.Count)]);
        return picked;
    }
}

// SPACE op: free every class sitting in a room whose TYPE doesn't permit its lesson type
public sealed class DestroyWrongRoom : IDestroyOperator
{
    public string Name => "WrongRoom";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        if (ctx.RoomAllowedLessonTypes is not { } allowed) return new HashSet<int>();
        var wrong = new List<int>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (e.IsOnline || !e.RoomId.HasValue) continue;
            if (e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;
            if (allowed.TryGetValue(e.RoomId.Value, out var ok) && !ok.Contains(e.LessonType))
                wrong.Add(ri);
        }
        if (wrong.Count <= ctx.TargetDestroySize) return wrong.ToHashSet();
        DestroyHelpers.Shuffle(wrong, ctx.Rng);
        return wrong.Take(ctx.TargetDestroySize).ToHashSet();
    }
}

// TIME op: free every class on a day its group cant attend, or on a slot its teacher is unavailable.
// The time analog of WrongRoom.
public sealed class DestroyBlockedSlot : IDestroyOperator
{
    public string Name => "BlockedSlot";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var gbd = ctx.GroupBlockedDays;
        var tbs = ctx.TeacherBlockedSlots;
        if (gbd == null && tbs == null) return new HashSet<int>();
        var bad = new List<int>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            bool violates = false;
            if (gbd != null)
                foreach (var sg in e.StudentGroups)
                    if (gbd.TryGetValue(sg.StudentGroupId, out var days) && days.Contains(e.DayOfWeek)) { violates = true; break; }
            if (!violates && tbs != null)
                foreach (int cw in CalWeeks(e.WeekType))
                    if (tbs.Contains((e.TeacherId, e.DayOfWeek, e.PairNumber, cw))) { violates = true; break; }
            if (violates) bad.Add(ri);
        }
        if (bad.Count <= ctx.TargetDestroySize) return bad.ToHashSet();
        DestroyHelpers.Shuffle(bad, ctx.Rng);
        return bad.Take(ctx.TargetDestroySize).ToHashSet();
    }

    private static int[] CalWeeks(WeekType wt) => wt switch
    {
        WeekType.Odd => new[] { 0 },
        WeekType.Even => new[] { 1 },
        _ => new[] { 0, 1 }
    };
}

// Free the ri whose entries contribute most to the current penalty. Heuristic - we attribute
// per-entry contribution from each soft component (S1 by group/day, S2 by teacher/day, S4 by
// adjacent-pair groups, S7 by pair index, S8 by Saturday occupancy, S9 by dept mismatch). The
// top K accumulating ri are returned.
public sealed class DestroyWorstK : IDestroyOperator
{
    private readonly SolverWeights weights;
    public DestroyWorstK(SolverWeights weights) { this.weights = weights; }
    public string Name => "WorstK";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        if (ctx.EntryByRi.Count == 0) return new HashSet<int>();
        var score = new Dictionary<int, long>(ctx.EntryByRi.Count);

        var byGroupDay = new Dictionary<(Guid g, RussianDayOfWeek d, WeekType wt), List<int>>();
        var byTeacherDay = new Dictionary<(Guid t, RussianDayOfWeek d, WeekType wt), List<int>>();

        foreach (var (ri, e) in ctx.EntryByRi)
        {
            score.TryAdd(ri, 0);
            // S7 - bake the per-pair penalty into the per-entry score directly.
            int p0 = e.PairNumber - 1;
            int s7 = p0 < 2 ? weights.EarlyPair * (2 - p0)
                  : p0 > 3 ? weights.LatePair  * (p0 - 3)
                  : weights.MiddlePair;
            score[ri] += s7;

            // S8 - Saturday penalty per entry.
            if (e.DayOfWeek == RussianDayOfWeek.Saturday) score[ri] += weights.SaturdayPenalty;

            // Bucket for S1/S5 (by group+day) and S2/S3 (by teacher+day).
            foreach (var sg in e.StudentGroups)
            {
                var k = (sg.StudentGroupId, e.DayOfWeek, e.WeekType);
                if (!byGroupDay.TryGetValue(k, out var lst)) byGroupDay[k] = lst = new List<int>();
                lst.Add(ri);
            }
            var tk = (e.TeacherId, e.DayOfWeek, e.WeekType);
            if (!byTeacherDay.TryGetValue(tk, out var tl)) byTeacherDay[tk] = tl = new List<int>();
            tl.Add(ri);
        }

        // S1 + S5: distribute group-day penalties to every entry in that group-day bucket.
        foreach (var ((_, _, _), list) in byGroupDay)
        {
            int sanPin = Math.Max(0, list.Count - 4) * weights.SanPinOverload;
            // Coarse S1 proxy: long days are likely to have windows. We don't reconstruct exact
            // gaps here; we just add a small per-extra-pair charge.
            int s1Proxy = Math.Max(0, list.Count - 3) * weights.StudentWindow / 2;
            int share = (sanPin + s1Proxy) / Math.Max(1, list.Count);
            foreach (var ri in list) score[ri] += share;
        }

        foreach (var ((_, _, _), list) in byTeacherDay)
        {
            int s3 = weights.ActiveDay; // one day-active charge per teacher-day
            int s2Proxy = Math.Max(0, list.Count - 3) * weights.TeacherWindow / 2;
            int share = (s3 + s2Proxy) / Math.Max(1, list.Count);
            foreach (var ri in list) score[ri] += share;
        }

        int take = ctx.MinDestroySize;
        return score.OrderByDescending(kv => kv.Value).Take(take).Select(kv => kv.Key).ToHashSet();
    }
}

// SPACE op: free several rooms worth of classes (up to the target) so the repair can rehome to better rooms
public sealed class DestroyRoomSpace : IDestroyOperator
{
    public string Name => "RoomSpace";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byRoom = new Dictionary<Guid, List<int>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (!e.RoomId.HasValue || e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;
            if (!byRoom.TryGetValue(e.RoomId.Value, out var lst)) byRoom[e.RoomId.Value] = lst = new List<int>();
            lst.Add(ri);
        }
        return DestroyHelpers.AccumulateToTarget(byRoom, ctx.TargetDestroySize, ctx.Rng);
    }
}

// SPACE op: free several rooms that all sit in same building.
public sealed class DestroyBuildingSpace : IDestroyOperator
{
    public string Name => "BuildingSpace";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byBuilding = new Dictionary<Guid, List<int>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (!e.RoomId.HasValue || e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;
            if (!ctx.RoomToBuilding.TryGetValue(e.RoomId.Value, out var bld)) continue;
            if (!byBuilding.TryGetValue(bld, out var lst)) byBuilding[bld] = lst = new List<int>();
            lst.Add(ri);
        }
        if (byBuilding.Count == 0) return new HashSet<int>();

        var buildings = byBuilding.Keys.ToList();
        var pick = buildings[ctx.Rng.Next(buildings.Count)];
        var reqs = byBuilding[pick];
        if (reqs.Count <= ctx.TargetDestroySize) return reqs.ToHashSet();
        var shuffled = reqs.ToList();
        DestroyHelpers.Shuffle(shuffled, ctx.Rng);
        return shuffled.Take(ctx.TargetDestroySize).ToHashSet();
    }
}

// SPACE op: pick a (group, day) with 2+ classes and free that group's classes that day, so the
// repair can co-locate them in nearby rooms (times fixed, distances on). Directly targets walking (S4).
public sealed class DestroyGroupDaySpace : IDestroyOperator
{
    public string Name => "GroupDaySpace";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byGroupDay = new Dictionary<(Guid g, RussianDayOfWeek d), List<int>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (!e.RoomId.HasValue || e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;
            foreach (var sg in e.StudentGroups)
            {
                var k = (sg.StudentGroupId, e.DayOfWeek);
                if (!byGroupDay.TryGetValue(k, out var lst)) byGroupDay[k] = lst = new List<int>();
                lst.Add(ri);
            }
        }
        var multi = byGroupDay.Where(kv => kv.Value.Count >= 2)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return DestroyHelpers.AccumulateToTarget(multi, ctx.TargetDestroySize, ctx.Rng);
    }
}

// SPACE op: free one groups whole week of rooms.
public sealed class DestroyGroupWeekSpace : IDestroyOperator
{
    public string Name => "GroupWeekSpace";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var byGroup = new Dictionary<Guid, List<int>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (!e.RoomId.HasValue || e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;
            foreach (var sg in e.StudentGroups)
            {
                if (!byGroup.TryGetValue(sg.StudentGroupId, out var lst))
                    byGroup[sg.StudentGroupId] = lst = new List<int>();
                lst.Add(ri);
            }
        }
        return DestroyHelpers.AccumulateToTarget(byGroup, ctx.TargetDestroySize, ctx.Rng);
    }
}
