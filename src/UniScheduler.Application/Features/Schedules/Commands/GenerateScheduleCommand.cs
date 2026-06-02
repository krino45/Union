using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.StudyPlans;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record GenerateScheduleCommand(
    Guid ScheduleId,
    int SolverTimeoutSeconds = 60,
    IProgress<string>? Progress = null,
    IReadOnlyList<Guid>? PlanIds = null
) : IRequest<GenerateScheduleResult>;

public class GenerateScheduleCommandHandler : IRequestHandler<GenerateScheduleCommand, GenerateScheduleResult>
{
    private readonly IApplicationDbContext db;
    private readonly ISchedulerService scheduler;

    public GenerateScheduleCommandHandler(IApplicationDbContext db, ISchedulerService scheduler)
    {
        this.db = db;
        this.scheduler = scheduler;
    }

    public async Task<GenerateScheduleResult> Handle(GenerateScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await db.Schedules
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);

        var p = request.Progress;
        p?.Report("Загрузка данных...");

        var settingsEntity = await db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        var weights = settingsEntity == null ? new SolverWeights() : new SolverWeights(
            settingsEntity.StudentWindow, settingsEntity.TeacherWindow, settingsEntity.ActiveDay, settingsEntity.SanPinOverload,
            settingsEntity.ConsecLecture, settingsEntity.ConsecSeminar, settingsEntity.ConsecPractical, settingsEntity.ConsecLab,
            settingsEntity.EarlyPair, settingsEntity.MiddlePair, settingsEntity.LatePair, settingsEntity.ConsecRunScalar,
            settingsEntity.SaturdayPenalty, settingsEntity.DepartmentMismatchPenalty, settingsEntity.WalkingPenaltyMax,
            settingsEntity.StairFloorMeters);

        var shared = await LoadSharedDataAsync(schedule, weights, cancellationToken);

        List<StudyPlan> plansToRun;
        if (request.PlanIds is { Count: > 0 })
        {
            var byId = shared.StudyPlans.ToDictionary(sp => sp.Id);
            plansToRun = request.PlanIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            if (plansToRun.Count == 0)
                return new GenerateScheduleResult(false, "Infeasible",
                    "Ни один из указанных учебных планов не найден для этого расписания.", 0);
        }
        else
        {
            plansToRun = shared.StudyPlans
                .OrderByDescending(sp => sp.Groups.Count)
                .ThenByDescending(sp => sp.Entries.Count)
                .ToList();
        }

        var plansToRunIds = plansToRun.Select(sp => sp.Id).ToHashSet();

        var groupToPlanId = new Dictionary<Guid, Guid>();
        foreach (var sp in shared.StudyPlans)
            foreach (var g in sp.Groups)
                groupToPlanId[g.StudentGroupId] = sp.Id;

        var existing = await db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(cancellationToken);

        var keptEntries = new List<ScheduleEntry>();
        var entriesToDelete = new List<ScheduleEntry>();
        foreach (var e in existing)
        {
            var firstGroupId = e.StudentGroups.FirstOrDefault()?.StudentGroupId;
            Guid? planId = firstGroupId.HasValue && groupToPlanId.TryGetValue(firstGroupId.Value, out var pid)
                ? pid : (Guid?)null;
            if (planId.HasValue && plansToRunIds.Contains(planId.Value))
                entriesToDelete.Add(e);
            else
                keptEntries.Add(e);
        }
        if (entriesToDelete.Count > 0)
        {
            var deleteIds = entriesToDelete.Select(e => e.Id).ToList();
            var relatedRequests = await db.RescheduleRequests
                .Where(r => deleteIds.Contains(r.OriginalEntryId))
                .ToListAsync(cancellationToken);
            db.RescheduleRequests.RemoveRange(relatedRequests);
            db.ScheduleEntries.RemoveRange(entriesToDelete);
            await db.SaveChangesAsync(cancellationToken);
        }

        var roomBlocks = keptEntries
            .Where(e => e.RoomId.HasValue && !e.IsOnline)
            .Select(e => new SchedulerRoomBlock(e.RoomId!.Value, e.DayOfWeek, e.PairNumber, e.WeekType))
            .ToList();
        var dynamicTeacherBlocks = keptEntries
            .Select(e => new SchedulerBlock(e.TeacherId, e.DayOfWeek, e.PairNumber, e.WeekType))
            .ToList();

        int totalPlaced = 0;
        var perPlanMessages = new List<string>();
        int parallelSeq = 1;

        for (int i = 0; i < plansToRun.Count; i++)
        {
            var plan = plansToRun[i];
            p?.Report($"План {i + 1}/{plansToRun.Count}: {plan.Name ?? plan.Id.ToString()[..8]}...");

            var requirements = BuildRequirementsForPlan(plan, shared, ref parallelSeq);
            if (requirements.Count == 0)
            {
                perPlanMessages.Add($"{plan.Name ?? plan.Id.ToString()[..8]}: 0 занятий (пропущен)");
                continue;
            }

            var input = BuildSchedulerInputForPlan(
                schedule.Id, shared, requirements, roomBlocks, dynamicTeacherBlocks,
                request.SolverTimeoutSeconds, weights);

            var output = await scheduler.SolveAsync(input, cancellationToken, p);

            if (output.Status == SolverStatus.Infeasible)
            {
                perPlanMessages.Add($"{plan.Name ?? plan.Id.ToString()[..8]}: НЕРАЗРЕШИМО: {output.Message}");
                schedule.GenerationNotes = $"План {plan.Name ?? plan.Id.ToString()[..8]} неразрешим. {string.Join(" | ", perPlanMessages)}";
                await db.SaveChangesAsync(cancellationToken);
                return new GenerateScheduleResult(false, "Infeasible",
                    $"Plan {plan.Name ?? plan.Id.ToString()[..8]}: {output.Message}", totalPlaced);
            }

            if (output.Status == SolverStatus.Unknown)
            {
                perPlanMessages.Add($"{plan.Name ?? plan.Id.ToString()[..8]}: ТАЙМАУТ: решение не найдено за {request.SolverTimeoutSeconds}с");
                continue;
            }

            var parallelGuids = new Dictionary<int, Guid>();
            var newEntries = new List<ScheduleEntry>();
            foreach (var assignment in output.Assignments)
            {
                var req = input.Requirements[assignment.RequirementIndex];
                Guid? parallelGroupId = null;
                if (req.ParallelKey is int pk)
                {
                    if (!parallelGuids.TryGetValue(pk, out var g)) parallelGuids[pk] = g = Guid.NewGuid();
                    parallelGroupId = g;
                }
                var entry = new ScheduleEntry
                {
                    ScheduleId = request.ScheduleId,
                    SubjectId = req.SubjectId,
                    TeacherId = req.TeacherId,
                    RoomId = req.IsOnline ? null : assignment.RoomId,
                    DayOfWeek = assignment.Day,
                    PairNumber = assignment.PairNumber,
                    WeekType = assignment.WeekType,
                    LessonType = req.LessonType,
                    IsOnline = req.IsOnline,
                    ParallelGroupId = parallelGroupId,
                    SubgroupLabel = req.SubgroupLabel
                };
                db.ScheduleEntries.Add(entry);
                newEntries.Add(entry);
                foreach (var groupId in req.GroupIds)
                {
                    db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup
                    {
                        ScheduleEntry = entry,
                        StudentGroupId = groupId
                    });
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            totalPlaced += newEntries.Count;
            perPlanMessages.Add($"{plan.Name ?? plan.Id.ToString()[..8]}: {newEntries.Count} занятий ({output.Status})");

            foreach (var e in newEntries)
            {
                if (e.RoomId.HasValue && !e.IsOnline)
                    roomBlocks.Add(new SchedulerRoomBlock(e.RoomId.Value, e.DayOfWeek, e.PairNumber, e.WeekType));
                dynamicTeacherBlocks.Add(new SchedulerBlock(e.TeacherId, e.DayOfWeek, e.PairNumber, e.WeekType));
            }
        }

        schedule.GeneratedAt = DateTime.UtcNow;
        schedule.GenerationNotes = string.Join(" | ", perPlanMessages);
        await db.SaveChangesAsync(cancellationToken);

        if (totalPlaced == 0)
        {
            return new GenerateScheduleResult(false, "Unknown",
                "Ни один учебный план не уложился в таймаут. Увеличьте таймаут или сузьте состав планов.",
                0);
        }

        p?.Report("Вычисление оценки...");
        var scoreEntries = await db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(cancellationToken);
        schedule.BaseScore = ScheduleScoreCalculator.Compute(scoreEntries, shared.ScoreCtx);
        await db.SaveChangesAsync(cancellationToken);

        return new GenerateScheduleResult(true, "Feasible",
            string.Join(" | ", perPlanMessages), totalPlaced);
    }

    private record SharedData(
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
        ScoreContext ScoreCtx);

    private async Task<SharedData> LoadSharedDataAsync(Schedule schedule, SolverWeights weights, CancellationToken ct)
    {
        var rooms = await db.Rooms.Include(r => r.Building).Include(r => r.Department).Where(r => r.IsEnabled).ToListAsync(ct);
        var distributedRoom = await EnsureDistributedRoomAsync(ct);
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
            zoneEntryDist, bldDistList, scoreCtx);
    }

    private static List<SchedulerRequirement> BuildRequirementsForPlan(
        StudyPlan plan, SharedData shared, ref int parallelSeq)
    {
        var requirements = new List<SchedulerRequirement>();
        int idx = 0;
        int studyWeeks = StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan);
        var planGroupIds = plan.Groups
            .Select(g => g.StudentGroupId)
            .Where(gid => shared.GroupIds.Contains(gid))
            .ToList();
        if (planGroupIds.Count == 0) return requirements;

        foreach (var entry in plan.Entries)
        {
            shared.SubjectFacultyIds.TryGetValue(entry.SubjectId, out var subjFacultyId);
            shared.SubjectsById.TryGetValue(entry.SubjectId, out var subj);

            AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lecture,
                entry.LectureHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: true, isLab: false, subjFacultyId);
            AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Practical,
                entry.PracticalHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: false, isLab: false, subjFacultyId);

            if (subj is { AllowsSubgroups: true } &&
                AddSubgroupLabRequirements(requirements, ref idx, ref parallelSeq, entry.SubjectId,
                    entry.LabHours, studyWeeks, planGroupIds, shared.TeacherSubjects,
                    subj.SubgroupCount, shared.GroupSizes, subjFacultyId))
            {
                // emitted as subgroups
            }
            else
            {
                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lab,
                    entry.LabHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: false, isLab: true, subjFacultyId);
            }

            AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Seminar,
                entry.SeminarHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: false, isLab: false, subjFacultyId);

            AddLanguageRequirements(requirements, ref idx, ref parallelSeq, entry.SubjectId,
                entry.LanguageHours, studyWeeks, planGroupIds, shared.TeacherSubjects, subjFacultyId);
        }

        return requirements;
    }

    private static SchedulerInput BuildSchedulerInputForPlan(
        Guid scheduleId, SharedData shared, List<SchedulerRequirement> requirements,
        IReadOnlyList<SchedulerRoomBlock> roomBlocks, IReadOnlyList<SchedulerBlock> extraTeacherBlocks,
        int timeoutSeconds, SolverWeights weights)
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
            RoomBlocks: roomBlocks
        );
    }

    // Creates requirements for one lesson type based on total semester hours
    private static void AddRequirements(
        List<SchedulerRequirement> requirements, ref int idx,
        Guid subjectId, LessonType lt, double totalHours, int studyWeeks,
        List<Guid> planGroupIds, List<TeacherSubject> teacherSubjects,
        bool merged, bool isLab, Guid? subjectFacultyId = null)
    {
        if (totalHours <= 0) return;
        var teachers = teacherSubjects
            .Where(ts => ts.SubjectId == subjectId && ts.LessonType == lt)
            .Select(ts => ts.TeacherId).ToList();
        if (teachers.Count == 0) return;

        foreach (var wt in HoursToWeekTypes(totalHours, studyWeeks))
        {
            if (merged)
            {
                // Lectures: groups split across available teachers
                var chunks = SplitRoundRobin(planGroupIds, teachers.Count);
                for (int i = 0; i < teachers.Count; i++)
                {
                    if (chunks[i].Count == 0) continue;
                    requirements.Add(new SchedulerRequirement(idx++, chunks[i], subjectId, lt, teachers[i], wt, false, lt == LessonType.Lecture, false, isLab, subjectFacultyId));
                }
            }
            else
            {
                // Per-group: each group assigned round-robin to a teacher
                for (int gi = 0; gi < planGroupIds.Count; gi++)
                {
                    var teacherId = teachers[gi % teachers.Count];
                    requirements.Add(new SchedulerRequirement(idx++, [planGroupIds[gi]], subjectId, lt, teacherId, wt, false, false, false, isLab, subjectFacultyId));
                }
            }
        }
    }

    // Converts total semester hours to a list of WeekType occurrences per week
    private static List<WeekType> HoursToWeekTypes(double totalHours, int studyWeeks)
    {
        if (studyWeeks <= 0 || totalHours <= 0) return [];
        double pairsPerWeek = totalHours / 2.0 / studyWeeks;
        var result = new List<WeekType>();
        int whole = (int)pairsPerWeek;
        for (int i = 0; i < whole; i++) result.Add(WeekType.Both);
        double frac = pairsPerWeek - whole;
        if (frac >= 0.25) result.Add(WeekType.Odd);
        return result;
    }

    private static List<List<Guid>> SplitRoundRobin(List<Guid> items, int buckets)
    {
        var result = Enumerable.Range(0, buckets).Select(_ => new List<Guid>()).ToList();
        for (int i = 0; i < items.Count; i++)
            result[i % buckets].Add(items[i]);
        return result;
    }

    // Emits parallel language streams (one requirement per language teacher) for each group.
    // All streams of a group share a ParallelKey, are co-scheduled, and use the distributed room.
    private static void AddLanguageRequirements(
        List<SchedulerRequirement> requirements, ref int idx, ref int parallelSeq,
        Guid subjectId, double totalHours, int studyWeeks,
        List<Guid> planGroupIds, List<TeacherSubject> teacherSubjects, Guid? subjectFacultyId)
    {
        if (totalHours <= 0) return;
        var teachers = teacherSubjects
            .Where(ts => ts.SubjectId == subjectId && ts.LessonType == LessonType.Language)
            .Select(ts => ts.TeacherId).Distinct().ToList();
        if (teachers.Count == 0) return;

        foreach (var wt in HoursToWeekTypes(totalHours, studyWeeks))
        {
            foreach (var gId in planGroupIds)
            {
                int pkey = parallelSeq++;
                for (int i = 0; i < teachers.Count; i++)
                {
                    requirements.Add(new SchedulerRequirement(
                        idx++, new[] { gId }, subjectId, LessonType.Language, teachers[i], wt,
                        IsOnline: false, NeedsProjector: false, NeedsComputers: false, NeedsLab: false,
                        SubjectFacultyId: subjectFacultyId,
                        ParallelKey: pkey,
                        SubgroupLabel: $"Поток {i + 1}",
                        HeadcountOverride: null,
                        RequiresDistributedRoom: true));
                }
            }
        }
    }

    // Emits a lab as parallel subgroups, each its own teacher and (real) room, seating a fraction of
    // the group. Returns false when the split is impossible (needs ≥2 distinct lab teachers).
    private static bool AddSubgroupLabRequirements(
        List<SchedulerRequirement> requirements, ref int idx, ref int parallelSeq,
        Guid subjectId, double totalHours, int studyWeeks,
        List<Guid> planGroupIds, List<TeacherSubject> teacherSubjects,
        int subgroupCount, Dictionary<Guid, int> groupSizes, Guid? subjectFacultyId)
    {
        if (totalHours <= 0) return false;
        var teachers = teacherSubjects
            .Where(ts => ts.SubjectId == subjectId && ts.LessonType == LessonType.Lab)
            .Select(ts => ts.TeacherId).Distinct().ToList();
        int n = Math.Min(Math.Max(2, subgroupCount), teachers.Count);
        if (n < 2) return false;

        foreach (var wt in HoursToWeekTypes(totalHours, studyWeeks))
        {
            foreach (var gId in planGroupIds)
            {
                int pkey = parallelSeq++;
                int total = groupSizes.TryGetValue(gId, out var sz) ? sz : 0;
                int per = total > 0 ? (int)Math.Ceiling(total / (double)n) : 0;
                for (int s = 0; s < n; s++)
                {
                    requirements.Add(new SchedulerRequirement(
                        idx++, new[] { gId }, subjectId, LessonType.Lab, teachers[s], wt,
                        IsOnline: false, NeedsProjector: false, NeedsComputers: false, NeedsLab: true,
                        SubjectFacultyId: subjectFacultyId,
                        ParallelKey: pkey,
                        SubgroupLabel: $"Подгр. {s + 1}",
                        HeadcountOverride: per > 0 ? per : (int?)null,
                        RequiresDistributedRoom: false));
                }
            }
        }
        return true;
    }

    // Gets or creates the per-university distributed sentinel room (placeholder for classes with no
    // fixed location). Buildings are already scoped to the current university by the query filter.
    private async Task<Room?> EnsureDistributedRoomAsync(CancellationToken ct)
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

}
