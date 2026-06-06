using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Lns;

// All destroy operators emit a set of requirement indices to free. The optimizer expands
// the set to include parallel siblings (so H_par stays satisfiable) before calling the repair.

// Pick a teacher at random and free every ri assigned to them. Targets S2/S3 directly.
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
        if (byTeacher.Count == 0) return new HashSet<int>();

        // Prefer teachers with multiple lessons (single-lesson teacher would barely move the needle).
        var teachers = byTeacher.Where(kv => kv.Value.Count >= 2).Select(kv => kv.Key).ToList();
        if (teachers.Count == 0) teachers = byTeacher.Keys.ToList();

        var picked = teachers[ctx.Rng.Next(teachers.Count)];
        return byTeacher[picked].ToHashSet();
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

        int take = Math.Max(ctx.MinDestroySize, ctx.TargetDestroySize);
        return score.OrderByDescending(kv => kv.Value).Take(take).Select(kv => kv.Key).ToHashSet();
    }
}

// SPACE op: pick a room (weighted by occupancy) and free every req sitting in it, so the repair can
// re-home that room's load to better rooms with times fixed and full distances on. "Delete a room's
// schedule." Picking via a random entry weights selection toward busier rooms.
public sealed class DestroyRoomSpace : IDestroyOperator
{
    public string Name => "RoomSpace";
    public RepairAxis Axis => RepairAxis.Space;

    public HashSet<int> SelectToDestroy(LnsKickContext ctx)
    {
        var withRoom = ctx.EntryByRi.Where(kv => kv.Value.RoomId.HasValue).ToList();
        if (withRoom.Count == 0) return new HashSet<int>();
        var pickRoom = withRoom[ctx.Rng.Next(withRoom.Count)].Value.RoomId!.Value;
        return withRoom.Where(kv => kv.Value.RoomId == pickRoom).Select(kv => kv.Key).ToHashSet();
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
            if (!e.RoomId.HasValue) continue;
            foreach (var sg in e.StudentGroups)
            {
                var k = (sg.StudentGroupId, e.DayOfWeek);
                if (!byGroupDay.TryGetValue(k, out var lst)) byGroupDay[k] = lst = new List<int>();
                lst.Add(ri);
            }
        }
        var keys = byGroupDay.Where(kv => kv.Value.Count >= 2).Select(kv => kv.Key).ToList();
        if (keys.Count == 0) return new HashSet<int>();
        var pick = keys[ctx.Rng.Next(keys.Count)];
        return byGroupDay[pick].ToHashSet();
    }
}
