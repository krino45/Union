using UniScheduler.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Rooms.Commands;

public record UpdateRoomCommand(
    Guid Id, Guid BuildingId, string Number, RoomType RoomType, int Capacity,
    bool HasProjector, bool HasComputers, bool HasLab, bool IsOnline,
    int Floor = 1,
    List<LessonType>? AllowedLessonTypes = null) : IRequest<RoomDto>;

public class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateRoomCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RoomDto> Handle(UpdateRoomCommand r, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms.Include(x => x.Building).FirstOrDefaultAsync(x => x.Id == r.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), r.Id);

        room.BuildingId = r.BuildingId; room.Number = r.Number; room.RoomType = r.RoomType;
        room.Capacity = r.Capacity; room.HasProjector = r.HasProjector;
        room.HasComputers = r.HasComputers; room.HasLab = r.HasLab; room.IsOnline = r.IsOnline;
        room.Floor = r.Floor;
        room.AllowedLessonTypes = r.AllowedLessonTypes ?? new List<LessonType>();
        await _db.SaveChangesAsync(cancellationToken);
        return new RoomDto(room.Id, room.BuildingId, room.Building.ShortCode, room.Number, room.RoomType,
            room.Capacity, room.HasProjector, room.HasComputers, room.HasLab, room.IsOnline,
            room.Floor, room.AllowedLessonTypes);
    }
}
