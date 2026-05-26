using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record MoveEntryCommand(
    Guid EntryId,
    RussianDayOfWeek NewDayOfWeek,
    int NewPairNumber,
    WeekType NewWeekType,
    Guid? NewRoomId,
    bool? NewIsOnline = null) : IRequest<ScheduleEntryDto>;

public class MoveEntryCommandHandler : IRequestHandler<MoveEntryCommand, ScheduleEntryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflict;
    private readonly ICurrentUserService _user;

    public MoveEntryCommandHandler(IApplicationDbContext db, IConflictDetector conflict, ICurrentUserService user)
    {
        _db = db;
        _conflict = conflict;
        _user = user;
    }

    public async Task<ScheduleEntryDto> Handle(MoveEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _db.ScheduleEntries
            .Include(e => e.Subject)
            .Include(e => e.Teacher)
            .Include(e => e.Room).ThenInclude(r => r!.Building)
            .Include(e => e.StudentGroups).ThenInclude(sg => sg.StudentGroup)
            .FirstOrDefaultAsync(e => e.Id == request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduleEntry), request.EntryId);

        var allEntries = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == entry.ScheduleId && e.Id != entry.Id)
            .ToListAsync(cancellationToken);

        var groupIds = entry.StudentGroups.Select(sg => sg.StudentGroupId);

        var isOnline = request.NewIsOnline ?? entry.IsOnline;
        var newRoomId = isOnline ? null : request.NewRoomId;

        bool roomIsDistributed = newRoomId.HasValue
            && await _db.Rooms.AnyAsync(rm => rm.Id == newRoomId && rm.IsDistributed, cancellationToken);

        var conflicts = _conflict.DetectConflicts(
            entry.Id, entry.ScheduleId, newRoomId, entry.TeacherId, groupIds,
            request.NewDayOfWeek, request.NewPairNumber, request.NewWeekType, isOnline,
            allEntries, entry.ParallelGroupId, roomIsDistributed);

        if (conflicts.Count > 0)
            throw new ConflictException(conflicts);

        entry.DayOfWeek = request.NewDayOfWeek;
        entry.PairNumber = request.NewPairNumber;
        entry.WeekType = request.NewWeekType;
        entry.IsOnline = isOnline;
        entry.RoomId = newRoomId;

        var schedule = await _db.Schedules.FindAsync(new object[] { entry.ScheduleId }, cancellationToken);
        if (schedule == null)
            throw new NotFoundException(nameof(Schedule), entry.ScheduleId);
        if (schedule.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        ScheduleAccessGuard.EnsureCanEdit(schedule, _user);
        if (schedule.Status == ScheduleStatus.Published)
        {
            schedule.Status = ScheduleStatus.Draft;
            ScheduleAccessGuard.TransferOwnershipOnDemote(schedule, _user);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ScheduleEntryDto(
            entry.Id, entry.ScheduleId, entry.SubjectId, entry.Subject.Name, entry.Subject.ShortName,
            entry.TeacherId, entry.Teacher.DisplayName,
            entry.RoomId, entry.Room?.Number, entry.Room?.Building?.ShortCode,
            entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.LessonType, entry.IsOnline,
            entry.StudentGroups.Select(sg => new GroupRefDto(sg.StudentGroupId, sg.StudentGroup.Name)).ToList());
    }
}
