using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Queries;

public record FloorPlanSummaryDto(
    Guid Id,
    Guid BuildingId,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    Guid? CreatedByUserId,
    string? CreatedByUsername);

public record GetFloorPlansQuery(Guid BuildingId) : IRequest<List<FloorPlanSummaryDto>>;

public class GetFloorPlansQueryHandler : IRequestHandler<GetFloorPlansQuery, List<FloorPlanSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    public GetFloorPlansQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<FloorPlanSummaryDto>> Handle(GetFloorPlansQuery request, CancellationToken cancellationToken)
    {
        return await _db.FloorPlans
            .Where(p => p.BuildingId == request.BuildingId)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new FloorPlanSummaryDto(
                p.Id, p.BuildingId, p.Name, p.IsActive, p.CreatedAt,
                p.CreatedByUserId,
                p.CreatedByUser != null ? p.CreatedByUser.Username : null))
            .ToListAsync(cancellationToken);
    }
}
