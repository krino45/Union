using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.RescheduleRequests.Queries;

public record GetRescheduleRequestsQuery(RescheduleStatus? Status = null, Guid? TeacherId = null) : IRequest<List<RescheduleRequestDto>>;

public class GetRescheduleRequestsQueryHandler : IRequestHandler<GetRescheduleRequestsQuery, List<RescheduleRequestDto>>
{
    private readonly IApplicationDbContext _db;
    public GetRescheduleRequestsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<RescheduleRequestDto>> Handle(GetRescheduleRequestsQuery request, CancellationToken ct)
    {
        var query = _db.RescheduleRequests.Include(r => r.RequestedByTeacher).AsQueryable();
        if (request.Status.HasValue) query = query.Where(r => r.Status == request.Status);
        if (request.TeacherId.HasValue) query = query.Where(r => r.RequestedByTeacherId == request.TeacherId);
        return await query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new RescheduleRequestDto(
                r.Id, r.RequestedByTeacherId,
                r.RequestedByTeacher.LastName + " " + r.RequestedByTeacher.FirstName,
                r.OriginalEntryId, r.ProposedDayOfWeek, r.ProposedPairNumber, r.ProposedWeekType,
                r.Reason, r.Status, r.AdminNote, r.CreatedAt, r.ResolvedAt))
            .ToListAsync(ct);
    }
}
