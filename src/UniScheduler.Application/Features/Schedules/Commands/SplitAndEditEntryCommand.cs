using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

// Splits a WeekType=Both lesson into two single-week entries (Odd + Even) and applies the edit
// to the chosen half. The sibling half keeps the original values.
//
// If the entry's WeekType is not Both, this falls through to a normal UpdateEntry-like behavior
// (same effect as UpdateEntryCommand but with the same call shape, so the frontend can use one
// codepath for both "edit both" and "edit half").
public record SplitAndEditEntryCommand(
    Guid EntryId,
    WeekType TargetWeek, // Odd or Even — the half to edit. If equal to the entry's current WeekType
                         // or the entry is not Both, just edits in place without splitting.
    Guid SubjectId,
    Guid TeacherId,
    Guid? RoomId,
    List<Guid> GroupIds,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    LessonType LessonType,
    bool IsOnline) : IRequest<ScheduleEntryDto>;

public class SplitAndEditEntryCommandHandler : IRequestHandler<SplitAndEditEntryCommand, ScheduleEntryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflict;
    private readonly ICurrentUserService _user;

    public SplitAndEditEntryCommandHandler(IApplicationDbContext db, IConflictDetector conflict, ICurrentUserService user)
    { _db = db; _conflict = conflict; _user = user; }

    public async Task<ScheduleEntryDto> Handle(SplitAndEditEntryCommand r, CancellationToken cancellationToken)
    {
        if (r.TargetWeek == WeekType.Both)
            throw new InvalidOperationException("TargetWeek must be Odd or Even when splitting.");

        var entry = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .FirstOrDefaultAsync(e => e.Id == r.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduleEntry), r.EntryId);

        var schedule = await _db.Schedules.FindAsync(new object[] { entry.ScheduleId }, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), entry.ScheduleId);

        if (schedule.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        ScheduleAccessGuard.EnsureCanEdit(schedule, _user);

        // If the entry covers both weeks, create a sibling that keeps the OPPOSITE half
        // with the entry's ORIGINAL values, before mutating the edited half.
        if (entry.WeekType == WeekType.Both)
        {
            var siblingWeek = r.TargetWeek == WeekType.Odd ? WeekType.Even : WeekType.Odd;

            var sibling = new ScheduleEntry
            {
                ScheduleId = entry.ScheduleId,
                SubjectId = entry.SubjectId,
                TeacherId = entry.TeacherId,
                RoomId = entry.RoomId,
                DayOfWeek = entry.DayOfWeek,
                PairNumber = entry.PairNumber,
                WeekType = siblingWeek,
                LessonType = entry.LessonType,
                IsOnline = entry.IsOnline,
                ParallelGroupId = entry.ParallelGroupId,
                SubgroupLabel = entry.SubgroupLabel
            };
            _db.ScheduleEntries.Add(sibling);
            foreach (var g in entry.StudentGroups)
                _db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup
                {
                    ScheduleEntry = sibling,
                    StudentGroupId = g.StudentGroupId
                });

            entry.WeekType = r.TargetWeek;
        }

        // Apply edits to the target entry (the original one that's been narrowed to r.TargetWeek)
        // Conflict check
        var allOtherEntries = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == entry.ScheduleId && e.Id != entry.Id)
            .ToListAsync(cancellationToken);

        bool roomIsDistributed = r.RoomId.HasValue
            && await _db.Rooms.AnyAsync(rm => rm.Id == r.RoomId && rm.IsDistributed, cancellationToken);

        var conflicts = _conflict.DetectConflicts(
            entry.Id, entry.ScheduleId, r.RoomId, r.TeacherId, r.GroupIds,
            r.DayOfWeek, r.PairNumber, entry.WeekType, r.IsOnline, allOtherEntries,
            entry.ParallelGroupId, roomIsDistributed, entry.SubgroupLabel);
        if (conflicts.Count > 0) throw new ConflictException(conflicts);

        entry.SubjectId = r.SubjectId;
        entry.TeacherId = r.TeacherId;
        entry.RoomId = r.RoomId;
        entry.DayOfWeek = r.DayOfWeek;
        entry.PairNumber = r.PairNumber;
        entry.LessonType = r.LessonType;
        entry.IsOnline = r.IsOnline;

        var existingGroupIds = entry.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
        var newGroupIds = r.GroupIds.ToHashSet();
        foreach (var remove in entry.StudentGroups.Where(sg => !newGroupIds.Contains(sg.StudentGroupId)).ToList())
            _db.ScheduleEntryStudentGroups.Remove(remove);
        foreach (var add in newGroupIds.Where(id => !existingGroupIds.Contains(id)))
            _db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup { ScheduleEntryId = entry.Id, StudentGroupId = add });

        if (schedule.Status == ScheduleStatus.Published)
        {
            schedule.Status = ScheduleStatus.Draft;
            ScheduleAccessGuard.TransferOwnershipOnDemote(schedule, _user);
        }

        await _db.SaveChangesAsync(cancellationToken);

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
            groups.Select(g => new GroupRefDto(g.Id, g.Name)).ToList(), entry.ParallelGroupId, entry.SubgroupLabel);
    }
}
