using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Queries;

public record FloorPlanDraftSummaryDto(
    Guid Id,
    Guid BuildingId,
    string Name,
    bool IsOpenToAdmins,
    DateTime LastModified,
    Guid OwnerUserId,
    string OwnerUsername,
    bool IsMine);

public record GetFloorPlanDraftsQuery(Guid BuildingId) : IRequest<List<FloorPlanDraftSummaryDto>>;

public class GetFloorPlanDraftsQueryHandler : IRequestHandler<GetFloorPlanDraftsQuery, List<FloorPlanDraftSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public GetFloorPlanDraftsQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task<List<FloorPlanDraftSummaryDto>> Handle(GetFloorPlanDraftsQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? Guid.Empty;

        return await _db.FloorPlanDrafts
            .Where(d => d.BuildingId == request.BuildingId
                && (d.OwnerUserId == userId || d.IsOpenToAdmins))
            .OrderByDescending(d => d.LastModified)
            .Select(d => new FloorPlanDraftSummaryDto(
                d.Id,
                d.BuildingId,
                d.Name,
                d.IsOpenToAdmins,
                d.LastModified,
                d.OwnerUserId,
                d.Owner.Username,
                d.OwnerUserId == userId))
            .ToListAsync(cancellationToken);
    }
}
