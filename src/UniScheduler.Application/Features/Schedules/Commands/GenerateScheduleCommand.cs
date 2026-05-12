using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record GenerateScheduleCommand(Guid ScheduleId, int SolverTimeoutSeconds = 60) : IRequest<GenerateScheduleResult>;

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

        var existing = await db.ScheduleEntries.Where(e => e.ScheduleId == request.ScheduleId).ToListAsync(cancellationToken);
        db.ScheduleEntries.RemoveRange(existing);

        var input = await BuildInputAsync(schedule, request.SolverTimeoutSeconds, cancellationToken);
        var output = await scheduler.SolveAsync(input, cancellationToken);

        if (output.Status == SolverStatus.Infeasible)
            return new GenerateScheduleResult(false, "Infeasible", output.Message, 0);

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
        return new GenerateScheduleResult(true, output.Status.ToString(), output.Message, output.Assignments.Count);
    }

    private async Task<SchedulerInput> BuildInputAsync(Schedule schedule, int timeout, CancellationToken ct)
    {
        var rooms = await db.Rooms.Include(r => r.Building).ToListAsync(ct);
        var teachers = await db.Teachers.ToListAsync(ct);

        var groupsQuery = db.StudentGroups.AsQueryable();
        if (schedule.FacultyId.HasValue && !schedule.AllowCrossFacultyLessons)
            groupsQuery = groupsQuery.Where(g => g.FacultyId == schedule.FacultyId);
        var groups = await groupsQuery.ToListAsync(ct);

        var subjects = await db.Subjects
            .Where(s => s.AcademicYear == schedule.AcademicYear && s.Term == schedule.Term)
            .ToListAsync(ct);
        var subjectIds = subjects.Select(s => s.Id).ToHashSet();
        var teacherSubjects = await db.TeacherSubjects
            .Where(ts => subjectIds.Contains(ts.SubjectId))
            .ToListAsync(ct);
        var distances = await db.BuildingDistances.ToListAsync(ct);
        var blocks = await db.TeacherAvailabilities.ToListAsync(ct);

        var pairSlots = await db.PairTimeSlots.OrderBy(p => p.PairNumber).ToListAsync(ct);
        int pairsPerDay = pairSlots.Count > 0 ? pairSlots.Max(p => p.PairNumber) : 6;
        var breakMinutes = ComputeBreakMinutes(pairSlots);

        var groupIds = groups.Select(g => g.Id).ToList();
        var requirements = new List<SchedulerRequirement>();
        int idx = 0;

        foreach (var subject in subjects)
        {
            if (groupIds.Count == 0) continue;

            // Lectures: one merged requirement per teacher, groups split evenly across teachers
            if (subject.LectureHoursPerWeek > 0)
            {
                var lectureTeachers = teacherSubjects
                    .Where(ts => ts.SubjectId == subject.Id && ts.LessonType == LessonType.Lecture)
                    .Select(ts => ts.TeacherId).ToList();
                if (lectureTeachers.Count > 0)
                {
                    var chunks = SplitRoundRobin(groupIds, lectureTeachers.Count);
                    for (int i = 0; i < lectureTeachers.Count; i++)
                    {
                        if (chunks[i].Count == 0) continue;
                        foreach (var wt in GetWeekTypeOccurrences(subject.LectureWeekType))
                            requirements.Add(new SchedulerRequirement(idx++, chunks[i], subject.Id, LessonType.Lecture, lectureTeachers[i], wt, false, true, false, false));
                    }
                }
            }

            // Practicals: each group round-robined to a practical teacher
            if (subject.PracticalHoursPerWeek > 0)
            {
                var practTeachers = teacherSubjects
                    .Where(ts => ts.SubjectId == subject.Id && ts.LessonType == LessonType.Practical)
                    .Select(ts => ts.TeacherId).ToList();
                if (practTeachers.Count > 0)
                {
                    for (int gi = 0; gi < groupIds.Count; gi++)
                    {
                        var teacherId = practTeachers[gi % practTeachers.Count];
                        foreach (var wt in GetWeekTypeOccurrences(subject.PracticalWeekType))
                            requirements.Add(new SchedulerRequirement(idx++, [groupIds[gi]], subject.Id, LessonType.Practical, teacherId, wt, false, false, false, false));
                    }
                }
            }

            // Labs: each group round-robined to a lab teacher
            if (subject.LabHoursPerWeek > 0)
            {
                var labTeachers = teacherSubjects
                    .Where(ts => ts.SubjectId == subject.Id && ts.LessonType == LessonType.Lab)
                    .Select(ts => ts.TeacherId).ToList();
                if (labTeachers.Count > 0)
                {
                    for (int gi = 0; gi < groupIds.Count; gi++)
                    {
                        var teacherId = labTeachers[gi % labTeachers.Count];
                        foreach (var wt in GetWeekTypeOccurrences(subject.LabWeekType))
                            requirements.Add(new SchedulerRequirement(idx++, [groupIds[gi]], subject.Id, LessonType.Lab, teacherId, wt, false, false, false, true));
                    }
                }
            }
        }

        return new SchedulerInput(
            schedule.Id,
            rooms.Select(r => new SchedulerRoom(r.Id, r.BuildingId, r.RoomType, r.Capacity, r.HasProjector, r.HasComputers, r.HasLab, r.IsOnline,
                r.Floor, r.DistanceFromStairsMeters, r.Building.StairsDistancePerFloor)).ToList(),
            teachers.Select(t => new SchedulerTeacher(t.Id)).ToList(),
            groups.Select(g => new SchedulerGroup(g.Id, g.StudentCount)).ToList(),
            requirements,
            distances.Select(d => new SchedulerBuildingDistance(d.FromBuildingId, d.ToBuildingId, d.DistanceMeters)).ToList(),
            blocks.Select(b => new SchedulerBlock(b.TeacherId, b.DayOfWeek, b.PairNumber, b.WeekType)).ToList(),
            PairsPerDay: pairsPerDay,
            BreakMinutesBetweenPairs: breakMinutes,
            SolverTimeoutSeconds: timeout
        );
    }

    /// <summary>
    /// Distributes items across n buckets round-robin: item[i] → bucket[i % n].
    /// </summary>
    private static List<List<Guid>> SplitRoundRobin(List<Guid> items, int buckets)
    {
        var result = Enumerable.Range(0, buckets).Select(_ => new List<Guid>()).ToList();
        for (int i = 0; i < items.Count; i++)
            result[i % buckets].Add(items[i]);
        return result;
    }

    /// <summary>
    /// Computes break duration (minutes) between consecutive pairs from the seeded timetable.
    /// Result[i] = break between pair i+1 and pair i+2 (0-indexed gaps).
    /// </summary>
    private static List<int> ComputeBreakMinutes(List<PairTimeSlot> slots)
    {
        var ordered = slots.OrderBy(s => s.PairNumber).ToList();
        var breaks = new List<int>();
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var gap = (int)(ordered[i + 1].StartTime - ordered[i].EndTime).TotalMinutes;
            breaks.Add(Math.Max(0, gap));
        }
        return breaks;
    }

    private static IEnumerable<WeekType> GetWeekTypeOccurrences(WeekType weekType) => weekType switch
    {
        WeekType.Numerator => new[] { WeekType.Numerator },
        WeekType.Denominator => new[] { WeekType.Denominator },
        _ => new[] { WeekType.Both }
    };
}
