using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.PairTimes;

public record PairTimeDto(int PairNumber, string StartTime, string EndTime);

public record GetPairTimesQuery : IRequest<List<PairTimeDto>>;

public class GetPairTimesQueryHandler : IRequestHandler<GetPairTimesQuery, List<PairTimeDto>>
{
    private readonly IApplicationDbContext db;
    public GetPairTimesQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<List<PairTimeDto>> Handle(GetPairTimesQuery request, CancellationToken cancellationToken)
    {
        var slots = await db.PairTimeSlots.OrderBy(p => p.PairNumber).ToListAsync(cancellationToken);
        // Tenants that never customised their grid (e.g. created before pair times were seeded) fall
        // back to the standard defaults so the schedule UI and break calculations still work.
        if (slots.Count == 0)
            return PairTimeDefaults.Dtos();
        return slots.Select(s => new PairTimeDto(
            s.PairNumber,
            s.StartTime.ToString("HH:mm"),
            s.EndTime.ToString("HH:mm")
        )).ToList();
    }
}
