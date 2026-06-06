using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.StudyPlans;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Internal;

// Snapshot of everything the scheduler/LNS needs about a single Schedule. Loaded once per
// generation run; threaded read-only into both the CP-SAT seeding pass and any LNS polish pass.
public record SharedData(
    Schedule Schedule,
    List<Room> Rooms,
    List<Teacher> Teachers,
    List<StudentGroup> Groups,
    HashSet<Guid> GroupIds,
    List<StudyPlan> StudyPlans,
    List<TeacherSubject> TeacherSubjects,
    Dictionary<Guid, Subject> SubjectsById,
    Dictionary<Guid, Guid?> SubjectFacultyIds,
    Dictionary<Guid, int> GroupSizes,
    List<BuildingDistance> BuildingDistances,
    List<FloorPlanNode> FloorPlanNodes,
    List<FloorPlanEdge> FloorPlanEdges,
    List<UniScheduler.Domain.Entities.TeacherAvailability> TeacherAvailabilities,
    List<PairTimeSlot> PairSlots,
    int PairsPerDay,
    List<int> BreakMinutes,
    List<SchedulerRoomDistance> RoomDistList,
    IReadOnlyDictionary<Guid, int> EntryDistByRoom,
    IReadOnlyDictionary<(Guid buildingId, int floor), int> ZoneEntryDistByZone,
    List<SchedulerBuildingDistance> BldgDistList,
    ScoreContext ScoreCtx,
    SolverWeights Weights);

public static class ScheduleBuildContext
{
    public static async Task<SharedData> LoadSharedDataAsync(
        IApplicationDbContext db, Schedule schedule, SolverWeights weights, CancellationToken ct)
    {
        var rooms = await db.Rooms.Include(r => r.Building).Include(r => r.Department).Where(r => r.IsEnabled).ToListAsync(ct);
        var distributedRoom = await EnsureDistributedRoomAsync(db, ct);
        if (distributedRoom != null && rooms.All(r => r.Id != distributedRoom.Id))
            rooms.Add(distributedRoom);
        var teachers = await db.Teachers.ToListAsync(ct);

        var groupsQuery = db.StudentGroups.Include(g => g.BlockedDays).AsQueryable();
        if (schedule.FacultyId.HasValue && !schedule.AllowCrossFacultyLessons)
            groupsQuery = groupsQuery.Where(g => g.FacultyId == schedule.FacultyId);
        var groups = await groupsQuery.ToListAsync(ct);
        var groupIds = groups.Select(g => g.Id).ToHashSet();

        var studyPlans = await StudyPlanQ.BaseQuery(db)
            .Where(sp => sp.AcademicYear == schedule.AcademicYear && sp.Term == schedule.Term)
            .ToListAsync(ct);

        var subjectIds = studyPlans.SelectMany(sp => sp.Entries.Select(e => e.SubjectId)).ToHashSet();
        var teacherSubjects = await db.TeacherSubjects
            .Where(ts => subjectIds.Contains(ts.SubjectId))
            .ToListAsync(ct);
        var subjectsWithDepts = await db.Subjects
            .Include(s => s.Department)
            .Where(s => subjectIds.Contains(s.Id))
            .ToListAsync(ct);
        var subjectFacultyIds = subjectsWithDepts.ToDictionary(s => s.Id, s => s.Department?.FacultyId);
        var subjectsById = subjectsWithDepts.ToDictionary(s => s.Id);
        var groupSizes = groups.ToDictionary(g => g.Id, g => g.StudentCount);

        var distances = await db.BuildingDistances.ToListAsync(ct);
        var floorPlanNodes = await db.FloorPlanNodes.ToListAsync(ct);
        var floorPlanEdges = await db.FloorPlanEdges.ToListAsync(ct);
        var teacherAvail = await db.TeacherAvailabilities.ToListAsync(ct);
        var pairSlots = await db.PairTimeSlots.OrderBy(p => p.PairNumber).ToListAsync(ct);
        int pairsPerDay = pairSlots.Count > 0 ? pairSlots.Max(p => p.PairNumber) : 6;
        var breakMinutes = ScheduleScoreCalculator.ComputeBreakMinutes(pairSlots);

        var roomDistMap = ScheduleScoreCalculator.ComputeRoomDistances(floorPlanNodes, floorPlanEdges, weights.StairFloorMeters);
        var roomDistList = roomDistMap
            .Select(kv => new SchedulerRoomDistance(kv.Key.Item1, kv.Key.Item2, kv.Value))
            .ToList();

        var entryDistByRoom = ScheduleScoreCalculator.ComputeRoomEntryDistances(
            floorPlanNodes, floorPlanEdges, weights.StairFloorMeters);
        var zoneEntryDist = ScheduleScoreCalculator.ComputeZoneEntryDistances(
            floorPlanNodes, floorPlanEdges, weights.StairFloorMeters);

        var bldDistMap = ScheduleScoreCalculator.ComputeAllPairsBuildingDistances(distances);
        var bldDistList = bldDistMap
            .Select(kv => new SchedulerBuildingDistance(kv.Key.Item1, kv.Key.Item2, kv.Value))
            .ToList();

        var scoreCtx = ScheduleScoreCalculator.BuildScoreContext(
            floorPlanNodes, floorPlanEdges, distances, rooms, pairSlots, subjectsWithDepts, weights);

        return new SharedData(schedule, rooms, teachers, groups, groupIds, studyPlans, teacherSubjects,
            subjectsById, subjectFacultyIds, groupSizes, distances, floorPlanNodes, floorPlanEdges,
            teacherAvail, pairSlots, pairsPerDay, breakMinutes, roomDistList, entryDistByRoom,
            zoneEntryDist, bldDistList, scoreCtx, weights);
    }

    // Per-university distributed sentinel room (placeholder for classes with no fixed location,
    // e.g. parallel language streams). Buildings are tenant-scoped by the global query filter.
    private static async Task<Room?> EnsureDistributedRoomAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var buildingIds = await db.Buildings.Select(b => b.Id).ToListAsync(ct);
        if (buildingIds.Count == 0) return null;

        var existing = await db.Rooms.FirstOrDefaultAsync(r => r.IsDistributed && buildingIds.Contains(r.BuildingId), ct);
        if (existing != null) return existing;

        var room = new Room
        {
            BuildingId = buildingIds[0],
            Number = "— по подгруппам —",
            RoomType = RoomType.RegularCabinet,
            Capacity = 0,
            IsDistributed = true,
            IsEnabled = true
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        return room;
    }

    public static SchedulerInput BuildSchedulerInputForPlan(
        Guid scheduleId, SharedData shared, IReadOnlyList<SchedulerRequirement> requirements,
        IReadOnlyList<SchedulerRoomBlock> roomBlocks, IReadOnlyList<SchedulerBlock> extraTeacherBlocks,
        int timeoutSeconds, SolverWeights weights,
        IReadOnlyList<SchedulerPin>? pinnings = null,
        IReadOnlyList<SchedulerHint>? hints = null,
        bool isRepairSolve = false,
        bool skipTravel = false,
        IReadOnlyList<SchedulerFreedReq>? freedReqs = null,
        long overflowPenalty = 0)
    {
        var requirementGroupIds = requirements.SelectMany(r => r.GroupIds).ToHashSet();
        var relevantGroups = shared.Groups.Where(g => requirementGroupIds.Contains(g.Id)).ToList();

        var requirementTeacherIds = requirements.Select(r => r.TeacherId).ToHashSet();
        foreach (var b in extraTeacherBlocks) requirementTeacherIds.Add(b.TeacherId);
        var relevantTeachers = shared.Teachers.Where(t => requirementTeacherIds.Contains(t.Id)).ToList();

        var teacherBlocks = shared.TeacherAvailabilities
            .Where(ta => requirementTeacherIds.Contains(ta.TeacherId))
            .Select(b => new SchedulerBlock(b.TeacherId, b.DayOfWeek, b.PairNumber, b.WeekType))
            .Concat(extraTeacherBlocks)
            .ToList();

        var zoneEntryList = shared.ZoneEntryDistByZone
            .Select(kv => new SchedulerZoneEntryDistance(kv.Key.buildingId, kv.Key.floor, kv.Value))
            .ToList();

        return new SchedulerInput(
            scheduleId,
            shared.Rooms.Select(r => new SchedulerRoom(r.Id, r.BuildingId, r.RoomType, r.Capacity, r.HasProjector, r.HasComputers, r.HasLab, r.IsOnline,
                r.Floor, r.AllowedLessonTypes, r.Department?.FacultyId, r.IsDistributed,
                EntryDistanceMeters: shared.EntryDistByRoom.TryGetValue(r.Id, out var ed) ? ed : 0)).ToList(),
            relevantTeachers.Select(t => new SchedulerTeacher(t.Id)).ToList(),
            relevantGroups.Select(g => new SchedulerGroup(g.Id, g.StudentCount,
                g.BlockedDays.Select(bd => (int)bd.DayOfWeek - 1).ToList())).ToList(),
            requirements,
            shared.BldgDistList,
            teacherBlocks,
            PairsPerDay: shared.PairsPerDay,
            BreakMinutesBetweenPairs: shared.BreakMinutes,
            SolverTimeoutSeconds: timeoutSeconds,
            RoomDistances: shared.RoomDistList,
            Weights: weights,
            ZoneEntryDistances: zoneEntryList,
            RoomBlocks: roomBlocks,
            Pinnings: pinnings,
            Hints: hints,
            IsRepairSolve: isRepairSolve,
            SkipTravel: skipTravel,
            FreedReqs: freedReqs,
            OverflowPenalty: overflowPenalty
        );
    }
}
