using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Queries;

public record FloorPlanDraftDto(string DraftJson, DateTime LastModified);

public record GetFloorPlanDraftQuery(Guid BuildingId) : IRequest<FloorPlanDraftDto?>;

public class GetFloorPlanDraftQueryHandler : IRequestHandler<GetFloorPlanDraftQuery, FloorPlanDraftDto?>
{
    private readonly IApplicationDbContext _db;
    public GetFloorPlanDraftQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<FloorPlanDraftDto?> Handle(GetFloorPlanDraftQuery request, CancellationToken cancellationToken)
    {
        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.BuildingId == request.BuildingId, cancellationToken);

        return draft == null ? null : new FloorPlanDraftDto(draft.DraftJson, draft.LastModified);
    }
}
