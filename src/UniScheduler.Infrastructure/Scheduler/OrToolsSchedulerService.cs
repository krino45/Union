using Google.OrTools.Sat;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Scheduler;

public class OrToolsSchedulerService : ISchedulerService
{
    private const int NumDays = 6;
    private const double WalkSpeedMperMin = 80.0;

    public Task<SchedulerOutput> SolveAsync(SchedulerInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(Solve(input));

    private SchedulerOutput Solve(SchedulerInput input)
    {
        int numPairs = input.PairsPerDay;
        int[] breakMinutes = BuildBreakArray(input.BreakMinutesBetweenPairs, numPairs);

        var model = new CpModel();
        var reqs = input.Requirements.ToList();
        var rooms = input.Rooms.ToList();
        var groups = input.Groups.ToList();
        var teachers = input.Teachers.ToList();

        var distances = BuildDistanceMap(input.BuildingDistances);
        var roomDistances = BuildRoomDistanceMap(input.RoomDistances);
        var blocked = BuildBlockedSet(input.TeacherBlocks);

        // key: (reqIdx, day, pair, weekTypeIdx, roomIdx)  value: BoolVar
        // WeekType.Both requirements use wi=0 only; they occupy both odd AND even weeks.
        // WeekType.Odd  → wi=0, WeekType.Even → wi=1.
        var vars = new Dictionary<(int ri, int d, int p, int wi, int rmi), BoolVar>();

        for (int ri = 0; ri < reqs.Count; ri++)
        {
            var req = reqs[ri];
            int varWi = VarWeekIndex(req.WeekType);

            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs; p++)
            {
                // Both-type requirements fire on every week — skip slot if blocked on either week index
                bool slotBlocked = req.WeekType == WeekType.Both
                    ? blocked.Contains((req.TeacherId, d, p, 0)) || blocked.Contains((req.TeacherId, d, p, 1))
                    : blocked.Contains((req.TeacherId, d, p, varWi));
                if (slotBlocked) continue;

                for (int rmi = 0; rmi < rooms.Count; rmi++)
                {
                    if (!IsCompatible(req, rooms[rmi], groups)) continue;
                    vars[(ri, d, p, varWi, rmi)] = model.NewBoolVar($"a_{ri}_{d}_{p}_{varWi}_{rmi}");
                }
            }
        }

        //  Pre-solve diagnostic: detect requirements that provably cannot be placed
        var noRoomLines = new List<string>();
        var noSlotLines = new List<string>();
        for (int ri = 0; ri < reqs.Count; ri++)
        {
            var req = reqs[ri];
            bool anyRoom = rooms.Any(r => IsCompatible(req, r, groups));
            if (!anyRoom)
            {
                noRoomLines.Add($"{req.LessonType} (subj {req.SubjectId:D})");
                continue;
            }
            int varWi = VarWeekIndex(req.WeekType);
            if (!vars.Any(kv => kv.Key.ri == ri && kv.Key.wi == varWi))
                noSlotLines.Add($"{req.LessonType} (subj {req.SubjectId:D})");
        }
        if (noRoomLines.Count > 0 || noSlotLines.Count > 0)
        {
            var parts = new List<string>();
            if (noRoomLines.Count > 0)
                parts.Add($"{noRoomLines.Count} requirement(s) have no compatible rooms — check AllowedLessonTypes, room type, and capacity: {string.Join("; ", noRoomLines.Take(5))}");
            if (noSlotLines.Count > 0)
                parts.Add($"{noSlotLines.Count} requirement(s) have all time slots blocked by teacher availability: {string.Join("; ", noSlotLines.Take(5))}");
            return new SchedulerOutput(SolverStatus.Infeasible, string.Join(" | ", parts), Array.Empty<SchedulerAssignment>());
        }

        // Hard: each (group, subject, lessonType) must have exactly one teacher
        {
            var gstTeachers = new Dictionary<(Guid grp, Guid subj, LessonType lt), HashSet<Guid>>();
            foreach (var req in reqs)
                foreach (var gId in req.GroupIds)
                {
                    var key = (gId, req.SubjectId, req.LessonType);
                    if (!gstTeachers.TryGetValue(key, out var ts)) gstTeachers[key] = ts = new HashSet<Guid>();
                    ts.Add(req.TeacherId);
                }
            var teacherConflicts = gstTeachers
                .Where(kv => kv.Value.Count > 1)
                .Select(kv => $"group …{kv.Key.grp.ToString()[..8]}: {kv.Key.lt} of subj …{kv.Key.subj.ToString()[..8]}")
                .ToList();
            if (teacherConflicts.Count > 0)
                return new SchedulerOutput(SolverStatus.Infeasible,
                    $"Multiple teachers assigned to the same (group, subject, lesson type) — only one teacher per combination is allowed. " +
                    $"{teacherConflicts.Count} conflict(s): {string.Join("; ", teacherConflicts.Take(5))}",
                    Array.Empty<SchedulerAssignment>());
        }

        //  H1: Each requirement scheduled exactly once
        for (int ri = 0; ri < reqs.Count; ri++)
        {
            int varWi = VarWeekIndex(reqs[ri].WeekType);
            var slotVars = CollectVars(vars, ri, varWi);
            model.AddExactlyOne(slotVars);
        }

        //  H4: All rooms — at most one assignment per (room, day, pair, calendar-week-type)
        // Both-type vars (wi=0) occupy the slot on both odd and even weeks, so they appear in
        // BOTH the wi=0 and the wi=1 conflict cells.
        for (int rmi = 0; rmi < rooms.Count; rmi++)
        for (int d = 0; d < NumDays; d++)
        for (int p = 0; p < numPairs; p++)
        for (int wi = 0; wi < 2; wi++)
        {
            var cell = vars
                .Where(kv => kv.Key.rmi == rmi && kv.Key.d == d && kv.Key.p == p
                             && AffectsWeekIndex(reqs[kv.Key.ri].WeekType, wi))
                .Select(kv => kv.Value).ToList();
            if (cell.Count > 1) model.AddAtMostOne(cell);
        }

        //  H5: Teacher — at most one per (day, pair, calendar-week)
        var teacherReqs = teachers.ToDictionary(t => t.Id, t =>
            reqs.Select((r, i) => (r, i)).Where(x => x.r.TeacherId == t.Id).Select(x => x.i).ToList());

        foreach (var (_, trIdxs) in teacherReqs)
        {
            if (trIdxs.Count <= 1) continue;
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                var cell = new List<BoolVar>();
                foreach (int ri in trIdxs)
                {
                    if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                    int varWi = VarWeekIndex(reqs[ri].WeekType);
                    foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == varWi))
                        cell.Add(kv.Value);
                }
                if (cell.Count > 1) model.AddAtMostOne(cell);
            }
        }

        //  H6: Group — at most one per (day, pair, calendar-week)
        var groupReqs = groups.ToDictionary(g => g.Id, g =>
            reqs.Select((r, i) => (r, i)).Where(x => x.r.GroupIds.Contains(g.Id)).Select(x => x.i).ToList());

        foreach (var (_, grIdxs) in groupReqs)
        {
            if (grIdxs.Count <= 1) continue;
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                var cell = new List<BoolVar>();
                foreach (int ri in grIdxs)
                {
                    if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                    int varWi = VarWeekIndex(reqs[ri].WeekType);
                    foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == varWi))
                        cell.Add(kv.Value);
                }
                if (cell.Count > 1) model.AddAtMostOne(cell);
            }
        }

        //  H_travel: Consecutive pairs cannot require impossible building travel
        foreach (var group in groups)
        {
            var grIdxs = groupReqs[group.Id];
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs - 1; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                double allowedTravelMin = breakMinutes[p];

                for (int rmi1 = 0; rmi1 < rooms.Count; rmi1++)
                for (int rmi2 = 0; rmi2 < rooms.Count; rmi2++)
                {
                    int dist = TravelDistanceMeters(rooms[rmi1], rooms[rmi2], distances, roomDistances);
                    if (dist / WalkSpeedMperMin <= allowedTravelMin) continue;

                    foreach (int ri1 in grIdxs)
                    foreach (int ri2 in grIdxs)
                    {
                        if (!AffectsWeekIndex(reqs[ri1].WeekType, wi)) continue;
                        if (!AffectsWeekIndex(reqs[ri2].WeekType, wi)) continue;
                        int wi1 = VarWeekIndex(reqs[ri1].WeekType);
                        int wi2 = VarWeekIndex(reqs[ri2].WeekType);
                        if (!vars.TryGetValue((ri1, d, p, wi1, rmi1), out var v1)) continue;
                        if (!vars.TryGetValue((ri2, d, p + 1, wi2, rmi2), out var v2)) continue;
                        model.Add(LinearExpr.Sum(new BoolVar[] { v1, v2 }) <= 1);
                    }
                }
            }
        }

        //  Auxiliary "is_used" boolean variables
        var isGroupUsed = new BoolVar[groups.Count, NumDays, numPairs, 2];
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var grIdxs = groupReqs[groups[gi].Id];
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                var bv = model.NewBoolVar($"gu_{gi}_{d}_{p}_{wi}");
                isGroupUsed[gi, d, p, wi] = bv;

                var slotVars = new List<BoolVar>();
                foreach (int ri in grIdxs)
                {
                    if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                    int varWi = VarWeekIndex(reqs[ri].WeekType);
                    foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == varWi))
                        slotVars.Add(kv.Value);
                }

                if (slotVars.Count > 0)
                    model.AddMaxEquality(bv, slotVars.Select(v => (IntVar)v).ToArray());
                else
                    model.Add(bv == 0);
            }
        }

        var isTeacherUsed = new BoolVar[teachers.Count, NumDays, numPairs, 2];
        for (int ti = 0; ti < teachers.Count; ti++)
        {
            var trIdxs = teacherReqs[teachers[ti].Id];
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                var bv = model.NewBoolVar($"tu_{ti}_{d}_{p}_{wi}");
                isTeacherUsed[ti, d, p, wi] = bv;

                var slotVars = new List<BoolVar>();
                foreach (int ri in trIdxs)
                {
                    if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                    int varWi = VarWeekIndex(reqs[ri].WeekType);
                    foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == varWi))
                        slotVars.Add(kv.Value);
                }

                if (slotVars.Count > 0)
                    model.AddMaxEquality(bv, slotVars.Select(v => (IntVar)v).ToArray());
                else
                    model.Add(bv == 0);
            }
        }

        //  Build objective
        var objVars = new List<IntVar>();
        var objCoeffs = new List<long>();

        // S1: Student windows (weight 100)
        for (int gi = 0; gi < groups.Count; gi++)
        for (int d = 0; d < NumDays; d++)
        for (int wi = 0; wi < 2; wi++)
        {
            for (int pm = 1; pm < numPairs - 1; pm++)
            {
                var before = model.NewBoolVar($"bef_{gi}_{d}_{wi}_{pm}");
                var after = model.NewBoolVar($"aft_{gi}_{d}_{wi}_{pm}");
                var beforeVars = Enumerable.Range(0, pm).Select(pp => (IntVar)isGroupUsed[gi, d, pp, wi]).ToArray();
                var afterVars = Enumerable.Range(pm + 1, numPairs - pm - 1).Select(pp => (IntVar)isGroupUsed[gi, d, pp, wi]).ToArray();

                model.AddMaxEquality(before, beforeVars);
                model.AddMaxEquality(after, afterVars);

                var windowVar = model.NewBoolVar($"win_{gi}_{d}_{wi}_{pm}");
                model.Add(
                    (LinearExpr)windowVar >=
                    LinearExpr.Sum(new LinearExpr[] { before, after }) - (LinearExpr)isGroupUsed[gi, d, pm, wi] - 1);

                objVars.Add(windowVar);
                objCoeffs.Add(100L);
            }
        }

        // S2: Teacher windows (weight 80)
        for (int ti = 0; ti < teachers.Count; ti++)
        for (int d = 0; d < NumDays; d++)
        for (int wi = 0; wi < 2; wi++)
        {
            for (int pm = 1; pm < numPairs - 1; pm++)
            {
                var before = model.NewBoolVar($"tbef_{ti}_{d}_{wi}_{pm}");
                var after = model.NewBoolVar($"taft_{ti}_{d}_{wi}_{pm}");
                var beforeVars = Enumerable.Range(0, pm).Select(pp => (IntVar)isTeacherUsed[ti, d, pp, wi]).ToArray();
                var afterVars = Enumerable.Range(pm + 1, numPairs - pm - 1).Select(pp => (IntVar)isTeacherUsed[ti, d, pp, wi]).ToArray();

                model.AddMaxEquality(before, beforeVars);
                model.AddMaxEquality(after, afterVars);

                var windowVar = model.NewBoolVar($"twin_{ti}_{d}_{wi}_{pm}");
                model.Add(
                    (LinearExpr)windowVar >=
                    LinearExpr.Sum(new LinearExpr[] { before, after }) - (LinearExpr)isTeacherUsed[ti, d, pm, wi] - 1);

                objVars.Add(windowVar);
                objCoeffs.Add(80L);
            }
        }

        // S3: Penalize each active group-day (weight 60)
        for (int gi = 0; gi < groups.Count; gi++)
        for (int d = 0; d < NumDays; d++)
        for (int wi = 0; wi < 2; wi++)
        {
            var dayHasClass = model.NewBoolVar($"gd_{gi}_{d}_{wi}");
            var daySlotVars = Enumerable.Range(0, numPairs).Select(p => (IntVar)isGroupUsed[gi, d, p, wi]).ToArray();
            model.AddMaxEquality(dayHasClass, daySlotVars);
            objVars.Add(dayHasClass);
            objCoeffs.Add(60L);
        }

        // S4: Walking penalty for adjacent pairs (weight 1–119)
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var grIdxs = groupReqs[groups[gi].Id];
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs - 1; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                double allowedTravelMin = breakMinutes[p];

                for (int rmi1 = 0; rmi1 < rooms.Count; rmi1++)
                for (int rmi2 = 0; rmi2 < rooms.Count; rmi2++)
                {
                    int dist = TravelDistanceMeters(rooms[rmi1], rooms[rmi2], distances, roomDistances);
                    if (dist == 0) continue;
                    double walkMins = dist / WalkSpeedMperMin;
                    if (walkMins > allowedTravelMin) continue;

                    long penalty = Math.Max(1L, (long)(walkMins / allowedTravelMin * 120));

                    foreach (int ri1 in grIdxs)
                    foreach (int ri2 in grIdxs)
                    {
                        if (!AffectsWeekIndex(reqs[ri1].WeekType, wi)) continue;
                        if (!AffectsWeekIndex(reqs[ri2].WeekType, wi)) continue;
                        var wi1 = VarWeekIndex(reqs[ri1].WeekType);
                        var wi2 = VarWeekIndex(reqs[ri2].WeekType);
                        if (!vars.TryGetValue((ri1, d, p, wi1, rmi1), out var v1)) continue;
                        if (!vars.TryGetValue((ri2, d, p + 1, wi2, rmi2), out var v2)) continue;
                        var walkPen = model.NewBoolVar($"walk_{gi}_{d}_{p}_{wi}_{rmi1}_{rmi2}_{ri1}_{ri2}");
                        model.Add(LinearExpr.Sum([v1, v2]) <= 1 + walkPen);
                        model.Add(walkPen <= LinearExpr.Sum([v1, v2]));
                        objVars.Add(walkPen);
                        objCoeffs.Add(penalty);
                    }
                }
            }
        }

        // S5: SanPIN — penalize daily pairs > 4 per group (weight 300 per excess pair)
        const int sanPinMax = 4;
        for (var gi = 0; gi < groups.Count; gi++)
        for (var d = 0; d < NumDays; d++)
        for (var wi = 0; wi < 2; wi++)
        {
            var dayUsed = Enumerable.Range(0, numPairs).Select(p => (IntVar)isGroupUsed[gi, d, p, wi]).ToArray();
            var overload = model.NewIntVar(0, numPairs - sanPinMax, $"spov_{gi}_{d}_{wi}");
            model.Add(overload >= LinearExpr.Sum(dayUsed) - sanPinMax);
            objVars.Add(overload);
            objCoeffs.Add(300L);
        }

        // S6: Penalize consecutive pairs of the same (subject, lessonType) for a group — weight 70
        {
            var stGroups = reqs
                .Select((r, i) => (r, i))
                .GroupBy(x => (x.r.SubjectId, x.r.LessonType))
                .ToList();

            for (int sti = 0; sti < stGroups.Count; sti++)
            {
                var stg = stGroups[sti];
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var gId = groups[gi].Id;
                    var riSet = stg.Where(x => x.r.GroupIds.Contains(gId)).Select(x => x.i).ToList();
                    if (riSet.Count < 2) continue;

                    for (int d = 0; d < NumDays; d++)
                    for (int p = 0; p < numPairs - 1; p++)
                    for (int wi = 0; wi < 2; wi++)
                    {
                        var atP  = new List<BoolVar>();
                        var atP1 = new List<BoolVar>();
                        foreach (int ri in riSet)
                        {
                            if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                            int varWi = VarWeekIndex(reqs[ri].WeekType);
                            for (int rmi = 0; rmi < rooms.Count; rmi++)
                            {
                                if (vars.TryGetValue((ri, d, p,     varWi, rmi), out var vp))  atP.Add(vp);
                                if (vars.TryGetValue((ri, d, p + 1, varWi, rmi), out var vp1)) atP1.Add(vp1);
                            }
                        }
                        if (atP.Count == 0 || atP1.Count == 0) continue;

                        var usedP  = model.NewBoolVar($"rep_p_{sti}_{gi}_{d}_{p}_{wi}");
                        var usedP1 = model.NewBoolVar($"rep_p1_{sti}_{gi}_{d}_{p}_{wi}");
                        model.AddMaxEquality(usedP,  atP.Select(v  => (IntVar)v).ToArray());
                        model.AddMaxEquality(usedP1, atP1.Select(v => (IntVar)v).ToArray());

                        var repPen = model.NewBoolVar($"rep_{sti}_{gi}_{d}_{p}_{wi}");
                        model.Add(repPen <= usedP);
                        model.Add(repPen <= usedP1);
                        model.Add(LinearExpr.Sum(new BoolVar[] { usedP, usedP1 }) <= 1 + repPen);

                        objVars.Add(repPen);
                        objCoeffs.Add(70L);
                    }
                }
            }
        }

        if (objVars.Count > 0)
            model.Minimize(LinearExpr.WeightedSum(objVars.ToArray(), objCoeffs.ToArray()));

        //  Solve
        var solver = new CpSolver();
        solver.StringParameters =
            $"max_time_in_seconds:{input.SolverTimeoutSeconds}," +
            "num_search_workers:4," +
            "log_search_progress:false";

        var status = solver.Solve(model);

        switch (status)
        {
            case CpSolverStatus.Infeasible:
                return new SchedulerOutput(SolverStatus.Infeasible,
                    "No feasible schedule found. Check room availability, teacher blocks, and building travel times.",
                    []);
            case CpSolverStatus.Unknown:
                return new SchedulerOutput(SolverStatus.Unknown,
                    "Solver reached time limit without finding a feasible schedule.",
                    []);
        }

        //  Extract assignments
        var assignments = new List<SchedulerAssignment>();
        foreach (var ((ri, d, p, wi, rmi), v) in vars)
        {
            if (solver.BooleanValue(v))
            {
                var weekType = reqs[ri].WeekType == WeekType.Both
                    ? WeekType.Both
                    : (wi == 0 ? WeekType.Odd : WeekType.Even);

                assignments.Add(new SchedulerAssignment(ri, (RussianDayOfWeek)(d + 1), p + 1, weekType, rooms[rmi].Id));
            }
        }

        var solverStatus = status == CpSolverStatus.Optimal ? SolverStatus.Optimal : SolverStatus.Feasible;
        return new SchedulerOutput(solverStatus,
            $"Status: {status}, Objective: {(objVars.Count > 0 ? solver.ObjectiveValue.ToString("F0") : "N/A")}, Entries: {assignments.Count}",
            assignments);
    }

    //  Helpers

    /// <summary>
    /// The variable key wi used for a requirement. Both/Odd use wi=0; Even uses wi=1.
    /// </summary>
    private static int VarWeekIndex(WeekType wt) => wt == WeekType.Even ? 1 : 0;

    /// <summary>
    /// True if a requirement with this WeekType fires during calendar week wi (0=odd, 1=even).
    /// Both fires every week; Odd only on odd; Even only on even.
    /// </summary>
    private static bool AffectsWeekIndex(WeekType wt, int wi) => wt switch
    {
        WeekType.Both => true,
        WeekType.Odd => wi == 0,
        WeekType.Even => wi == 1,
        _ => true
    };

    private static int[] BuildBreakArray(IReadOnlyList<int>? breaks, int numPairs)
    {
        int gaps = numPairs - 1;
        if (breaks != null && breaks.Count >= gaps)
            return breaks.Take(gaps).ToArray();
        return Enumerable.Repeat(15, gaps).ToArray();
    }

    private static Dictionary<(Guid, Guid), int> BuildDistanceMap(IEnumerable<SchedulerBuildingDistance> distances)
    {
        var map = new Dictionary<(Guid, Guid), int>();
        foreach (var d in distances)
        {
            map[(d.FromId, d.ToId)] = d.DistanceMeters;
            map[(d.ToId, d.FromId)] = d.DistanceMeters;
        }
        return map;
    }

    private static Dictionary<(Guid, Guid), int> BuildRoomDistanceMap(IReadOnlyList<SchedulerRoomDistance>? distances)
    {
        var map = new Dictionary<(Guid, Guid), int>();
        if (distances == null) return map;
        foreach (var d in distances)
        {
            map[(d.FromRoomId, d.ToRoomId)] = d.DistanceMeters;
            map[(d.ToRoomId, d.FromRoomId)] = d.DistanceMeters;
        }
        return map;
    }

    private static int TravelDistanceMeters(SchedulerRoom r1, SchedulerRoom r2,
        Dictionary<(Guid, Guid), int> buildingDistanceMap,
        Dictionary<(Guid, Guid), int> roomDistanceMap)
    {
        if (r1.BuildingId == r2.BuildingId)
            return roomDistanceMap.TryGetValue((r1.Id, r2.Id), out int rd) ? rd : 0;
        return buildingDistanceMap.TryGetValue((r1.BuildingId, r2.BuildingId), out int d) ? d : int.MaxValue / 2;
    }

    private static HashSet<(Guid, int, int, int)> BuildBlockedSet(IEnumerable<SchedulerBlock> blocks)
    {
        var set = new HashSet<(Guid, int, int, int)>();
        foreach (var b in blocks)
        {
            // Both-type blocks must cover both week indices
            foreach (int wi in b.WeekType == WeekType.Both ? new[] { 0, 1 } : new[] { VarWeekIndex(b.WeekType) })
                set.Add((b.TeacherId, (int)b.Day - 1, b.PairNumber - 1, wi));
        }
        return set;
    }

    private static List<BoolVar> CollectVars(Dictionary<(int ri, int d, int p, int wi, int rmi), BoolVar> vars, int ri, int wi)
        => vars.Where(kv => kv.Key.ri == ri && kv.Key.wi == wi).Select(kv => kv.Value).ToList();

    private static bool IsCompatible(SchedulerRequirement req, SchedulerRoom room, List<SchedulerGroup> groups)
    {
        if (req.IsOnline) return room.IsOnline;
        if (room.IsOnline) return false;
        if (req.NeedsProjector && !room.HasProjector) return false;
        if (req.NeedsComputers && !room.HasComputers) return false;
        if (req.NeedsLab && !room.HasLab) return false;
        if (room.AllowedLessonTypes is { Count: > 0 } allowed && !allowed.Contains(req.LessonType)) return false;

        int total = groups.Where(g => req.GroupIds.Contains(g.Id)).Sum(g => g.StudentCount);
        if (room.Capacity < total) return false;

        return (req.LessonType, room.RoomType) switch
        {
            (LessonType.Lecture, RoomType.LectureHall) => true,
            (LessonType.Lecture, RoomType.RegularCabinet) => true,
            (LessonType.Practical, RoomType.RegularCabinet) => true,
            (LessonType.Practical, RoomType.LectureHall) => true,
            (LessonType.Practical, RoomType.ComputerLab) => true,
            (LessonType.Seminar, RoomType.RegularCabinet) => true,
            (LessonType.Lab, RoomType.Lab) => true,
            (LessonType.Lab, RoomType.ComputerLab) => req.NeedsComputers,
            _ => false
        };
    }
}
