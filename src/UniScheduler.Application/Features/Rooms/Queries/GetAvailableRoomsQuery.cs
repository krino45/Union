using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Rooms.Queries;

// Rooms that have no class occupying them at the given slot in the given schedule.
// Used by the teacher reschedule form so suggested moves are feasible.
public record GetAvailableRoomsQuery(
    Guid ScheduleId,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    WeekType WeekType,
    Guid? ExcludeEntryId = null) : IRequest<List<RoomDto>>;

public class GetAvailableRoomsQueryHandler : IRequestHandler<GetAvailableRoomsQuery, List<RoomDto>>
{
    private readonly IApplicationDbContext _db;
    public GetAvailableRoomsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<RoomDto>> Handle(GetAvailableRoomsQuery r, CancellationToken ct)
    {
        var universityId = await _db.Schedules
            .Where(s => s.Id == r.ScheduleId)
            .Select(s => s.UniversityId)
            .FirstOrDefaultAsync(ct);

        var occupied = await _db.ScheduleEntries
            .Where(e => e.ScheduleId == r.ScheduleId
                && e.RoomId != null
                && !e.IsOnline
                && e.DayOfWeek == r.DayOfWeek
                && e.PairNumber == r.PairNumber
                && (e.WeekType == WeekType.Both || r.WeekType == WeekType.Both || e.WeekType == r.WeekType)
                && (r.ExcludeEntryId == null || e.Id != r.ExcludeEntryId))
            .Select(e => e.RoomId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var rooms = await _db.Rooms
            .Include(rm => rm.Building)
            .Include(rm => rm.Department)
            .Where(rm => rm.IsEnabled && rm.Building.UniversityId == universityId && !occupied.Contains(rm.Id))
            .OrderBy(rm => rm.Building.ShortCode).ThenBy(rm => rm.Floor).ThenBy(rm => rm.Number)
            .ToListAsync(ct);

        return rooms.Select(rm => new RoomDto(rm.Id, rm.BuildingId, rm.Building.ShortCode, rm.Number, rm.RoomType,
                rm.Capacity, rm.HasProjector, rm.HasComputers, rm.HasLab, rm.IsOnline,
                rm.Floor, rm.AllowedLessonTypes, rm.IsEnabled, rm.DepartmentId, rm.Department != null ? rm.Department.Name : null,
                0))
            .ToList();
    }
}
