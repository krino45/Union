using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Rooms.Commands;

public record CreateRoomCommand(
    Guid BuildingId, string Number, RoomType RoomType, int Capacity,
    bool HasProjector, bool HasComputers, bool IsOnline,
    int Floor = 1,
    List<LessonType>? AllowedLessonTypes = null,
    bool IsEnabled = true,
    Guid? DepartmentId = null) : IRequest<RoomDto>;

public class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext db;
    public CreateRoomCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<RoomDto> Handle(CreateRoomCommand r, CancellationToken cancellationToken)
    {
        var room = new Room
        {
            BuildingId = r.BuildingId, Number = r.Number, RoomType = r.RoomType,
            Capacity = r.Capacity, HasProjector = r.HasProjector,
            HasComputers = r.HasComputers, IsOnline = r.IsOnline,
            Floor = r.Floor, AllowedLessonTypes = r.AllowedLessonTypes ?? new List<LessonType>(),
            IsEnabled = r.IsEnabled, DepartmentId = r.DepartmentId
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);

        var building = await db.Buildings.FirstAsync(b => b.Id == r.BuildingId, cancellationToken);
        string? deptName = null;
        if (r.DepartmentId.HasValue)
        {
            var dept = await db.Departments.FindAsync(new object[] { r.DepartmentId.Value }, cancellationToken);
            deptName = dept?.Name;
        }
        return new RoomDto(room.Id, room.BuildingId, building.ShortCode, room.Number, room.RoomType,
            room.Capacity, room.HasProjector, room.HasComputers, room.IsOnline,
            room.Floor, room.AllowedLessonTypes, room.IsEnabled, room.DepartmentId, deptName);
    }
}
