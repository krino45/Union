using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Invitations.Queries;

public record InvitationDto(
    Guid Id,
    string Email,
    UniversityRole UniversityRole,
    string SystemRole,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsConsumed,
    string? InvitedByUsername);

public record GetInvitationsQuery(Guid UniversityId) : IRequest<List<InvitationDto>>;

public class GetInvitationsQueryHandler : IRequestHandler<GetInvitationsQuery, List<InvitationDto>>
{
    private readonly IApplicationDbContext _db;
    public GetInvitationsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<InvitationDto>> Handle(GetInvitationsQuery request, CancellationToken cancellationToken)
    {
        return await _db.Invitations
            .Where(i => i.UniversityId == request.UniversityId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto(
                i.Id, i.Email, i.UniversityRole, i.SystemRole,
                i.CreatedAt, i.ExpiresAt,
                i.ConsumedAt != null,
                i.InvitedBy.Username))
            .ToListAsync(cancellationToken);
    }
}
