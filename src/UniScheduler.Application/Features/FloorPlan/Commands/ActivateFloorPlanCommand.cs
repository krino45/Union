using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record ActivateFloorPlanCommand(Guid FloorPlanId) : IRequest;

public class ActivateFloorPlanCommandHandler : IRequestHandler<ActivateFloorPlanCommand>
{
    private readonly IApplicationDbContext _db;
    public ActivateFloorPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ActivateFloorPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _db.FloorPlans.FirstOrDefaultAsync(p => p.Id == request.FloorPlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.FloorPlan), request.FloorPlanId);

        if (plan.IsActive) return;

        var currentActive = await _db.FloorPlans
            .Where(p => p.BuildingId == plan.BuildingId && p.IsActive && p.Id != plan.Id)
            .ToListAsync(cancellationToken);

        // Deactivate the previous active plan first so the partial unique index doesn't conflict
        // within a single SaveChanges call.
        foreach (var fp in currentActive) fp.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        plan.IsActive = true;

        await FloorPlanMaterializer.ReplaceAsync(_db, plan.BuildingId, plan.FloorPlanJson, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
