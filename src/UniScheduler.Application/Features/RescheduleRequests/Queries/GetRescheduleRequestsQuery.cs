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
    private readonly ICurrentUserService _user;
    public GetRescheduleRequestsQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task<List<RescheduleRequestDto>> Handle(GetRescheduleRequestsQuery request, CancellationToken ct)
    {
        Guid? effectiveTeacherId = request.TeacherId;

        if (!_user.IsAdmin && _user.UserId.HasValue)
        {
            var appUser = await _db.AppUsers.FindAsync(new object[] { _user.UserId.Value }, ct);
            effectiveTeacherId = appUser?.TeacherId;
        }

        var query = _db.RescheduleRequests
            .Include(r => r.RequestedByTeacher)
            .Include(r => r.OriginalEntry).ThenInclude(e => e.Subject)
            .Include(r => r.ProposedRoom).ThenInclude(rm => rm!.Building)
            .AsQueryable();
        if (request.Status.HasValue) query = query.Where(r => r.Status == request.Status);
        if (effectiveTeacherId.HasValue) query = query.Where(r => r.RequestedByTeacherId == effectiveTeacherId);
        return await query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new RescheduleRequestDto(
                r.Id, r.RequestedByTeacherId,
                r.RequestedByTeacher.LastName + " " + r.RequestedByTeacher.FirstName,
                r.OriginalEntryId,
                r.OriginalEntry.Subject.Name + " — " + r.OriginalEntry.DayOfWeek + ", пара " + r.OriginalEntry.PairNumber,
                r.ProposedDayOfWeek, r.ProposedPairNumber, r.ProposedWeekType,
                r.ProposedRoomId,
                r.ProposedRoom != null ? r.ProposedRoom.Building.ShortCode + "-" + r.ProposedRoom.Number : null,
                r.ProposedIsOnline,
                r.Reason, r.Status, r.AdminNote, r.CreatedAt, r.ResolvedAt))
            .ToListAsync(ct);
    }
}
