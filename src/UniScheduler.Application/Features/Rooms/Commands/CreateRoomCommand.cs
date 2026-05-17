using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Rooms.Commands;

public record CreateRoomCommand(
    Guid BuildingId, string Number, RoomType RoomType, int Capacity,
    bool HasProjector, bool HasComputers, bool HasLab, bool IsOnline,
    int Floor = 1, int DistanceFromStairsMeters = 0,
    List<LessonType>? AllowedLessonTypes = null) : IRequest<RoomDto>;

public class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext _db;
    public CreateRoomCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RoomDto> Handle(CreateRoomCommand r, CancellationToken cancellationToken)
    {
        var room = new Room
        {
            BuildingId = r.BuildingId, Number = r.Number, RoomType = r.RoomType,
            Capacity = r.Capacity, HasProjector = r.HasProjector,
            HasComputers = r.HasComputers, HasLab = r.HasLab, IsOnline = r.IsOnline,
            Floor = r.Floor, DistanceFromStairsMeters = r.DistanceFromStairsMeters,
            AllowedLessonTypes = r.AllowedLessonTypes ?? new List<LessonType>()
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(cancellationToken);

        var building = await _db.Buildings.FirstAsync(b => b.Id == r.BuildingId, cancellationToken);
        return new RoomDto(room.Id, room.BuildingId, building.ShortCode, room.Number, room.RoomType,
            room.Capacity, room.HasProjector, room.HasComputers, room.HasLab, room.IsOnline,
            room.Floor, room.DistanceFromStairsMeters, room.AllowedLessonTypes);
    }
}
