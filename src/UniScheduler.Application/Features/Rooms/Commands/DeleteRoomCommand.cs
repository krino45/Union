using UniScheduler.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.Rooms.Commands;

public record DeleteRoomCommand(Guid Id) : IRequest;

public class DeleteRoomCommandHandler : IRequestHandler<DeleteRoomCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteRoomCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.Id);
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
