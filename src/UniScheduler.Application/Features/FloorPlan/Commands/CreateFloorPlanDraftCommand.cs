using MediatR;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.FloorPlan.Commands;

public record CreateFloorPlanDraftCommand(Guid BuildingId, string Name, string DraftJson) : IRequest<Guid>;

public class CreateFloorPlanDraftCommandHandler : IRequestHandler<CreateFloorPlanDraftCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public CreateFloorPlanDraftCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task<Guid> Handle(CreateFloorPlanDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Требуется аутентификация.");
        var name = string.IsNullOrWhiteSpace(request.Name) ? "Черновик" : request.Name.Trim();

        var draft = new FloorPlanDraft
        {
            BuildingId = request.BuildingId,
            OwnerUserId = userId,
            Name = name,
            DraftJson = request.DraftJson ?? string.Empty,
            IsOpenToAdmins = false,
            LastModified = DateTime.UtcNow
        };
        _db.FloorPlanDrafts.Add(draft);
        await _db.SaveChangesAsync(cancellationToken);
        return draft.Id;
    }
}
