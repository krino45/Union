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
    Guid? NewRoomId) : IRequest<ScheduleEntryDto>;

public class MoveEntryCommandHandler : IRequestHandler<MoveEntryCommand, ScheduleEntryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IConflictDetector _conflict;

    public MoveEntryCommandHandler(IApplicationDbContext db, IConflictDetector conflict)
    {
        _db = db;
        _conflict = conflict;
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

        var conflicts = _conflict.DetectConflicts(
            entry.Id, entry.ScheduleId, request.NewRoomId, entry.TeacherId, groupIds,
            request.NewDayOfWeek, request.NewPairNumber, request.NewWeekType, entry.IsOnline,
            allEntries);

        if (conflicts.Count > 0)
            throw new ConflictException(conflicts);

        entry.DayOfWeek = request.NewDayOfWeek;
        entry.PairNumber = request.NewPairNumber;
        entry.WeekType = request.NewWeekType;
        entry.RoomId = request.NewRoomId;

        var schedule = await _db.Schedules.FindAsync(new object[] { entry.ScheduleId }, cancellationToken);
        if (schedule?.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        if (schedule?.Status == ScheduleStatus.Published)
            schedule.Status = ScheduleStatus.Draft;

        await _db.SaveChangesAsync(cancellationToken);

        return new ScheduleEntryDto(
            entry.Id, entry.ScheduleId, entry.SubjectId, entry.Subject.Name, entry.Subject.ShortName,
            entry.TeacherId, entry.Teacher.DisplayName,
            entry.RoomId, entry.Room?.Number, entry.Room?.Building?.ShortCode,
            entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.LessonType, entry.IsOnline,
            entry.StudentGroups.Select(sg => new GroupRefDto(sg.StudentGroupId, sg.StudentGroup.Name)).ToList());
    }
}
