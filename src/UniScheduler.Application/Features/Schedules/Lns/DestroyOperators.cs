using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;
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
    public double Factor => 2.0; // 2-3 whole day-buckets - heavy af

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
    public double Factor => 1.75; // whole groups weeks freed

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

// TIME op. Destroy a full student plan and rebuild it. Can take a while
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

// TIME op: generic diversification. Picks random seed reqs + a bit around them.
public sealed class DestroyRandomK : IDestroyOperator
{
    public string Name => "RandomK";
    public RepairAxis Axis => RepairAxis.Time;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var pool = ctx.EntryByRi.Keys.ToList();
        if (pool.Count == 0) return new HashSet<int>();

        var byGroupDay = new Dictionary<(Guid, RussianDayOfWeek), List<int>>();
        var byTeacherDay = new Dictionary<(Guid, RussianDayOfWeek), List<int>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            var tk = (e.TeacherId, e.DayOfWeek);
            if (!byTeacherDay.TryGetValue(tk, out var tl)) byTeacherDay[tk] = tl = new List<int>();
            tl.Add(ri);
            foreach (var sg in e.StudentGroups)
            {
                var gk = (sg.StudentGroupId, e.DayOfWeek);
                if (!byGroupDay.TryGetValue(gk, out var gl)) byGroupDay[gk] = gl = new List<int>();
                gl.Add(ri);
            }
        }

        var result = new HashSet<int>();
        int guard = 0, cap = pool.Count * 2;
        while (result.Count < ctx.TargetDestroySize && guard++ < cap)
        {
            var e = ctx.EntryByRi[pool[ctx.Rng.Next(pool.Count)]];
            if (byTeacherDay.TryGetValue((e.TeacherId, e.DayOfWeek), out var tl))
                foreach (var ri in tl) result.Add(ri);
            foreach (var sg in e.StudentGroups)
                if (byGroupDay.TryGetValue((sg.StudentGroupId, e.DayOfWeek), out var gl))
                    foreach (var ri in gl) result.Add(ri);
        }
        return result;
    }
}

// SPACE op: free every class sitting in a room whose TYPE doesn't permit its lesson type
public sealed class DestroyWrongRoom : IDestroyOperator
{
    public string Name => "WrongRoom";
    // Full (not Space): a class bound to a specific room may need to change BOTH its room and its
    // time slot. If the bound room is busy at the class's current slot, a room-only (Space) repair
    // can't relocate it and it spills to overflow. Full lets it find a slot where the room is free;
    // we additionally free whoever currently occupies the bound room at that slot so the repair can
    // evict them into another room.
    public RepairAxis Axis => RepairAxis.Full;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var allowed = ctx.RoomAllowedLessonTypes;
        var bindings = ctx.ScoreCtx.SubjectRoomBindings;
        if (allowed == null && bindings == null) return new HashSet<int>();

        // Occupants indexed by room, so a bound class can evict whoever sits in its target room.
        var byRoom = new Dictionary<Guid, List<(int ri, ScheduleEntry e)>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (e.IsOnline || e.RoomId is not { } rid || rid == SchedulerSentinels.OverflowRoomId) continue;
            if (!byRoom.TryGetValue(rid, out var lst)) byRoom[rid] = lst = new();
            lst.Add((ri, e));
        }

        var violators = new List<(int ri, ScheduleEntry e, IReadOnlySet<Guid>? bound)>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (e.IsOnline || !e.RoomId.HasValue) continue;
            if (e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;

            // A hard (subject, lessonType) -> room binding overrides the room-type check.
            if (bindings != null &&
                bindings.TryGetValue((e.SubjectId, e.LessonType), out var bound) && bound.Count > 0)
            {
                if (!bound.Contains(e.RoomId.Value)) violators.Add((ri, e, bound));
                continue;
            }
            if (allowed != null &&
                allowed.TryGetValue(e.RoomId.Value, out var ok) && !ok.Contains(e.LessonType))
                violators.Add((ri, e, null));
        }
        if (violators.Count == 0) return new HashSet<int>();

        if (violators.Count > ctx.TargetDestroySize)
        {
            DestroyHelpers.Shuffle(violators, ctx.Rng);
            violators = violators.Take(ctx.TargetDestroySize).ToList();
        }

        var result = new HashSet<int>();
        foreach (var (ri, e, bound) in violators)
        {
            result.Add(ri);
            if (bound == null) continue;
            // Free the current occupants of the bound rooms at this class's slot so the repair can
            // make space for it there (they get re-placed into other compatible rooms).
            foreach (var roomId in bound)
                if (byRoom.TryGetValue(roomId, out var occupants))
                    foreach (var (ori, oe) in occupants)
                        if (ori != ri && SameSlot(oe, e)) result.Add(ori);
        }
        return result;
    }

    private static bool SameSlot(ScheduleEntry a, ScheduleEntry b)
        => a.DayOfWeek == b.DayOfWeek && a.PairNumber == b.PairNumber
           && (a.WeekType == b.WeekType || a.WeekType == WeekType.Both || b.WeekType == WeekType.Both);
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
        int maxPair = ctx.ScoreCtx.MaxPairNumber;
        int daysPerWeek = ctx.ScoreCtx.DaysPerWeek;
        if (gbd == null && tbs == null && maxPair <= 0 && daysPerWeek <= 0) return new HashSet<int>();
        var bad = new List<int>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            bool violates = false;
            // Out-of-grid placement after a settings change (shrunk pair grid / dropped day).
            if (maxPair > 0 && e.PairNumber > maxPair) violates = true;
            else if (daysPerWeek > 0 && (int)e.DayOfWeek > daysPerWeek) violates = true;
            if (!violates && gbd != null)
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
        WeekType.Odd => [0],
        WeekType.Even => [1],
        _ => [0, 1]
    };
}

// TIME op: the time analog of WorstDistanceSpace. Rank group-days by their penalty and free the worst ones.
public sealed class DestroyWorstK(SolverWeights weights) : IDestroyOperator
{
    public string Name => "WorstK";
    public RepairAxis Axis => RepairAxis.Time;
    public double Factor => 1.5; // hopefully enough to find the optimal solution

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        if (ctx.EntryByRi.Count == 0) return [];

        var penaltyByGroupDay = ScheduleScoreCalculator.Score_StudentDayPenaltyByGroupDay(ctx.Incumbent, weights);
        if (penaltyByGroupDay.Count == 0) return [];

        var byGroupDay = new Dictionary<(Guid g, RussianDayOfWeek d), List<int>>();
        var byTeacherDay = new Dictionary<(Guid t, RussianDayOfWeek d), List<int>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            var tk = (e.TeacherId, e.DayOfWeek);
            if (!byTeacherDay.TryGetValue(tk, out var tl)) byTeacherDay[tk] = tl = [];
            tl.Add(ri);
            foreach (var sg in e.StudentGroups)
            {
                var k = (sg.StudentGroupId, e.DayOfWeek);
                if (!byGroupDay.TryGetValue(k, out var lst)) byGroupDay[k] = lst = [];
                lst.Add(ri);
            }
        }

        var ranked = byGroupDay
            .Where(kv => penaltyByGroupDay.ContainsKey(kv.Key))
            .Select(kv => (kv.Value, Score: penaltyByGroupDay[kv.Key]))
            .OrderByDescending(x => x.Score)
            .ToList();
        if (ranked.Count == 0) return [];

        var band = Math.Min(ranked.Count, Math.Max(3, ranked.Count / 4));
        var top = ranked.Take(band).Select(x => x.Value).ToList();
        DestroyHelpers.Shuffle(top, ctx.Rng);

        var result = new HashSet<int>();
        foreach (var bucket in top)
        {
            foreach (var ri in bucket)
            {
                result.Add(ri);
                var e = ctx.EntryByRi[ri];
                if (!byTeacherDay.TryGetValue((e.TeacherId, e.DayOfWeek), out var tl)) continue;
                foreach (var tri in tl) result.Add(tri);
            }
            if (result.Count >= ctx.TargetDestroySize) break;
        }
        return result;
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
    public double Factor => 1.5; // co-locates several (group, day) regions at once

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
    public double Factor => 1.75; // one group's whole week of rooms freed

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

// SPACE op: the space analog of WorstK. Rank (group, day) regions by real walking dists and free the worst regions
public sealed class DestroyWorstDistanceSpace(SolverWeights weights) : IDestroyOperator
{
    public string Name => "WorstDistanceSpace";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        if (ctx.EntryByRi.Count == 0) return [];

        var walkByGroupDay = ScheduleScoreCalculator.Score_S4_WalkingByGroupDay(ctx.Incumbent, ctx.ScoreCtx, weights);
        if (walkByGroupDay.Count == 0) return [];

        var byGroupDay = new Dictionary<(Guid g, RussianDayOfWeek d), List<int>>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (e.IsOnline || !e.RoomId.HasValue || e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;
            foreach (var sg in e.StudentGroups)
            {
                var k = (sg.StudentGroupId, e.DayOfWeek);
                if (!byGroupDay.TryGetValue(k, out var lst)) byGroupDay[k] = lst = [];
                lst.Add(ri);
            }
        }

        var ranked = byGroupDay
            .Where(kv => walkByGroupDay.ContainsKey(kv.Key))
            .Select(kv => (kv.Value, Walk: walkByGroupDay[kv.Key]))
            .OrderByDescending(x => x.Walk)
            .ToList();
        if (ranked.Count == 0) return [];

        var band = Math.Min(ranked.Count, Math.Max(3, ranked.Count / 4));
        var top = ranked.Take(band).Select(x => x.Value).ToList();
        DestroyHelpers.Shuffle(top, ctx.Rng);

        var result = new HashSet<int>();
        foreach (var bucket in top)
        {
            foreach (var ri in bucket) result.Add(ri);
            if (result.Count >= ctx.TargetDestroySize) break;
        }

        return result;
    }
}

// SPACE op: destroys overflow rooms. Becomes a forced operator if there's any overflow rooms left from the previous kick.
public sealed class DestroyOverflowRooms : IDestroyOperator
{
    public string Name => "Overflow";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        if (ctx.EntryByRi.Count == 0) return [];
        var lst = new List<int>();
        foreach (var (ri, e) in ctx.EntryByRi)
        {
            if (e.RoomId != null && e.RoomId.Value == SchedulerSentinels.OverflowRoomId)
                lst.Add(ri);
        }

        DestroyHelpers.Shuffle(lst, ctx.Rng);

        var result = new HashSet<int>();
        foreach (var ri in lst)
        {
            result.Add(ri);
            if (result.Count >= ctx.TargetDestroySize) break;
        }

        return result;
    }
}

