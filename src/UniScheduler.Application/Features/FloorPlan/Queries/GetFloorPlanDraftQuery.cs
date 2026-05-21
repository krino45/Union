using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.FloorPlan.Queries;

public record FloorPlanDraftDto(
    Guid Id,
    Guid BuildingId,
    string Name,
    string DraftJson,
    bool IsOpenToAdmins,
    DateTime LastModified,
    Guid OwnerUserId,
    string OwnerUsername,
    bool IsMine);

public record GetFloorPlanDraftQuery(Guid DraftId) : IRequest<FloorPlanDraftDto>;

public class GetFloorPlanDraftQueryHandler : IRequestHandler<GetFloorPlanDraftQuery, FloorPlanDraftDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public GetFloorPlanDraftQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task<FloorPlanDraftDto> Handle(GetFloorPlanDraftQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? Guid.Empty;
        var draft = await _db.FloorPlanDrafts
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.FloorPlanDraft), request.DraftId);

        var isMine = draft.OwnerUserId == userId;
        if (!isMine && !draft.IsOpenToAdmins)
            throw new ForbiddenException("Этот черновик закрыт для других пользователей.");

        return new FloorPlanDraftDto(
            draft.Id, draft.BuildingId, draft.Name, draft.DraftJson,
            draft.IsOpenToAdmins, draft.LastModified,
            draft.OwnerUserId, draft.Owner.Username, isMine);
    }
}
