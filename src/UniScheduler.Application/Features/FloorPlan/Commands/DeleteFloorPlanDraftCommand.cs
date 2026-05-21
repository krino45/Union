using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record DeleteFloorPlanDraftCommand(Guid DraftId) : IRequest;

public class DeleteFloorPlanDraftCommandHandler : IRequestHandler<DeleteFloorPlanDraftCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public DeleteFloorPlanDraftCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(DeleteFloorPlanDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");
        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken);
        if (draft == null) return;

        if (draft.OwnerUserId != userId && !_user.IsSuperAdmin)
            throw new ForbiddenException("Только владелец может удалить черновик.");

        _db.FloorPlanDrafts.Remove(draft);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
