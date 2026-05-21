using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Queries;

public record ValidationIssue(string Severity, string Code, string Message);

public record ValidateEditQuery(
    Guid ScheduleId,
    Guid? EntryId, // null = creating a new entry
    Guid SubjectId,
    Guid TeacherId,
    Guid? RoomId,
    List<Guid> GroupIds,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    WeekType WeekType,
    LessonType LessonType,
    bool IsOnline) : IRequest<List<ValidationIssue>>;

public class ValidateEditQueryHandler : IRequestHandler<ValidateEditQuery, List<ValidationIssue>>
{
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflict;

    public ValidateEditQueryHandler(IApplicationDbContext db, IConflictDetector conflict)
    { _db = db; _conflict = conflict; }

    public async Task<List<ValidationIssue>> Handle(ValidateEditQuery r, CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();

        var allOtherEntries = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == r.ScheduleId && (r.EntryId == null || e.Id != r.EntryId))
            .ToListAsync(cancellationToken);

        var conflicts = _conflict.DetectConflicts(
            r.EntryId ?? Guid.Empty, r.ScheduleId, r.RoomId, r.TeacherId, r.GroupIds,
            r.DayOfWeek, r.PairNumber, r.WeekType, r.IsOnline, allOtherEntries);

        foreach (var c in conflicts)
            issues.Add(new ValidationIssue("error", c.Type.ToString(), c.Description));

        if (r.RoomId.HasValue && !r.IsOnline)
        {
            var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(rm => rm.Id == r.RoomId, cancellationToken);
            if (room != null)
            {
                var totalStudents = await _db.StudentGroups
                    .Where(g => r.GroupIds.Contains(g.Id))
                    .SumAsync(g => (int?)g.StudentCount, cancellationToken) ?? 0;
                if (room.Capacity > 0 && totalStudents > room.Capacity)
                {
                    issues.Add(new ValidationIssue("error", "RoomCapacityExceeded",
                        $"Вместимость аудитории {room.Number} ({room.Capacity}) меньше суммарного размера групп ({totalStudents})."));
                }

                if (room.AllowedLessonTypes.Count > 0 && !room.AllowedLessonTypes.Contains(r.LessonType))
                {
                    issues.Add(new ValidationIssue("warning", "RoomLessonTypeMismatch",
                        $"Аудитория {room.Number} не предназначена для типа занятия «{r.LessonType}»."));
                }

                if (!room.IsEnabled)
                {
                    issues.Add(new ValidationIssue("warning", "RoomDisabled",
                        $"Аудитория {room.Number} отключена."));
                }
            }
        }

        var unavail = await _db.TeacherAvailabilities
            .Where(a => a.TeacherId == r.TeacherId
                && a.DayOfWeek == r.DayOfWeek
                && a.PairNumber == r.PairNumber
                && (a.WeekType == WeekType.Both || r.WeekType == WeekType.Both || a.WeekType == r.WeekType))
            .ToListAsync(cancellationToken);
        if (unavail.Count > 0)
        {
            var reasons = string.Join(", ", unavail.Where(u => !string.IsNullOrWhiteSpace(u.Reason)).Select(u => u.Reason));
            var msg = string.IsNullOrEmpty(reasons)
                ? "Преподаватель отметил этот слот как недоступный."
                : $"Преподаватель отметил этот слот как недоступный: {reasons}.";
            issues.Add(new ValidationIssue("warning", "TeacherUnavailable", msg));
        }

        if (r.IsOnline && r.RoomId.HasValue)
        {
            issues.Add(new ValidationIssue("info", "OnlineWithRoom",
                "Занятие помечено как онлайн, но указана аудитория."));
        }

        return issues;
    }
}
