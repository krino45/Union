using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record UpdateEntryCommand(
    Guid EntryId,
    Guid SubjectId, Guid TeacherId, Guid? RoomId,
    List<Guid> GroupIds,
    RussianDayOfWeek DayOfWeek, int PairNumber, WeekType WeekType,
    LessonType LessonType, bool IsOnline) : IRequest<ScheduleEntryDto>;

public class UpdateEntryCommandHandler : IRequestHandler<UpdateEntryCommand, ScheduleEntryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflict;
    private readonly ICurrentUserService _user;

    public UpdateEntryCommandHandler(IApplicationDbContext db, IConflictDetector conflict, ICurrentUserService user)
    {
        _db = db;
        _conflict = conflict;
        _user = user;
    }

    public async Task<ScheduleEntryDto> Handle(UpdateEntryCommand r, CancellationToken cancellationToken)
    {
        var entry = await _db.ScheduleEntries
            .Include(e => e.Subject)
            .Include(e => e.Teacher)
            .Include(e => e.Room).ThenInclude(rm => rm!.Building)
            .Include(e => e.StudentGroups).ThenInclude(sg => sg.StudentGroup)
            .FirstOrDefaultAsync(e => e.Id == r.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduleEntry), r.EntryId);

        var schedule = await _db.Schedules.FindAsync(new object[] { entry.ScheduleId }, cancellationToken);
        if (schedule == null)
            throw new NotFoundException(nameof(Schedule), entry.ScheduleId);
        if (schedule.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        ScheduleAccessGuard.EnsureCanEdit(schedule, _user);

        var allOtherEntries = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == entry.ScheduleId && e.Id != r.EntryId)
            .ToListAsync(cancellationToken);

        var conflicts = _conflict.DetectConflicts(
            r.EntryId, entry.ScheduleId, r.RoomId, r.TeacherId, r.GroupIds,
            r.DayOfWeek, r.PairNumber, r.WeekType, r.IsOnline, allOtherEntries);

        if (conflicts.Count > 0) throw new ConflictException(conflicts);

        entry.SubjectId  = r.SubjectId;
        entry.TeacherId  = r.TeacherId;
        entry.RoomId     = r.RoomId;
        entry.DayOfWeek  = r.DayOfWeek;
        entry.PairNumber = r.PairNumber;
        entry.WeekType   = r.WeekType;
        entry.LessonType = r.LessonType;
        entry.IsOnline   = r.IsOnline;

        var existingGroupIds = entry.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
        var newGroupIds      = r.GroupIds.ToHashSet();

        foreach (var remove in entry.StudentGroups.Where(sg => !newGroupIds.Contains(sg.StudentGroupId)).ToList())
            _db.ScheduleEntryStudentGroups.Remove(remove);

        foreach (var add in newGroupIds.Where(id => !existingGroupIds.Contains(id)))
            _db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup { ScheduleEntryId = r.EntryId, StudentGroupId = add });

        if (schedule.Status == ScheduleStatus.Published)
        {
            schedule.Status = ScheduleStatus.Draft;
            ScheduleAccessGuard.TransferOwnershipOnDemote(schedule, _user);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Reload navigation props that may have changed
        var subject = await _db.Subjects.FindAsync(new object[] { r.SubjectId }, cancellationToken);
        var teacher = await _db.Teachers.FindAsync(new object[] { r.TeacherId }, cancellationToken);
        Room? room = r.RoomId.HasValue
            ? await _db.Rooms.Include(x => x.Building).FirstOrDefaultAsync(x => x.Id == r.RoomId, cancellationToken)
            : null;
        var groups = await _db.StudentGroups.Where(g => r.GroupIds.Contains(g.Id)).ToListAsync(cancellationToken);

        return new ScheduleEntryDto(
            entry.Id, entry.ScheduleId, entry.SubjectId, subject!.Name, subject.ShortName,
            entry.TeacherId, teacher!.DisplayName,
            entry.RoomId, room?.Number, room?.Building?.ShortCode,
            entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.LessonType, entry.IsOnline,
            groups.Select(g => new GroupRefDto(g.Id, g.Name)).ToList());
    }
}
