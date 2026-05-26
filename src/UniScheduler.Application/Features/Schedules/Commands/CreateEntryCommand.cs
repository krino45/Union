using MediatR;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record CreateEntryCommand(
    Guid ScheduleId, Guid SubjectId, Guid TeacherId, Guid? RoomId,
    List<Guid> GroupIds,
    RussianDayOfWeek DayOfWeek, int PairNumber, WeekType WeekType,
    LessonType LessonType, bool IsOnline,
    string? SubgroupLabel = null) : IRequest<ScheduleEntryDto>;

public class CreateEntryCommandHandler : IRequestHandler<CreateEntryCommand, ScheduleEntryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflict;
    private readonly ICurrentUserService _user;

    public CreateEntryCommandHandler(IApplicationDbContext db, IConflictDetector conflict, ICurrentUserService user)
    {
        _db = db;
        _conflict = conflict;
        _user = user;
    }

    public async Task<ScheduleEntryDto> Handle(CreateEntryCommand r, CancellationToken cancellationToken)
    {
        var schedule = await _db.Schedules.FindAsync(new object[] { r.ScheduleId }, cancellationToken);
        if (schedule == null)
            throw new NotFoundException(nameof(Schedule), r.ScheduleId);
        if (schedule.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        ScheduleAccessGuard.EnsureCanEdit(schedule, _user);

        var allEntries = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == r.ScheduleId)
            .ToListAsync(cancellationToken);

        bool roomIsDistributed = r.RoomId.HasValue
            && await _db.Rooms.AnyAsync(rm => rm.Id == r.RoomId && rm.IsDistributed, cancellationToken);

        var subgroupLabel = string.IsNullOrWhiteSpace(r.SubgroupLabel) ? null : r.SubgroupLabel.Trim();

        var conflicts = _conflict.DetectConflicts(Guid.Empty, r.ScheduleId, r.RoomId, r.TeacherId, r.GroupIds,
            r.DayOfWeek, r.PairNumber, r.WeekType, r.IsOnline, allEntries,
            parallelGroupId: null, roomIsDistributed: roomIsDistributed, subgroupLabel: subgroupLabel);

        if (conflicts.Count > 0) throw new ConflictException(conflicts);

        var entry = new ScheduleEntry
        {
            ScheduleId = r.ScheduleId, SubjectId = r.SubjectId, TeacherId = r.TeacherId,
            RoomId = r.RoomId, DayOfWeek = r.DayOfWeek, PairNumber = r.PairNumber,
            WeekType = r.WeekType, LessonType = r.LessonType, IsOnline = r.IsOnline,
            SubgroupLabel = subgroupLabel
        };
        _db.ScheduleEntries.Add(entry);
        foreach (var gid in r.GroupIds)
            _db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup { ScheduleEntry = entry, StudentGroupId = gid });

        if (schedule.Status == ScheduleStatus.Published)
        {
            schedule.Status = ScheduleStatus.Draft;
            ScheduleAccessGuard.TransferOwnershipOnDemote(schedule, _user);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var subject = await _db.Subjects.FindAsync(new object[] { r.SubjectId }, cancellationToken);
        var teacher = await _db.Teachers.FindAsync(new object[] { r.TeacherId }, cancellationToken);
        Room? room = r.RoomId.HasValue ? await _db.Rooms.Include(x => x.Building).FirstOrDefaultAsync(x => x.Id == r.RoomId, cancellationToken) : null;
        var groups = await _db.StudentGroups.Where(g => r.GroupIds.Contains(g.Id)).ToListAsync(cancellationToken);

        return new ScheduleEntryDto(entry.Id, entry.ScheduleId, entry.SubjectId, subject!.Name, subject.ShortName,
            entry.TeacherId, teacher!.DisplayName, entry.RoomId, room?.Number, room?.Building?.ShortCode,
            entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.LessonType, entry.IsOnline,
            groups.Select(g => new GroupRefDto(g.Id, g.Name)).ToList(), entry.ParallelGroupId, entry.SubgroupLabel);
    }
}
