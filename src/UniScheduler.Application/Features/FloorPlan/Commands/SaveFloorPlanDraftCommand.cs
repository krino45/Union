using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record SaveFloorPlanDraftCommand(Guid BuildingId, string DraftJson) : IRequest;

public class SaveFloorPlanDraftCommandHandler : IRequestHandler<SaveFloorPlanDraftCommand>
{
    private readonly IApplicationDbContext _db;
    public SaveFloorPlanDraftCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SaveFloorPlanDraftCommand request, CancellationToken cancellationToken)
    {
        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.BuildingId == request.BuildingId, cancellationToken);

        if (draft == null)
        {
            draft = new FloorPlanDraft
            {
                BuildingId = request.BuildingId,
                DraftJson = request.DraftJson,
                LastModified = DateTime.UtcNow
            };
            _db.FloorPlanDrafts.Add(draft);
        }
        else
        {
            draft.DraftJson = request.DraftJson;
            draft.LastModified = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
