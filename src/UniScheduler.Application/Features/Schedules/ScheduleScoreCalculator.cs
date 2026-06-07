using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
#pragma warning disable CS1591

namespace UniScheduler.Application.Features.Schedules;

/// <summary>
/// Distance/timing data needed for the S4 (walking) penalty.
/// Build via <see cref="ScheduleScoreCalculator.BuildScoreContext"/>.
/// </summary>
public record ScoreContext(
    IReadOnlyDictionary<(Guid, Guid), int> RoomDistances,
    IReadOnlyDictionary<(Guid, Guid), int> BuildingDistances,
    IReadOnlyDictionary<Guid, Guid> RoomToBuilding,
    IReadOnlyList<int> BreakMinutes,
    IReadOnlyDictionary<Guid, Guid?>? RoomDeptFacultyId = null,
    IReadOnlyDictionary<Guid, Guid?>? SubjectDeptFacultyId = null,
    SolverWeights? Penalties = null,
    IReadOnlyDictionary<Guid, int>? RoomEntryDistances = null,
    // Rooms exempt from room double-booking: the distributed placeholder room and sports halls are
    // multi-occupancy by design (many parallel classes share them). Mirrors the solver's H4 skip.
    IReadOnlySet<Guid>? MultiOccupancyRoomIds = null,
    IReadOnlyDictionary<Guid, IReadOnlySet<LessonType>>? RoomAllowedLessonTypes = null,
    IReadOnlyDictionary<Guid, IReadOnlySet<RussianDayOfWeek>>? GroupBlockedDays = null,
    IReadOnlySet<(Guid teacherId, RussianDayOfWeek day, int pair, int calWeek)>? TeacherBlockedSlots = null);

public record ScoreBreakdown(
    int HardConflicts,
    int S1_StudentWindows,
    int S2_TeacherWindows,
    int S3_ActiveDays,
    int S4_Walking,
    int S5_SanPinOverload,
    int S6_ConsecSameLesson,
    int S7_TimeOfDay,
    int S8_Saturday,
    int S9_DeptMismatch,
    int S10_Overflow = 0,
    int S11_RoomTypeMismatch = 0,
    int S12_BlockedPlacement = 0)
{
    public int Total =>
        HardConflicts + S1_StudentWindows + S2_TeacherWindows + S3_ActiveDays +
        S4_Walking + S5_SanPinOverload + S6_ConsecSameLesson + S7_TimeOfDay +
        S8_Saturday + S9_DeptMismatch + S10_Overflow + S11_RoomTypeMismatch + S12_BlockedPlacement;
}

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
        IEnumerable<PairTimeSlot> pairSlots,
        IEnumerable<Subject>? subjects = null,
        SolverWeights? penalties = null,
        IEnumerable<StudentGroup>? groups = null,
        IEnumerable<UniScheduler.Domain.Entities.TeacherAvailability>? teacherAvailabilities = null)
    {
        penalties ??= new SolverWeights();

        var roomList = rooms.ToList();
        var nodeList = nodes.ToList();
        var edgeList = edges.ToList();
        var roomDists = ComputeRoomDistances(nodeList, edgeList, penalties.StairFloorMeters);
        var roomEntryDists = ComputeRoomEntryDistances(nodeList, edgeList, penalties.StairFloorMeters);

        // Transitive all-pairs over the building-distance graph: A-B + B-C is reachable even when
        // no direct A-C edge exists.
        var bldDists = ComputeAllPairsBuildingDistances(buildingDistances);

        var roomToBuilding = roomList.ToDictionary(r => r.Id, r => r.BuildingId);
        var breakMins = ComputeBreakMinutes(pairSlots);

        // Distributed sentinel + sports halls are shared concurrently by many classes - never a
        // room double-book. Matches OrToolsSchedulerService H4 (skips AddAtMostOne for these).
        var multiOccupancy = roomList
            .Where(r => r.IsDistributed || r.RoomType == RoomType.SportsHall)
            .Select(r => r.Id)
            .ToHashSet();

        var roomAllowed = roomList.ToDictionary(r => r.Id, AllowedLessonTypes);

        IReadOnlyDictionary<Guid, IReadOnlySet<RussianDayOfWeek>>? groupBlockedDays = groups?
            .ToDictionary(g => g.Id, g => (IReadOnlySet<RussianDayOfWeek>)g.BlockedDays.Select(b => b.DayOfWeek).ToHashSet());
        IReadOnlySet<(Guid, RussianDayOfWeek, int, int)>? teacherBlocked = null;
        if (teacherAvailabilities != null)
        {
            var tb = new HashSet<(Guid, RussianDayOfWeek, int, int)>();
            foreach (var ta in teacherAvailabilities)
                foreach (int cw in CalWeekIndices(ta.WeekType))
                    tb.Add((ta.TeacherId, ta.DayOfWeek, ta.PairNumber, cw));
            teacherBlocked = tb;
        }

        IReadOnlyDictionary<Guid, Guid?>? roomDeptFacultyId = penalties.DepartmentMismatchPenalty > 0
            ? roomList.ToDictionary(r => r.Id, r => r.Department?.FacultyId)
            : null;

        IReadOnlyDictionary<Guid, Guid?>? subjectDeptFacultyId = penalties.DepartmentMismatchPenalty > 0 && subjects != null
            ? subjects.ToDictionary(s => s.Id, s => s.Department?.FacultyId)
            : null;

        return new ScoreContext(roomDists,
            bldDists,
            roomToBuilding,
            breakMins,
            roomDeptFacultyId,
            subjectDeptFacultyId,
            penalties,
            roomEntryDists,
            multiOccupancy,
            roomAllowed,
            groupBlockedDays,
            teacherBlocked);
    }

    public static int Compute(IReadOnlyList<ScheduleEntry> entries, ScoreContext ctx)
        => ComputeBreakdown(entries, ctx).Total;

    public static ScoreBreakdown ComputeBreakdown(IReadOnlyList<ScheduleEntry> entries, ScoreContext ctx)
    {
        var w = ctx?.Penalties ?? new SolverWeights();
        var hard = Score_HardConflicts(entries, ctx);

        int s1 = 0, s3 = 0, s4 = 0, s5 = 0, s6 = 0, s7 = 0, s8 = 0;

        var byGroup = GroupBy(entries, e => e.StudentGroups.Select(sg => sg.StudentGroupId));
        var weekVariants = new[] { WeekType.Odd, WeekType.Even };

        foreach (var (_, ge) in byGroup)
        foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
        foreach (var wv in weekVariants)
        {
            var slot = ge.Where(e => e.DayOfWeek == day && Affects(e.WeekType, wv)).ToList();
            if (slot.Count == 0) continue;

            var onlineByPair = slot.GroupBy(e => e.PairNumber).ToDictionary(g => g.Key, g => g.All(e => e.IsOnline));
            var pairs = onlineByPair.Keys.OrderBy(p => p).ToList();

            s3 += Score_S3_ActiveDay(w);
            s1 += Score_S1_StudentWindows(pairs, onlineByPair, w);
            s5 += Score_S5_SanPin(slot, pairs, w);
            s8 += Score_S8_Saturday(day, pairs.Count, w);
            s6 += Score_S6_ConsecSameLesson(slot, w);
            s7 += Score_S7_TimeOfDay(pairs, w);
            if (ctx != null) s4 += Score_S4_Walking(slot, pairs, ctx, w);
        }

        var byTeacher = entries.GroupBy(e => e.TeacherId).ToDictionary(g => g.Key, g => g.ToList());
        int s2 = Score_S2_TeacherWindows(byTeacher, weekVariants, w);
        int s9 = ctx == null ? 0 : Score_S9_DeptMismatch(entries, ctx, w);

        int s10 = 0;
        foreach (var e in entries)
            if (e.RoomId == SchedulerSentinels.OverflowRoomId)
                s10 += (int)SchedulerSentinels.OverflowPenalty;

        int s11 = ctx == null ? 0 : Score_RoomTypeMismatch(entries, ctx);
        int s12 = ctx == null ? 0 : Score_BlockedPlacement(entries, ctx);

        return new ScoreBreakdown(
            HardConflicts: hard,
            S1_StudentWindows: s1,
            S2_TeacherWindows: s2,
            S3_ActiveDays: s3,
            S4_Walking: s4,
            S5_SanPinOverload: s5,
            S6_ConsecSameLesson: s6,
            S7_TimeOfDay: s7,
            S8_Saturday: s8,
            S9_DeptMismatch: s9,
            S10_Overflow: s10,
            S11_RoomTypeMismatch: s11,
            S12_BlockedPlacement: s12);
    }

    internal static int[] CalWeekIndices(WeekType wt) => wt switch
    {
        WeekType.Odd => new[] { 0 },
        WeekType.Even => new[] { 1 },
        _ => new[] { 0, 1 }
    };

    public const int BlockedPlacementPenalty = 50_000;

    public static int Score_BlockedPlacement(IReadOnlyList<ScheduleEntry> entries, ScoreContext ctx)
    {
        var gbd = ctx?.GroupBlockedDays;
        var tbs = ctx?.TeacherBlockedSlots;
        if (gbd == null && tbs == null) return 0;
        int count = 0;
        foreach (var e in entries)
        {
            if (gbd != null)
                foreach (var sg in e.StudentGroups)
                    if (gbd.TryGetValue(sg.StudentGroupId, out var days) && days.Contains(e.DayOfWeek)) { count++; break; }
            if (tbs != null)
                foreach (int cw in CalWeekIndices(e.WeekType))
                    if (tbs.Contains((e.TeacherId, e.DayOfWeek, e.PairNumber, cw))) { count++; break; }
        }
        return count * BlockedPlacementPenalty;
    }

    public const int RoomTypeMismatchPenalty = 50_000;

    public static int Score_RoomTypeMismatch(IReadOnlyList<ScheduleEntry> entries, ScoreContext ctx)
    {
        if (ctx?.RoomAllowedLessonTypes is not { } allowedMap) return 0;
        int count = 0;
        foreach (var e in entries)
        {
            if (e.IsOnline || !e.RoomId.HasValue) continue;
            if (e.RoomId.Value == SchedulerSentinels.OverflowRoomId) continue;
            if (allowedMap.TryGetValue(e.RoomId.Value, out var allowed) && !allowed.Contains(e.LessonType))
                count++;
        }
        return count * RoomTypeMismatchPenalty;
    }

    private static readonly IReadOnlySet<LessonType> AllLessonTypes =
        new HashSet<LessonType>(Enum.GetValues<LessonType>());

    private static IReadOnlySet<LessonType> AllowedLessonTypes(Room r)
    {
        if (r.IsOnline) return AllLessonTypes;
        if (r.IsDistributed) return new HashSet<LessonType> { LessonType.Language };
        if (r.RoomType == RoomType.SportsHall) return new HashSet<LessonType> { LessonType.PhysicalEducation };
        if (r.AllowedLessonTypes is { Count: > 0 } a) return a.ToHashSet();     // admin opt-in overrides the heuristic
        return r.RoomType switch
        {
            RoomType.LectureHall   => new HashSet<LessonType> { LessonType.Lecture, LessonType.Practical },
            RoomType.RegularCabinet => new HashSet<LessonType> { LessonType.Lecture, LessonType.Practical, LessonType.Seminar },
            RoomType.Lab           => new HashSet<LessonType> { LessonType.Lab },
            RoomType.ComputerLab   => new HashSet<LessonType> { LessonType.Practical, LessonType.Lab },
            _                      => AllLessonTypes // Virtual / unknown - dont flag
        };
    }

    public static int Score_HardConflicts(IReadOnlyList<ScheduleEntry> entries, ScoreContext? ctx = null)
    {
        int score = 0;
        var seen = new HashSet<string>();
        var multiOcc = ctx?.MultiOccupancyRoomIds;
        for (int i = 0; i < entries.Count; i++)
        for (int j = i + 1; j < entries.Count; j++)
        {
            var a = entries[i]; var b = entries[j];
            if (!Overlaps(a, b)) continue;

            if (a.ParallelGroupId.HasValue && a.ParallelGroupId == b.ParallelGroupId) continue;

            if (!a.IsOnline && !b.IsOnline && a.RoomId.HasValue && a.RoomId == b.RoomId
                && a.RoomId != SchedulerSentinels.OverflowRoomId
                && (multiOcc == null || !multiOcc.Contains(a.RoomId.Value)))
                if (seen.Add($"r:{a.RoomId}:{a.DayOfWeek}:{a.PairNumber}"))
                    score += 100000;

            if (a.TeacherId == b.TeacherId)
                if (seen.Add($"t:{a.TeacherId}:{a.DayOfWeek}:{a.PairNumber}"))
                    score += 100000;

            var ag = a.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            var bg = b.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            foreach (var gid in ag.Intersect(bg))
                if (seen.Add($"g:{gid}:{a.DayOfWeek}:{a.PairNumber}"))
                    score += 100000;
        }
        return score;
    }

    // S1: student windows. A gap is only penalised between two on-campus pairs; a gap next to
    // an online pair is fine. A zero-gap online<->campus transition is penalised like a window.
    public static int Score_S1_StudentWindows(
        IReadOnlyList<int> pairs, IReadOnlyDictionary<int, bool> onlineByPair, SolverWeights w)
    {
        int score = 0;
        for (int i = 0; i + 1 < pairs.Count; i++)
        {
            int a = pairs[i], b = pairs[i + 1];
            int gap = b - a - 1;
            if (gap > 0)
            {
                if (!onlineByPair[a] && !onlineByPair[b]) score += gap * w.StudentWindow;
            }
            else if (onlineByPair[a] != onlineByPair[b])
            {
                score += w.StudentWindow;
            }
        }
        return score;
    }

    // S2: teacher windows (+w.TeacherWindow per gap-pair across active span).
    public static int Score_S2_TeacherWindows(
        Dictionary<Guid, List<ScheduleEntry>> byTeacher, WeekType[] weekVariants, SolverWeights w)
    {
        int score = 0;
        foreach (var (_, te) in byTeacher)
        foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
        foreach (var wv in weekVariants)
        {
            var pairs = te.Where(e => e.DayOfWeek == day && Affects(e.WeekType, wv))
                          .Select(e => e.PairNumber).Distinct().OrderBy(p => p).ToList();
            if (pairs.Count >= 2)
                score += (pairs[^1] - pairs[0] + 1 - pairs.Count) * w.TeacherWindow;
        }
        return score;
    }

    // S3: flat penalty for each (group, day) that has any class.
    public static int Score_S3_ActiveDay(SolverWeights w) => w.ActiveDay;

    // S4: walking penalty between consecutive on-campus pairs based on travel time vs break.
    public static int Score_S4_Walking(
        IReadOnlyList<ScheduleEntry> slot, IReadOnlyList<int> pairs, ScoreContext ctx, SolverWeights w)
    {
        int score = 0;
        for (int pi = 0; pi < pairs.Count - 1; pi++)
        {
            int p1 = pairs[pi], p2 = pairs[pi + 1];
            if (p2 != p1 + 1) continue;

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
                if (walkMins > allowedTravelMin) continue;

                long penalty = Math.Max(1L, (long)(walkMins / allowedTravelMin * w.WalkingPenaltyMax));
                score += (int)penalty;
            }
        }
        return score;
    }

    // Per-(group, day) S4 walking penalty over an entry set
    public static Dictionary<(Guid group, RussianDayOfWeek day), int> Score_S4_WalkingByGroupDay(
        IReadOnlyList<ScheduleEntry> entries, ScoreContext ctx, SolverWeights w)
    {
        var result = new Dictionary<(Guid, RussianDayOfWeek), int>();
        var byGroup = GroupBy(entries, e => e.StudentGroups.Select(sg => sg.StudentGroupId));
        var weekVariants = new[] { WeekType.Odd, WeekType.Even };

        foreach (var (gid, ge) in byGroup)
        foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
        {
            int walk = 0;
            foreach (var wv in weekVariants)
            {
                var slot = ge.Where(e => e.DayOfWeek == day && Affects(e.WeekType, wv)).ToList();
                if (slot.Count < 2) continue;
                var pairs = slot.GroupBy(e => e.PairNumber).Select(g => g.Key).OrderBy(p => p).ToList();
                walk += Score_S4_Walking(slot, pairs, ctx, w);
            }
            if (walk > 0) result[(gid, day)] = walk;
        }
        return result;
    }

    // Per-(group, day) student-local soft penalty
    // S3 (flat per active day) and S4 (walking, a space concern) are excluded.
    public static Dictionary<(Guid group, RussianDayOfWeek day), int> Score_StudentDayPenaltyByGroupDay(
        IReadOnlyList<ScheduleEntry> entries, SolverWeights w)
    {
        var result = new Dictionary<(Guid, RussianDayOfWeek), int>();
        var byGroup = GroupBy(entries, e => e.StudentGroups.Select(sg => sg.StudentGroupId));
        var weekVariants = new[] { WeekType.Odd, WeekType.Even };

        foreach (var (gid, ge) in byGroup)
        foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
        {
            int p = 0;
            foreach (var wv in weekVariants)
            {
                var slot = ge.Where(e => e.DayOfWeek == day && Affects(e.WeekType, wv)).ToList();
                if (slot.Count == 0) continue;
                var onlineByPair = slot.GroupBy(e => e.PairNumber).ToDictionary(g => g.Key, g => g.All(e => e.IsOnline));
                var pairs = onlineByPair.Keys.OrderBy(x => x).ToList();
                p += Score_S1_StudentWindows(pairs, onlineByPair, w);
                p += Score_S5_SanPin(slot, pairs, w);
                p += Score_S6_ConsecSameLesson(slot, w);
                p += Score_S7_TimeOfDay(pairs, w);
                p += Score_S8_Saturday(day, pairs.Count, w);
            }
            if (p > 0) result[(gid, day)] = p;
        }
        return result;
    }

    // S5: SanPiN day rules for one (group, day, week) slot. Combines:
    //  - daily overload (+SanPinOverload per pair beyond 4), with PE at the 5th pair exempt from the count
    //  - the PE-per-day cap (+SanPinOverload per PE pair-slot beyond MaxPePerDay)
    //  - PE should be the day's last lesson (+PeNotLastPenalty per PE pair with any later class).
    public static int Score_S5_SanPin(
        IReadOnlyList<ScheduleEntry> slot, IReadOnlyList<int> pairs, SolverWeights w)
    {
        bool PeFifth(int p) => p == 5 &&
            slot.Where(e => e.PairNumber == p).All(e => e.LessonType == LessonType.PhysicalEducation);

        int load = pairs.Count(p => !PeFifth(p));
        int score = Math.Max(0, load - 4) * w.SanPinOverload;

        var pePairs = slot.Where(e => e.LessonType == LessonType.PhysicalEducation)
            .Select(e => e.PairNumber).Distinct().ToList();
        if (pePairs.Count == 0) return score;

        score += Math.Max(0, pePairs.Count - Math.Max(1, w.MaxPePerDay)) * w.SanPinOverload;

        int lastPair = pairs.Count > 0 ? pairs[^1] : 0;
        foreach (var pp in pePairs)
            if (pp < lastPair) score += w.PeNotLastPenalty;

        return score;
    }

    // S6: consecutive same (subject, lessonType); +scalar uplift for runs of 3+.
    public static int Score_S6_ConsecSameLesson(IReadOnlyList<ScheduleEntry> slot, SolverWeights w)
    {
        int score = 0;
        foreach (var grp in slot.GroupBy(e => (e.SubjectId, e.LessonType)))
        {
            var sp = grp.Select(e => e.PairNumber).Distinct().OrderBy(p => p).ToList();
            int pen = grp.Key.LessonType switch
            {
                LessonType.Lecture   => w.ConsecLecture,
                LessonType.Practical => w.ConsecPractical,
                LessonType.Seminar   => w.ConsecSeminar,
                LessonType.Lab       => w.ConsecLab,
                // PE is the exception: a negative weight REWARDS consecutive PE, so two PE on one day
                // (where MaxPePerDay allows it) get paired into a single block rather than scattered.
                LessonType.PhysicalEducation => -w.PeConsecutiveReward,
                _                    => 0
            };
            for (var k = 0; k < sp.Count - 1; k++)
            {
                if (sp[k + 1] != sp[k] + 1) continue;
                score += pen;
                if (k + 2 < sp.Count && sp[k + 2] == sp[k + 1] + 1 && w.ConsecRunScalar > 1)
                    score += pen * (w.ConsecRunScalar - 1);
            }
        }
        return score;
    }

    // S7: prefer middle pairs (3–4); penalise early (1–2) and late (5+) pairs proportionally.
    public static int Score_S7_TimeOfDay(IReadOnlyList<int> pairs, SolverWeights w)
    {
        int score = 0;
        foreach (var p in pairs)
        {
            int p0 = p - 1;
            if (p0 < 2)      score += w.EarlyPair * (2 - p0);
            else if (p0 > 3) score += w.LatePair  * (p0 - 3);
            else             score += w.MiddlePair;
        }
        return score;
    }

    // S8: penalise each occupied pair-slot on Saturday (when penalty > 0).
    public static int Score_S8_Saturday(RussianDayOfWeek day, int pairCount, SolverWeights w)
        => (day == RussianDayOfWeek.Saturday && w.SaturdayPenalty > 0)
            ? pairCount * w.SaturdayPenalty
            : 0;

    // S9: per-entry penalty when room's department faculty != subject's department faculty.
    public static int Score_S9_DeptMismatch(IReadOnlyList<ScheduleEntry> entries, ScoreContext ctx, SolverWeights w)
    {
        if (w.DepartmentMismatchPenalty <= 0) return 0;
        if (ctx.RoomDeptFacultyId == null || ctx.SubjectDeptFacultyId == null) return 0;

        int score = 0;
        foreach (var e in entries)
        {
            if (e.RoomId == null) continue;
            if (!ctx.RoomDeptFacultyId.TryGetValue(e.RoomId.Value, out var roomFac) || !roomFac.HasValue) continue;
            if (!ctx.SubjectDeptFacultyId.TryGetValue(e.SubjectId, out var subjFac) || !subjFac.HasValue) continue;
            if (roomFac.Value != subjFac.Value)
                score += w.DepartmentMismatchPenalty;
        }
        return score;
    }

    public const double WalkMetersPerMinute = WalkSpeedMperMin;
    public const int UnreachableDistance = int.MaxValue / 2;

    public static int RoomDistanceMeters(Guid roomA, Guid roomB, ScoreContext ctx)
        => TravelDistance(roomA, roomB, ctx);

    private static int TravelDistance(Guid roomA, Guid roomB, ScoreContext ctx)
    {
        if (!ctx.RoomToBuilding.TryGetValue(roomA, out var bldA) ||
            !ctx.RoomToBuilding.TryGetValue(roomB, out var bldB))
            return 0;
        if (bldA == bldB)
            return ctx.RoomDistances.TryGetValue((roomA, roomB), out int d) ? d : 0;

        if (!ctx.BuildingDistances.TryGetValue((bldA, bldB), out int bd))
            return int.MaxValue / 2;

        int entryA = (ctx.RoomEntryDistances != null && ctx.RoomEntryDistances.TryGetValue(roomA, out var ea)) ? ea : 0;
        int entryB = (ctx.RoomEntryDistances != null && ctx.RoomEntryDistances.TryGetValue(roomB, out var eb)) ? eb : 0;
        return entryA + bd + entryB;
    }

    // Runs Dijkstra on the floor plan graph; returns room-to-room distances.
    internal static IReadOnlyDictionary<(Guid, Guid), int> ComputeRoomDistances(
        IEnumerable<FloorPlanNode> nodes, IEnumerable<FloorPlanEdge> edges, int stairFloorMeters = 20)
    {
        var nodeList = nodes.ToList();
        if (nodeList.Count == 0) return new Dictionary<(Guid, Guid), int>();

        var floorById = nodeList.ToDictionary(n => n.Id, n => n.Floor);
        var adj = nodeList.ToDictionary(n => n.Id, _ => new List<(Guid, int)>());
        foreach (var e in edges)
        {
            if (!adj.ContainsKey(e.FromNodeId) || !adj.ContainsKey(e.ToNodeId)) continue;

            // Stacked staircase/elevator nodes share x/y, so a cross-floor edge's stored length is
            // ~0. Replace it with a fixed per-floor cost so climbing actually carries a penalty.
            int floorDiff = Math.Abs(floorById[e.FromNodeId] - floorById[e.ToNodeId]);
            int weight = floorDiff > 0 ? floorDiff * stairFloorMeters : e.DistanceMeters;

            adj[e.FromNodeId].Add((e.ToNodeId, weight));
            adj[e.ToNodeId].Add((e.FromNodeId, weight));
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

    public static IReadOnlyDictionary<Guid, int> ComputeRoomEntryDistances(
        IEnumerable<FloorPlanNode> nodes, IEnumerable<FloorPlanEdge> edges, int stairFloorMeters = 20)
    {
        var nodeList = nodes.ToList();
        if (nodeList.Count == 0) return new Dictionary<Guid, int>();

        var floorById = nodeList.ToDictionary(n => n.Id, n => n.Floor);
        var adj = nodeList.ToDictionary(n => n.Id, _ => new List<(Guid, int)>());
        foreach (var e in edges)
        {
            if (!adj.ContainsKey(e.FromNodeId) || !adj.ContainsKey(e.ToNodeId)) continue;
            int floorDiff = Math.Abs(floorById[e.FromNodeId] - floorById[e.ToNodeId]);
            int weight = floorDiff > 0 ? floorDiff * stairFloorMeters : e.DistanceMeters;
            adj[e.FromNodeId].Add((e.ToNodeId, weight));
            adj[e.ToNodeId].Add((e.FromNodeId, weight));
        }

        var entrancesByBuilding = nodeList
            .Where(n => n.NodeType == FloorPlanNodeType.Entrance)
            .GroupBy(n => n.BuildingId)
            .ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToList());

        var result = new Dictionary<Guid, int>();
        var allIds = nodeList.Select(n => n.Id).ToArray();

        foreach (var (buildingId, entranceIds) in entrancesByBuilding)
        {
            var roomNodes = nodeList
                .Where(n => n.BuildingId == buildingId
                            && n.NodeType == FloorPlanNodeType.Room
                            && n.RoomId.HasValue)
                .ToList();
            if (roomNodes.Count == 0) continue;

            foreach (var entranceId in entranceIds)
            {
                var dist = Dijkstra(entranceId, adj, allIds);
                foreach (var rn in roomNodes)
                {
                    if (!dist.TryGetValue(rn.Id, out int d) || d >= int.MaxValue / 2) continue;
                    var rid = rn.RoomId!.Value;
                    if (!result.TryGetValue(rid, out int cur) || d < cur)
                        result[rid] = d;
                }
            }
        }
        return result;
    }

    // Shortest metres from any node on (building, floor) to any Entrance node in the same building.
    public static IReadOnlyDictionary<(Guid buildingId, int floor), int> ComputeZoneEntryDistances(
        IEnumerable<FloorPlanNode> nodes, IEnumerable<FloorPlanEdge> edges, int stairFloorMeters = 20)
    {
        var nodeList = nodes.ToList();
        if (nodeList.Count == 0) return new Dictionary<(Guid, int), int>();

        var floorById = nodeList.ToDictionary(n => n.Id, n => n.Floor);
        var adj = nodeList.ToDictionary(n => n.Id, _ => new List<(Guid, int)>());
        foreach (var e in edges)
        {
            if (!adj.ContainsKey(e.FromNodeId) || !adj.ContainsKey(e.ToNodeId)) continue;
            int floorDiff = Math.Abs(floorById[e.FromNodeId] - floorById[e.ToNodeId]);
            int weight = floorDiff > 0 ? floorDiff * stairFloorMeters : e.DistanceMeters;
            adj[e.FromNodeId].Add((e.ToNodeId, weight));
            adj[e.ToNodeId].Add((e.FromNodeId, weight));
        }

        var entrances = nodeList.Where(n => n.NodeType == FloorPlanNodeType.Entrance).ToList();
        var result = new Dictionary<(Guid, int), int>();
        var allIds = nodeList.Select(n => n.Id).ToArray();

        foreach (var entrance in entrances)
        {
            var dist = Dijkstra(entrance.Id, adj, allIds);
            var ebid = entrance.BuildingId;
            foreach (var n in nodeList)
            {
                if (n.BuildingId != ebid) continue;
                if (!dist.TryGetValue(n.Id, out int d) || d >= int.MaxValue / 2) continue;
                var key = (ebid, n.Floor);
                if (!result.TryGetValue(key, out int cur) || d < cur)
                    result[key] = d;
            }
        }
        return result;
    }

    public static IReadOnlyDictionary<(Guid, Guid), int> ComputeAllPairsBuildingDistances(
        IEnumerable<BuildingDistance> directEdges)
    {
        var edges = directEdges.ToList();
        if (edges.Count == 0) return new Dictionary<(Guid, Guid), int>();

        var adj = new Dictionary<Guid, List<(Guid, int)>>();
        void AddEdge(Guid a, Guid b, int d)
        {
            if (!adj.TryGetValue(a, out var lst)) adj[a] = lst = new List<(Guid, int)>();
            lst.Add((b, d));
        }
        foreach (var e in edges)
        {
            AddEdge(e.FromBuildingId, e.ToBuildingId, e.DistanceMeters);
            AddEdge(e.ToBuildingId, e.FromBuildingId, e.DistanceMeters);
        }

        var allBuildings = adj.Keys.ToArray();
        var result = new Dictionary<(Guid, Guid), int>();
        foreach (var src in allBuildings)
        {
            var dist = Dijkstra(src, adj, allBuildings);
            foreach (var tgt in allBuildings)
            {
                if (tgt == src) continue;
                if (dist.TryGetValue(tgt, out int d) && d < int.MaxValue / 2)
                    result[(src, tgt)] = d;
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
