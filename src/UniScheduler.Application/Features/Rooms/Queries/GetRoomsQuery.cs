using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Rooms.Queries;

public record GetRoomsQuery(Guid? BuildingId = null, RoomType? RoomType = null, int? MinCapacity = null) : IRequest<List<RoomDto>>;

public class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, List<RoomDto>>
{
    private readonly IApplicationDbContext _db;
    public GetRoomsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<RoomDto>> Handle(GetRoomsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Rooms.Include(r => r.Building).AsQueryable();

        if (request.BuildingId.HasValue) query = query.Where(r => r.BuildingId == request.BuildingId);
        if (request.RoomType.HasValue) query = query.Where(r => r.RoomType == request.RoomType);
        if (request.MinCapacity.HasValue) query = query.Where(r => r.Capacity >= request.MinCapacity);

        return await query
            .OrderBy(r => r.Building.ShortCode).ThenBy(r => r.Floor).ThenBy(r => r.Number)
            .Select(r => new RoomDto(r.Id, r.BuildingId, r.Building.ShortCode, r.Number, r.RoomType,
                r.Capacity, r.HasProjector, r.HasComputers, r.HasLab, r.IsOnline,
                r.Floor, r.DistanceFromStairsMeters, r.AllowedLessonTypes))
            .ToListAsync(cancellationToken);
    }
}
