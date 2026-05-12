using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.TeacherAvailability.Queries;

public record GetTeacherAvailabilityQuery(Guid? TeacherId = null) : IRequest<List<TeacherAvailabilityDto>>;

public class GetTeacherAvailabilityQueryHandler : IRequestHandler<GetTeacherAvailabilityQuery, List<TeacherAvailabilityDto>>
{
    private readonly IApplicationDbContext _db;
    public GetTeacherAvailabilityQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<TeacherAvailabilityDto>> Handle(GetTeacherAvailabilityQuery request, CancellationToken ct)
    {
        var query = _db.TeacherAvailabilities.Include(a => a.Teacher).AsQueryable();
        if (request.TeacherId.HasValue) query = query.Where(a => a.TeacherId == request.TeacherId);
        return await query.Select(a => new TeacherAvailabilityDto(
            a.Id, a.TeacherId, a.Teacher.LastName + " " + a.Teacher.FirstName,
            a.DayOfWeek, a.PairNumber, a.WeekType, a.Reason, a.IsRecurring, a.ValidFrom, a.ValidTo))
            .ToListAsync(ct);
    }
}
