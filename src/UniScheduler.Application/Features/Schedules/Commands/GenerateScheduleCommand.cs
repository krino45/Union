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
        db.ScheduleEntries.RemoveRange(existing);

        var settingsEntity = await db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        var weights = settingsEntity == null ? new SolverWeights() : new SolverWeights(
            settingsEntity.StudentWindow, settingsEntity.TeacherWindow, settingsEntity.ActiveDay, settingsEntity.SanPinOverload,
            settingsEntity.ConsecLecture, settingsEntity.ConsecSeminar, settingsEntity.ConsecPractical, settingsEntity.ConsecLab,
            settingsEntity.EarlyPair, settingsEntity.LatePair, settingsEntity.ConsecRunScalar,
            settingsEntity.SaturdayPenalty, settingsEntity.DepartmentMismatchPenalty, settingsEntity.WalkingPenaltyMax);

        var (input, scoreCtx) = await BuildInputAsync(schedule, request.SolverTimeoutSeconds, weights, cancellationToken);

        p?.Report($"Поиск расписания ({input.Requirements.Count} занятий)...");
        var output = await scheduler.SolveAsync(input, cancellationToken, p);

        if (output.Status == SolverStatus.Infeasible)
            return new GenerateScheduleResult(false, "Infeasible", output.Message, 0);

        p?.Report($"Сохранение результатов ({output.Assignments.Count} занятий)...");
        foreach (var assignment in output.Assignments)
        {
            var req = input.Requirements[assignment.RequirementIndex];
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
                IsOnline = req.IsOnline
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

        var distances = await db.BuildingDistances.ToListAsync(ct);
        var floorPlanNodes = await db.FloorPlanNodes.ToListAsync(ct);
        var floorPlanEdges = await db.FloorPlanEdges.ToListAsync(ct);
        var blocks = await db.TeacherAvailabilities.ToListAsync(ct);
        var pairSlots = await db.PairTimeSlots.OrderBy(p => p.PairNumber).ToListAsync(ct);
        int pairsPerDay = pairSlots.Count > 0 ? pairSlots.Max(p => p.PairNumber) : 6;
        var breakMinutes = ScheduleScoreCalculator.ComputeBreakMinutes(pairSlots);

        var requirements = new List<SchedulerRequirement>();
        int idx = 0;

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
                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lecture,
                    entry.LectureHours, studyWeeks, planGroupIds, teacherSubjects, merged: true, isLab: false, subjFacultyId);
                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Practical,
                    entry.PracticalHours, studyWeeks, planGroupIds, teacherSubjects, merged: false, isLab: false, subjFacultyId);
                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lab,
                    entry.LabHours, studyWeeks, planGroupIds, teacherSubjects, merged: false, isLab: true, subjFacultyId);
                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Seminar,
                    entry.SeminarHours, studyWeeks, planGroupIds, teacherSubjects, merged: false, isLab: false, subjFacultyId);
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

        var roomDistMap = ScheduleScoreCalculator.ComputeRoomDistances(floorPlanNodes, floorPlanEdges);
        var roomDistList = roomDistMap
            .Select(kv => new SchedulerRoomDistance(kv.Key.Item1, kv.Key.Item2, kv.Value))
            .ToList();

        var scoreCtx = ScheduleScoreCalculator.BuildScoreContext(
            floorPlanNodes, floorPlanEdges, distances, rooms, pairSlots, subjectsWithDepts, weights);

        var input = new SchedulerInput(
            schedule.Id,
            rooms.Select(r => new SchedulerRoom(r.Id, r.BuildingId, r.RoomType, r.Capacity, r.HasProjector, r.HasComputers, r.HasLab, r.IsOnline,
                r.Floor, r.AllowedLessonTypes, r.Department?.FacultyId)).ToList(),
            teachers.Select(t => new SchedulerTeacher(t.Id)).ToList(),
            groups.Select(g => new SchedulerGroup(g.Id, g.StudentCount,
                g.BlockedDays.Select(bd => (int)bd.DayOfWeek - 1).ToList())).ToList(),
            requirements,
            distances.Select(d => new SchedulerBuildingDistance(d.FromBuildingId, d.ToBuildingId, d.DistanceMeters)).ToList(),
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

}
