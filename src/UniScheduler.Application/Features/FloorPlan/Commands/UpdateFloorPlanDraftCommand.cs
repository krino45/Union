using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record UpdateFloorPlanDraftCommand(Guid DraftId, string DraftJson) : IRequest;

public class UpdateFloorPlanDraftCommandHandler : IRequestHandler<UpdateFloorPlanDraftCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public UpdateFloorPlanDraftCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(UpdateFloorPlanDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");
        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.FloorPlanDraft), request.DraftId);

        if (draft.OwnerUserId != userId && !draft.IsOpenToAdmins)
            throw new ForbiddenException("Этот черновик закрыт для редактирования.");

        draft.DraftJson = request.DraftJson;
        draft.LastModified = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
