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

        // Groups: if schedule is faculty-specific, only load that faculty's groups
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

        var requirements = new List<SchedulerRequirement>();
        int idx = 0;

        foreach (var subject in subjects)
        {
            foreach (var group in groups)
            {
                if (subject.LectureHoursPerWeek > 0)
                {
                    var teacher = teacherSubjects.FirstOrDefault(ts => ts.SubjectId == subject.Id && ts.LessonType == LessonType.Lecture);
                    if (teacher != null)
                    {
                        foreach (var wt in GetWeekTypeOccurrences(subject.LectureWeekType))
                            requirements.Add(new SchedulerRequirement(idx++, new[] { group.Id }, subject.Id, LessonType.Lecture, teacher.TeacherId, wt, false, true, false, false));
                    }
                }

                if (subject.PracticalHoursPerWeek > 0)
                {
                    var teacher = teacherSubjects.FirstOrDefault(ts => ts.SubjectId == subject.Id && ts.LessonType == LessonType.Practical);
                    if (teacher != null)
                    {
                        foreach (var wt in GetWeekTypeOccurrences(subject.PracticalWeekType))
                            requirements.Add(new SchedulerRequirement(idx++, new[] { group.Id }, subject.Id, LessonType.Practical, teacher.TeacherId, wt, false, false, false, false));
                    }
                }

                if (subject.LabHoursPerWeek > 0)
                {
                    var teacher = teacherSubjects.FirstOrDefault(ts => ts.SubjectId == subject.Id && ts.LessonType == LessonType.Lab);
                    if (teacher != null)
                    {
                        foreach (var wt in GetWeekTypeOccurrences(subject.LabWeekType))
                            requirements.Add(new SchedulerRequirement(idx++, new[] { group.Id }, subject.Id, LessonType.Lab, teacher.TeacherId, wt, false, false, false, true));
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
            SolverTimeoutSeconds: timeout
        );
    }

    private static IEnumerable<WeekType> GetWeekTypeOccurrences(WeekType weekType) => weekType switch
    {
        WeekType.Numerator => new[] { WeekType.Numerator },
        WeekType.Denominator => new[] { WeekType.Denominator },
        _ => new[] { WeekType.Both }
    };
}
