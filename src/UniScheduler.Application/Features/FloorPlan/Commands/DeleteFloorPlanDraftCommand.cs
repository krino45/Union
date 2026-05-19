using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record DeleteFloorPlanDraftCommand(Guid BuildingId) : IRequest;

public class DeleteFloorPlanDraftCommandHandler : IRequestHandler<DeleteFloorPlanDraftCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteFloorPlanDraftCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteFloorPlanDraftCommand request, CancellationToken cancellationToken)
    {
        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.BuildingId == request.BuildingId, cancellationToken);
        if (draft == null) return;
        _db.FloorPlanDrafts.Remove(draft);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
