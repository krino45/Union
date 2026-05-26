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
    bool IsOnline,
    string? SubgroupLabel = null) : IRequest<List<ValidationIssue>>;

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

        // When editing, inherit the entry's parallel-group link so its own siblings aren't flagged.
        Guid? parallelGroupId = r.EntryId.HasValue
            ? await _db.ScheduleEntries.Where(e => e.Id == r.EntryId).Select(e => e.ParallelGroupId).FirstOrDefaultAsync(cancellationToken)
            : null;

        bool roomIsDistributed = r.RoomId.HasValue
            && await _db.Rooms.AnyAsync(rm => rm.Id == r.RoomId && rm.IsDistributed, cancellationToken);

        var subgroupLabel = string.IsNullOrWhiteSpace(r.SubgroupLabel) ? null : r.SubgroupLabel.Trim();

        var conflicts = _conflict.DetectConflicts(
            r.EntryId ?? Guid.Empty, r.ScheduleId, r.RoomId, r.TeacherId, r.GroupIds,
            r.DayOfWeek, r.PairNumber, r.WeekType, r.IsOnline, allOtherEntries,
            parallelGroupId, roomIsDistributed, subgroupLabel);

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

        // Walking-distance warning: look at adjacent pairs on the same day for the same teacher
        // or any of the groups, and check inter-building walking time against the 10-min break.
        if (r.RoomId.HasValue && !r.IsOnline)
        {
            var edited = await _db.Rooms.AsNoTracking()
                .FirstOrDefaultAsync(rm => rm.Id == r.RoomId, cancellationToken);
            if (edited != null)
            {
                var adjacentPairs = new[] { r.PairNumber - 1, r.PairNumber + 1 };
                var groupIdSet = r.GroupIds.ToHashSet();

                var neighbours = allOtherEntries
                    .Where(e =>
                        e.DayOfWeek == r.DayOfWeek
                        && adjacentPairs.Contains(e.PairNumber)
                        && !e.IsOnline
                        && e.RoomId.HasValue
                        && WeeksOverlap(e.WeekType, r.WeekType)
                        && (e.TeacherId == r.TeacherId
                            || e.StudentGroups.Any(g => groupIdSet.Contains(g.StudentGroupId))))
                    .ToList();

                if (neighbours.Count > 0)
                {
                    var neighbourRoomIds = neighbours.Select(n => n.RoomId!.Value).ToList();
                    var rooms = await _db.Rooms.AsNoTracking()
                        .Where(rm => neighbourRoomIds.Contains(rm.Id))
                        .ToDictionaryAsync(rm => rm.Id, cancellationToken);

                    var otherBuildingIds = rooms.Values
                        .Where(rm => rm.BuildingId != edited.BuildingId)
                        .Select(rm => rm.BuildingId)
                        .Distinct()
                        .ToList();

                    if (otherBuildingIds.Count > 0)
                    {
                        var distances = await _db.BuildingDistances.AsNoTracking()
                            .Where(d =>
                                (d.FromBuildingId == edited.BuildingId && otherBuildingIds.Contains(d.ToBuildingId))
                                || (d.ToBuildingId == edited.BuildingId && otherBuildingIds.Contains(d.FromBuildingId)))
                            .ToListAsync(cancellationToken);

                        foreach (var n in neighbours)
                        {
                            if (!rooms.TryGetValue(n.RoomId!.Value, out var nRoom)) continue;
                            if (nRoom.BuildingId == edited.BuildingId) continue;

                            var dist = distances.FirstOrDefault(d =>
                                (d.FromBuildingId == edited.BuildingId && d.ToBuildingId == nRoom.BuildingId)
                                || (d.ToBuildingId == edited.BuildingId && d.FromBuildingId == nRoom.BuildingId));

                            if (dist == null) continue;
                            if (!dist.ExceedsPairBreak) continue;

                            var who = n.TeacherId == r.TeacherId ? "преподаватель" : "группа";
                            var when = n.PairNumber < r.PairNumber ? "предыдущая пара" : "следующая пара";
                            issues.Add(new ValidationIssue("warning", "WalkingDistance",
                                $"Пешком между корпусами ≈{dist.WalkingMinutes:F0} мин (>10 мин перемены): {when}, {who} занят(а)."));
                        }
                    }
                }
            }
        }

        return issues;
    }

    private static bool WeeksOverlap(WeekType a, WeekType b) =>
        a == WeekType.Both || b == WeekType.Both || a == b;
}
