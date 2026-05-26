using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record ParallelSessionInput(Guid TeacherId, Guid? RoomId, string? Label);

// Creates one logical class as several parallel sessions (language streams / lab subgroups) that
// share a ParallelGroupId: same slot and groups, distinct teachers, never conflicting with each other.
public record CreateParallelEntriesCommand(
    Guid ScheduleId, Guid SubjectId, LessonType LessonType, WeekType WeekType,
    RussianDayOfWeek DayOfWeek, int PairNumber, List<Guid> GroupIds, bool IsOnline,
    List<ParallelSessionInput> Sessions) : IRequest<List<ScheduleEntryDto>>;

public class CreateParallelEntriesCommandHandler : IRequestHandler<CreateParallelEntriesCommand, List<ScheduleEntryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflict;
    private readonly ICurrentUserService _user;

    public CreateParallelEntriesCommandHandler(IApplicationDbContext db, IConflictDetector conflict, ICurrentUserService user)
    {
        _db = db;
        _conflict = conflict;
        _user = user;
    }

    public async Task<List<ScheduleEntryDto>> Handle(CreateParallelEntriesCommand r, CancellationToken ct)
    {
        if (r.Sessions.Count < 2)
            throw new InvalidOperationException("A parallel class needs at least two sessions.");

        var teacherIds = r.Sessions.Select(s => s.TeacherId).ToList();
        if (teacherIds.Distinct().Count() != teacherIds.Count)
            throw new InvalidOperationException("Each parallel session must have a distinct teacher.");

        var schedule = await _db.Schedules.FindAsync(new object[] { r.ScheduleId }, ct)
            ?? throw new NotFoundException(nameof(Schedule), r.ScheduleId);
        if (schedule.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        ScheduleAccessGuard.EnsureCanEdit(schedule, _user);

        var distributedRoomIds = (await _db.Rooms.Where(x => x.IsDistributed).Select(x => x.Id).ToListAsync(ct)).ToHashSet();

        var realRooms = r.Sessions
            .Where(s => !r.IsOnline && s.RoomId.HasValue && !distributedRoomIds.Contains(s.RoomId.Value))
            .Select(s => s.RoomId!.Value).ToList();
        if (realRooms.Distinct().Count() != realRooms.Count)
            throw new InvalidOperationException("Two parallel sessions cannot share the same room.");

        var existing = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == r.ScheduleId)
            .ToListAsync(ct);

        var parallelGroupId = Guid.NewGuid();

        foreach (var s in r.Sessions)
        {
            bool roomIsDistributed = s.RoomId.HasValue && distributedRoomIds.Contains(s.RoomId.Value);
            var conflicts = _conflict.DetectConflicts(
                Guid.Empty, r.ScheduleId, r.IsOnline ? null : s.RoomId, s.TeacherId, r.GroupIds,
                r.DayOfWeek, r.PairNumber, r.WeekType, r.IsOnline, existing, parallelGroupId, roomIsDistributed);
            if (conflicts.Count > 0) throw new ConflictException(conflicts);
        }

        var created = new List<ScheduleEntry>();
        foreach (var s in r.Sessions)
        {
            var entry = new ScheduleEntry
            {
                ScheduleId = r.ScheduleId, SubjectId = r.SubjectId, TeacherId = s.TeacherId,
                RoomId = r.IsOnline ? null : s.RoomId, DayOfWeek = r.DayOfWeek, PairNumber = r.PairNumber,
                WeekType = r.WeekType, LessonType = r.LessonType, IsOnline = r.IsOnline,
                ParallelGroupId = parallelGroupId,
                SubgroupLabel = string.IsNullOrWhiteSpace(s.Label) ? null : s.Label.Trim()
            };
            _db.ScheduleEntries.Add(entry);
            foreach (var gid in r.GroupIds)
                _db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup { ScheduleEntry = entry, StudentGroupId = gid });
            created.Add(entry);
        }

        if (schedule.Status == ScheduleStatus.Published)
        {
            schedule.Status = ScheduleStatus.Draft;
            ScheduleAccessGuard.TransferOwnershipOnDemote(schedule, _user);
        }
        await _db.SaveChangesAsync(ct);

        var subject = await _db.Subjects.FindAsync(new object[] { r.SubjectId }, ct);
        var teachersById = await _db.Teachers.Where(t => teacherIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var roomIds = r.Sessions.Where(s => s.RoomId.HasValue).Select(s => s.RoomId!.Value).ToList();
        var roomsById = await _db.Rooms.Include(x => x.Building).Where(x => roomIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var groupRefs = (await _db.StudentGroups.Where(g => r.GroupIds.Contains(g.Id)).ToListAsync(ct))
            .Select(g => new GroupRefDto(g.Id, g.Name)).ToList();

        return created.Select(entry =>
        {
            teachersById.TryGetValue(entry.TeacherId, out var teacher);
            Room? room = entry.RoomId.HasValue && roomsById.TryGetValue(entry.RoomId.Value, out var rm) ? rm : null;
            return new ScheduleEntryDto(entry.Id, entry.ScheduleId, entry.SubjectId, subject!.Name, subject.ShortName,
                entry.TeacherId, teacher?.DisplayName ?? "", entry.RoomId, room?.Number, room?.Building?.ShortCode,
                entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.LessonType, entry.IsOnline,
                groupRefs, entry.ParallelGroupId, entry.SubgroupLabel);
        }).ToList();
    }
}
