using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Buildings.Commands;

public record DeleteBuildingCommand(Guid Id) : IRequest;

public class DeleteBuildingCommandHandler : IRequestHandler<DeleteBuildingCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteBuildingCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Building), request.Id);

        _db.Buildings.Remove(building);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
