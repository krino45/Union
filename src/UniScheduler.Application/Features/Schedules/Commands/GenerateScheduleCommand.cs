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

public record GenerateScheduleCommand(Guid ScheduleId, int SolverTimeoutSeconds = 60, IProgress<string>? Progress = null) : IRequest<GenerateScheduleResult>;

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
        var existing = await db.ScheduleEntries.Where(e => e.ScheduleId == request.ScheduleId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            var existingIds = existing.Select(e => e.Id).ToList();
            var relatedRequests = await db.RescheduleRequests
                .Where(r => existingIds.Contains(r.OriginalEntryId))
                .ToListAsync(cancellationToken);
            db.RescheduleRequests.RemoveRange(relatedRequests);
        }
        db.ScheduleEntries.RemoveRange(existing);

        var settingsEntity = await db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        var weights = settingsEntity == null ? new SolverWeights() : new SolverWeights(
            settingsEntity.StudentWindow, settingsEntity.TeacherWindow, settingsEntity.ActiveDay, settingsEntity.SanPinOverload,
            settingsEntity.ConsecLecture, settingsEntity.ConsecSeminar, settingsEntity.ConsecPractical, settingsEntity.ConsecLab,
            settingsEntity.EarlyPair, settingsEntity.MiddlePair, settingsEntity.LatePair, settingsEntity.ConsecRunScalar,
            settingsEntity.SaturdayPenalty, settingsEntity.DepartmentMismatchPenalty, settingsEntity.WalkingPenaltyMax,
            settingsEntity.StairFloorMeters);

        var (input, scoreCtx) = await BuildInputAsync(schedule, request.SolverTimeoutSeconds, weights, cancellationToken);

        p?.Report($"Поиск расписания ({input.Requirements.Count} занятий)...");
        var output = await scheduler.SolveAsync(input, cancellationToken, p);

        if (output.Status == SolverStatus.Infeasible)
            return new GenerateScheduleResult(false, "Infeasible", output.Message, 0);

        p?.Report($"Сохранение результатов ({output.Assignments.Count} занятий)...");
        var parallelGuids = new Dictionary<int, Guid>();
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
            foreach (var groupId in req.GroupIds)
            {
                db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup
                {
                    ScheduleEntry = entry,
                    StudentGroupId = groupId
                });
            }
        }

        schedule.GeneratedAt = DateTime.UtcNow;
        schedule.GenerationNotes = output.Message;
        await db.SaveChangesAsync(cancellationToken);

        p?.Report("Вычисление оценки...");
        var scoreEntries = await db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(cancellationToken);
        schedule.BaseScore = ScheduleScoreCalculator.Compute(scoreEntries, scoreCtx);
        await db.SaveChangesAsync(cancellationToken);

        return new GenerateScheduleResult(true, output.Status.ToString(), output.Message, output.Assignments.Count);
    }

    private async Task<(SchedulerInput, ScoreContext)> BuildInputAsync(Schedule schedule, int timeout, SolverWeights weights, CancellationToken ct)
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

        // Study plans are the authoritative source for what needs to be scheduled
        var studyPlans = await StudyPlanQ.BaseQuery(db)
            .Where(sp => sp.AcademicYear == schedule.AcademicYear && sp.Term == schedule.Term)
            .ToListAsync(ct);

        // Load teacher-subject assignments and subject departments for all subjects in the plans
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
        var blocks = await db.TeacherAvailabilities.ToListAsync(ct);
        var pairSlots = await db.PairTimeSlots.OrderBy(p => p.PairNumber).ToListAsync(ct);
        int pairsPerDay = pairSlots.Count > 0 ? pairSlots.Max(p => p.PairNumber) : 6;
        var breakMinutes = ScheduleScoreCalculator.ComputeBreakMinutes(pairSlots);

        var requirements = new List<SchedulerRequirement>();
        int idx = 0;
        int parallelSeq = 1;

        foreach (var plan in studyPlans)
        {
            int studyWeeks = StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan);
            var planGroupIds = plan.Groups
                .Select(g => g.StudentGroupId)
                .Where(gid => groupIds.Contains(gid))
                .ToList();
            if (planGroupIds.Count == 0) continue;

            foreach (var entry in plan.Entries)
            {
                subjectFacultyIds.TryGetValue(entry.SubjectId, out var subjFacultyId);
                subjectsById.TryGetValue(entry.SubjectId, out var subj);

                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lecture,
                    entry.LectureHours, studyWeeks, planGroupIds, teacherSubjects, merged: true, isLab: false, subjFacultyId);
                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Practical,
                    entry.PracticalHours, studyWeeks, planGroupIds, teacherSubjects, merged: false, isLab: false, subjFacultyId);

                // Labs split into parallel subgroups when the discipline opts in (requires ≥2 lab teachers).
                if (subj is { AllowsSubgroups: true } &&
                    AddSubgroupLabRequirements(requirements, ref idx, ref parallelSeq, entry.SubjectId,
                        entry.LabHours, studyWeeks, planGroupIds, teacherSubjects, subj.SubgroupCount, groupSizes, subjFacultyId))
                {
                    // emitted as subgroups
                }
                else
                {
                    AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lab,
                        entry.LabHours, studyWeeks, planGroupIds, teacherSubjects, merged: false, isLab: true, subjFacultyId);
                }

                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Seminar,
                    entry.SeminarHours, studyWeeks, planGroupIds, teacherSubjects, merged: false, isLab: false, subjFacultyId);

                // Foreign-language classes: each group's slot is split into parallel streams (one per
                // language teacher) held in the distributed room, since there is no single fixed room.
                AddLanguageRequirements(requirements, ref idx, ref parallelSeq, entry.SubjectId,
                    entry.LanguageHours, studyWeeks, planGroupIds, teacherSubjects, subjFacultyId);
            }
        }

        // Fallback: no study plans configured — create one Both-week requirement per teacher-subject-group
        if (requirements.Count == 0 && groups.Count > 0)
        {
            var fallbackSubjectIds = await db.Subjects
                .Where(s => s.AcademicYear == schedule.AcademicYear && s.Term == schedule.Term)
                .Select(s => s.Id)
                .ToListAsync(ct);
            var fallbackTs = await db.TeacherSubjects
                .Where(ts => fallbackSubjectIds.Contains(ts.SubjectId))
                .ToListAsync(ct);
            foreach (var ts in fallbackTs)
            {
                bool isLab = ts.LessonType == LessonType.Lab;
                bool isLecture = ts.LessonType == LessonType.Lecture;
                var reqGroupIds = groups.Select(g => g.Id).ToList();
                if (isLecture)
                {
                    requirements.Add(new SchedulerRequirement(idx++, reqGroupIds, ts.SubjectId, ts.LessonType,
                        ts.TeacherId, WeekType.Both, false, true, false, false));
                }
                else
                {
                    foreach (var group in groups)
                    {
                        requirements.Add(new SchedulerRequirement(idx++, [group.Id], ts.SubjectId, ts.LessonType,
                            ts.TeacherId, WeekType.Both, false, false, false, isLab));
                    }
                }
            }
        }

        var roomDistMap = ScheduleScoreCalculator.ComputeRoomDistances(floorPlanNodes, floorPlanEdges, weights.StairFloorMeters);
        var roomDistList = roomDistMap
            .Select(kv => new SchedulerRoomDistance(kv.Key.Item1, kv.Key.Item2, kv.Value))
            .ToList();

        var entryDistByRoom = ScheduleScoreCalculator.ComputeRoomEntryDistances(
            floorPlanNodes, floorPlanEdges, weights.StairFloorMeters);

        var bldDistMap = ScheduleScoreCalculator.ComputeAllPairsBuildingDistances(distances);
        var bldDistList = bldDistMap
            .Select(kv => new SchedulerBuildingDistance(kv.Key.Item1, kv.Key.Item2, kv.Value))
            .ToList();

        var scoreCtx = ScheduleScoreCalculator.BuildScoreContext(
            floorPlanNodes, floorPlanEdges, distances, rooms, pairSlots, subjectsWithDepts, weights);

        var input = new SchedulerInput(
            schedule.Id,
            rooms.Select(r => new SchedulerRoom(r.Id, r.BuildingId, r.RoomType, r.Capacity, r.HasProjector, r.HasComputers, r.HasLab, r.IsOnline,
                r.Floor, r.AllowedLessonTypes, r.Department?.FacultyId, r.IsDistributed,
                EntryDistanceMeters: entryDistByRoom.TryGetValue(r.Id, out var ed) ? ed : 0)).ToList(),
            teachers.Select(t => new SchedulerTeacher(t.Id)).ToList(),
            groups.Select(g => new SchedulerGroup(g.Id, g.StudentCount,
                g.BlockedDays.Select(bd => (int)bd.DayOfWeek - 1).ToList())).ToList(),
            requirements,
            bldDistList,
            blocks.Select(b => new SchedulerBlock(b.TeacherId, b.DayOfWeek, b.PairNumber, b.WeekType)).ToList(),
            PairsPerDay: pairsPerDay,
            BreakMinutesBetweenPairs: breakMinutes,
            SolverTimeoutSeconds: timeout,
            RoomDistances: roomDistList,
            Weights: weights
        );
        return (input, scoreCtx);
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
