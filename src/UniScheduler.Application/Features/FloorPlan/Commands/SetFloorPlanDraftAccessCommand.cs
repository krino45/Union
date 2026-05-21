using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record SetFloorPlanDraftAccessCommand(Guid DraftId, bool IsOpenToAdmins) : IRequest;

public class SetFloorPlanDraftAccessCommandHandler : IRequestHandler<SetFloorPlanDraftAccessCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public SetFloorPlanDraftAccessCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(SetFloorPlanDraftAccessCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");
        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.FloorPlanDraft), request.DraftId);

        if (draft.OwnerUserId != userId && !_user.IsSuperAdmin)
            throw new ForbiddenException("Только владелец может изменять доступ к черновику.");

        draft.IsOpenToAdmins = request.IsOpenToAdmins;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public record RenameFloorPlanDraftCommand(Guid DraftId, string Name) : IRequest;

public class RenameFloorPlanDraftCommandHandler : IRequestHandler<RenameFloorPlanDraftCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public RenameFloorPlanDraftCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(RenameFloorPlanDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");
        var draft = await _db.FloorPlanDrafts
            .FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.FloorPlanDraft), request.DraftId);

        if (draft.OwnerUserId != userId && !_user.IsSuperAdmin)
            throw new ForbiddenException("Только владелец может переименовать черновик.");

        var name = string.IsNullOrWhiteSpace(request.Name) ? "Черновик" : request.Name.Trim();
        draft.Name = name;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
