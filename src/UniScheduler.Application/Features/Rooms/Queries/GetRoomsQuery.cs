using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Rooms.Queries;

public record GetRoomsQuery(Guid? BuildingId = null, RoomType? RoomType = null, int? MinCapacity = null) : IRequest<List<RoomDto>>;

public class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, List<RoomDto>>
{
    private readonly IApplicationDbContext db;
    public GetRoomsQueryHandler(IApplicationDbContext db) => this.db = db;

    // 6 working days * 7 pair slots = 42 weekly slots; Both-week pair counts as 2, Odd/Even as 1.
    private const int TotalWeeklySlots = 42 * 2;

    public async Task<List<RoomDto>> Handle(GetRoomsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Rooms.Include(r => r.Building).Include(r => r.Department).AsQueryable();

        if (request.BuildingId.HasValue) query = query.Where(r => r.BuildingId == request.BuildingId);
        if (request.RoomType.HasValue) query = query.Where(r => r.RoomType == request.RoomType);
        if (request.MinCapacity.HasValue) query = query.Where(r => r.Capacity >= request.MinCapacity);

        var rooms = await query.OrderBy(r => r.Building.ShortCode).ThenBy(r => r.Floor).ThenBy(r => r.Number)
            .ToListAsync(cancellationToken);

        // Per-room weekly pair count across all Published schedules
        var loadRaw = await db.ScheduleEntries
            .Where(e => e.RoomId != null && e.Schedule.Status == ScheduleStatus.Published)
            .GroupBy(e => new { e.RoomId, e.WeekType })
            .Select(g => new { g.Key.RoomId, g.Key.WeekType, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var utilizationByRoom = loadRaw
            .GroupBy(x => x.RoomId!.Value)
            .ToDictionary(
                g => g.Key,
                g => Math.Min(100, (int)Math.Round(
                    g.Sum(x => x.WeekType == WeekType.Both ? x.Count * 2 : x.Count) * 100.0 / TotalWeeklySlots)));

        return rooms.Select(r => new RoomDto(r.Id, r.BuildingId, r.Building.ShortCode, r.Number, r.RoomType,
                r.Capacity, r.HasProjector, r.HasComputers, r.IsOnline,
                r.Floor, r.AllowedLessonTypes, r.IsEnabled, r.DepartmentId, r.Department?.Name,
                utilizationByRoom.TryGetValue(r.Id, out var u) ? u : 0))
            .ToList();
    }
}
