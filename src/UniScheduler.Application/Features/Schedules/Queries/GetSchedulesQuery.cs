using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Queries;

public record GetSchedulesQuery(ScheduleStatus? Status = null) : IRequest<List<ScheduleDto>>;

public class GetSchedulesQueryHandler : IRequestHandler<GetSchedulesQuery, List<ScheduleDto>>
{
    private readonly IApplicationDbContext db;

    public GetSchedulesQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<List<ScheduleDto>> Handle(GetSchedulesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Schedules.Include(s => s.Faculty).AsQueryable();
        if (request.Status.HasValue) query = query.Where(s => s.Status == request.Status);
        return await query.OrderByDescending(s => s.AcademicYear).ThenBy(s => s.Term)
            .Select(s => new ScheduleDto(
                s.Id, s.AcademicYear, s.Term, s.StartDate, s.EndDate,
                s.FacultyId, s.Faculty != null ? s.Faculty.Name : null,
                s.AllowCrossFacultyLessons, s.Status, s.GeneratedAt, s.GenerationNotes))
            .ToListAsync(cancellationToken);
    }
}
