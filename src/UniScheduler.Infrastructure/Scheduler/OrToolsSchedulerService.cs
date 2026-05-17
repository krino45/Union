using Google.OrTools.Sat;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Scheduler;

/// <summary>
/// Schedule optimizer using Google OR-Tools CP-SAT solver (version 9.x).
///
/// Variables: assignment[reqIdx, day, pair, weekTypeIdx, roomIdx] ∈ {0,1}
/// Only variables for feasible (requirement, room) pairs are created (pruning).
///
/// Hard constraints: H1 coverage, H4 room clash, H4b lecture hall same-subject,
///                  H5 teacher clash, H6 group clash, H7 teacher blocks (pruning), H_travel
/// Soft constraints: S1 student windows, S2 teacher windows, S3 day packing,
///                  S4 walking penalty
/// </summary>
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
        var blocked = BuildBlockedSet(input.TeacherBlocks);

        //  Create decision variables
        // key: (reqIdx, day, pair, weekTypeIdx, roomIdx)  value: BoolVar
        var vars = new Dictionary<(int ri, int d, int p, int wi, int rmi), BoolVar>();

        for (int ri = 0; ri < reqs.Count; ri++)
        {
            var req = reqs[ri];
            foreach (int wi in WeekIndexes(req.WeekType))
            {
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                {
                    if (blocked.Contains((req.TeacherId, d, p, wi))) continue;

                    for (int rmi = 0; rmi < rooms.Count; rmi++)
                    {
                        if (!IsCompatible(req, rooms[rmi], groups)) continue;
                        vars[(ri, d, p, wi, rmi)] = model.NewBoolVar($"a_{ri}_{d}_{p}_{wi}_{rmi}");
                    }
                }
            }
        }

        //  H1: Each requirement scheduled exactly once per applicable week type
        for (int ri = 0; ri < reqs.Count; ri++)
        {
            foreach (int wi in WeekIndexes(reqs[ri].WeekType))
            {
                var slotVars = CollectVars(vars, ri, wi);
                if (slotVars.Count == 0) { model.AddLinearConstraint(LinearExpr.Constant(1), 2, 3); continue; }
                model.AddExactlyOne(slotVars);
            }
        }

        //  H4: Non-lecture rooms — at most one per (room, day, pair, weekType)
        for (int rmi = 0; rmi < rooms.Count; rmi++)
        {
            if (rooms[rmi].RoomType == RoomType.LectureHall) continue;
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                var cell = vars.Where(kv => kv.Key.rmi == rmi && kv.Key.d == d && kv.Key.p == p && kv.Key.wi == wi)
                               .Select(kv => kv.Value).ToList();
                if (cell.Count > 1) model.AddAtMostOne(cell);
            }
        }

        //  H4b: Lecture halls — different subjects cannot share a slot
        for (int rmi = 0; rmi < rooms.Count; rmi++)
        {
            if (rooms[rmi].RoomType != RoomType.LectureHall) continue;
            for (int d = 0; d < NumDays; d++)
            for (int p = 0; p < numPairs; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                for (int ri1 = 0; ri1 < reqs.Count; ri1++)
                for (int ri2 = ri1 + 1; ri2 < reqs.Count; ri2++)
                {
                    if (reqs[ri1].SubjectId == reqs[ri2].SubjectId) continue;
                    if (!vars.TryGetValue((ri1, d, p, wi, rmi), out var v1)) continue;
                    if (!vars.TryGetValue((ri2, d, p, wi, rmi), out var v2)) continue;
                    model.Add(LinearExpr.Sum(new BoolVar[] { v1, v2 }) <= 1);
                }
            }
        }

        //  H5: Teacher — at most one per slot
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
                foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == wi))
                    cell.Add(kv.Value);
                if (cell.Count > 1) model.AddAtMostOne(cell);
            }
        }

        //  H6: Group — at most one per slot
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
                foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == wi))
                    cell.Add(kv.Value);
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
                    int dist = TravelDistanceMeters(rooms[rmi1], rooms[rmi2], distances);
                    if (dist / WalkSpeedMperMin <= allowedTravelMin) continue;

                    foreach (int ri1 in grIdxs)
                    foreach (int ri2 in grIdxs)
                    {
                        if (!vars.TryGetValue((ri1, d, p, wi, rmi1), out var v1)) continue;
                        if (!vars.TryGetValue((ri2, d, p + 1, wi, rmi2), out var v2)) continue;
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
                foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == wi))
                    slotVars.Add(kv.Value);

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
                foreach (var kv in vars.Where(x => x.Key.ri == ri && x.Key.d == d && x.Key.p == p && x.Key.wi == wi))
                    slotVars.Add(kv.Value);

                if (slotVars.Count > 0)
                    model.AddMaxEquality(bv, slotVars.Select(v => (IntVar)v).ToArray());
                else
                    model.Add(bv == 0);
            }
        }

        //  Build objective
        var objVars = new List<IntVar>();
        var objCoeffs = new List<long>();

        // S1: Student windows — penalize gap pairs (weight 100)
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

        // S3: Penalize each active group-day (fewer days = lower cost) weight 60
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

        // S4: Walking penalty for adjacent pairs with non-trivial travel (weight 1–119)
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
                    int dist = TravelDistanceMeters(rooms[rmi1], rooms[rmi2], distances);
                    if (dist == 0) continue;
                    double walkMins = dist / WalkSpeedMperMin;
                    if (walkMins > allowedTravelMin) continue; // already hard constraint

                    long penalty = Math.Max(1L, (long)(walkMins / allowedTravelMin * 120));

                    foreach (int ri1 in grIdxs)
                    foreach (int ri2 in grIdxs)
                    {
                        if (!vars.TryGetValue((ri1, d, p, wi, rmi1), out var v1)) continue;
                        if (!vars.TryGetValue((ri2, d, p + 1, wi, rmi2), out var v2)) continue;
                        var walkPen = model.NewBoolVar($"walk_{gi}_{d}_{p}_{wi}_{rmi1}_{rmi2}");
                        model.Add(LinearExpr.Sum(new BoolVar[] { v1, v2 }) <= 1 + walkPen);
                        model.Add((LinearExpr)walkPen <= LinearExpr.Sum(new BoolVar[] { v1, v2 }));
                        objVars.Add(walkPen);
                        objCoeffs.Add(penalty);
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

        if (status == CpSolverStatus.Infeasible)
            return new SchedulerOutput(SolverStatus.Infeasible,
                "No feasible schedule found. Check room availability, teacher blocks, and building travel times.",
                Array.Empty<SchedulerAssignment>());

        if (status == CpSolverStatus.Unknown)
            return new SchedulerOutput(SolverStatus.Unknown,
                "Solver reached time limit without finding a feasible schedule.",
                Array.Empty<SchedulerAssignment>());

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
    /// Returns break durations for each pair gap (index p = gap between pair p+1 and p+2).
    /// Falls back to 15 minutes if not provided.
    /// </summary>
    private static int[] BuildBreakArray(IReadOnlyList<int>? breaks, int numPairs)
    {
        int gaps = numPairs - 1;
        if (breaks != null && breaks.Count >= gaps)
            return breaks.Take(gaps).ToArray();
        // default 15 min breaks
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

    /// <summary>
    /// Returns the effective travel distance in metres between two rooms.
    /// Same building: stair model (|Δfloor| × stairsPerFloor + distFromStairs₁ + distFromStairs₂).
    /// Different buildings: inter-building distance (int.MaxValue/2 if unknown).
    /// </summary>
    private static int TravelDistanceMeters(SchedulerRoom r1, SchedulerRoom r2, Dictionary<(Guid, Guid), int> distanceMap)
    {
        if (r1.BuildingId == r2.BuildingId)
        {
            return Math.Abs(r1.Floor - r2.Floor) * r1.StairsDistancePerFloor
                   + r1.DistanceFromStairsMeters + r2.DistanceFromStairsMeters;
        }
        return distanceMap.TryGetValue((r1.BuildingId, r2.BuildingId), out int d) ? d : int.MaxValue / 2;
    }

    private static HashSet<(Guid, int, int, int)> BuildBlockedSet(IEnumerable<SchedulerBlock> blocks)
    {
        var set = new HashSet<(Guid, int, int, int)>();
        foreach (var b in blocks)
        {
            foreach (int wi in WeekIndexes(b.WeekType))
                set.Add((b.TeacherId, (int)b.Day - 1, b.PairNumber - 1, wi));
        }
        return set;
    }

    private static int[] WeekIndexes(WeekType wt) => wt switch
    {
        WeekType.Both => new[] { 0, 1 },
        WeekType.Odd => new[] { 0 },
        WeekType.Even => new[] { 1 },
        _ => new[] { 0, 1 }
    };

    private static List<BoolVar> CollectVars(Dictionary<(int ri, int d, int p, int wi, int rmi), BoolVar> vars, int ri, int wi)
        => vars.Where(kv => kv.Key.ri == ri && kv.Key.wi == wi).Select(kv => kv.Value).ToList();

    private static bool IsCompatible(SchedulerRequirement req, SchedulerRoom room, List<SchedulerGroup> groups)
    {
        if (req.IsOnline) return room.IsOnline;
        if (room.IsOnline) return false;
        if (req.NeedsProjector && !room.HasProjector) return false;
        if (req.NeedsComputers && !room.HasComputers) return false;
        if (req.NeedsLab && !room.HasLab) return false;

        int total = groups.Where(g => req.GroupIds.Contains(g.Id)).Sum(g => g.StudentCount);
        if (room.Capacity < total) return false;

        return (req.LessonType, room.RoomType) switch
        {
            (LessonType.Lecture, RoomType.LectureHall) => true,
            (LessonType.Lecture, RoomType.RegularCabinet) => true,
            (LessonType.Practical, RoomType.RegularCabinet) => true,
            (LessonType.Practical, RoomType.LectureHall) => true,
            (LessonType.Seminar, RoomType.RegularCabinet) => true,
            (LessonType.Lab, RoomType.Lab) => true,
            (LessonType.Lab, RoomType.ComputerLab) => req.NeedsComputers,
            _ => false
        };
    }
}
