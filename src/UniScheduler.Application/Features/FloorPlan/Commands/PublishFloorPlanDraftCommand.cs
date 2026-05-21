using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record PublishFloorPlanDraftCommand(Guid DraftId, string Name) : IRequest<Guid>;

public class PublishFloorPlanDraftCommandHandler : IRequestHandler<PublishFloorPlanDraftCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public PublishFloorPlanDraftCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task<Guid> Handle(PublishFloorPlanDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");

        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.FloorPlanDraft), request.DraftId);

        if (draft.OwnerUserId != userId && !_user.IsSuperAdmin)
            throw new ForbiddenException("Только владелец черновика может его опубликовать.");

        // Demote the currently active floor plan, if any.
        var currentActive = await _db.FloorPlans
            .Where(p => p.BuildingId == draft.BuildingId && p.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var fp in currentActive) fp.IsActive = false;

        var newPlan = new Domain.Entities.FloorPlan
        {
            BuildingId = draft.BuildingId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? draft.Name : request.Name.Trim(),
            FloorPlanJson = draft.DraftJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _db.FloorPlans.Add(newPlan);

        await FloorPlanMaterializer.ReplaceAsync(_db, draft.BuildingId, draft.DraftJson, cancellationToken);

        _db.FloorPlanDrafts.Remove(draft);

        await _db.SaveChangesAsync(cancellationToken);
        return newPlan.Id;
    }
}
