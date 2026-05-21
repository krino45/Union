using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record DeleteFloorPlanCommand(Guid FloorPlanId) : IRequest;

public class DeleteFloorPlanCommandHandler : IRequestHandler<DeleteFloorPlanCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteFloorPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteFloorPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _db.FloorPlans.FirstOrDefaultAsync(p => p.Id == request.FloorPlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.FloorPlan), request.FloorPlanId);

        if (plan.IsActive)
            throw new InvalidOperationException("Невозможно удалить активный план этажей. Сначала активируйте другую версию.");

        _db.FloorPlans.Remove(plan);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
