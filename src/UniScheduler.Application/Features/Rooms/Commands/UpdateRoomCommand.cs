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
    bool HasProjector, bool HasComputers, bool IsOnline,
    int Floor = 1,
    List<LessonType>? AllowedLessonTypes = null,
    bool IsEnabled = true,
    Guid? DepartmentId = null) : IRequest<RoomDto>;

public class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext db;
    public UpdateRoomCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<RoomDto> Handle(UpdateRoomCommand r, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.Include(x => x.Building).Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == r.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), r.Id);

        room.BuildingId = r.BuildingId; room.Number = r.Number; room.RoomType = r.RoomType;
        room.Capacity = r.Capacity; room.HasProjector = r.HasProjector;
        room.HasComputers = r.HasComputers; room.IsOnline = r.IsOnline;
        room.Floor = r.Floor; room.AllowedLessonTypes = r.AllowedLessonTypes ?? new List<LessonType>();
        room.IsEnabled = r.IsEnabled; room.DepartmentId = r.DepartmentId;
        await db.SaveChangesAsync(cancellationToken);

        string? deptName = r.DepartmentId.HasValue ? room.Department?.Name : null;
        return new RoomDto(room.Id, room.BuildingId, room.Building.ShortCode, room.Number, room.RoomType,
            room.Capacity, room.HasProjector, room.HasComputers, room.IsOnline,
            room.Floor, room.AllowedLessonTypes, room.IsEnabled, room.DepartmentId, deptName);
    }
}
