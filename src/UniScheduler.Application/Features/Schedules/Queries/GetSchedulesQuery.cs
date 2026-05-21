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
    private readonly ICurrentUserService user;

    public GetSchedulesQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    { this.db = db; this.user = user; }

    public async Task<List<ScheduleDto>> Handle(GetSchedulesQuery request, CancellationToken cancellationToken)
    {
        var userId = user.UserId ?? Guid.Empty;
        var isSuper = user.IsSuperAdmin;

        var query = db.Schedules.Include(s => s.Faculty).Include(s => s.Owner).AsQueryable();
        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status);

        // Draft visibility: owner OR open OR no-owner legacy OR SuperAdmin.
        // Published/Archived are always visible (within the university scope).
        if (!isSuper)
        {
            query = query.Where(s =>
                s.Status != ScheduleStatus.Draft
                || s.OwnerUserId == null
                || s.OwnerUserId == userId
                || s.IsOpenToAdmins);
        }

        return await query.OrderByDescending(s => s.AcademicYear).ThenBy(s => s.Term)
            .Select(s => new ScheduleDto(
                s.Id, s.AcademicYear, s.Term, s.StartDate, s.EndDate,
                s.FacultyId, s.Faculty != null ? s.Faculty.Name : null,
                s.AllowCrossFacultyLessons, s.Status, s.GeneratedAt, s.GenerationNotes,
                s.Name,
                s.OwnerUserId,
                s.Owner != null ? s.Owner.Username : null,
                s.IsOpenToAdmins,
                s.OwnerUserId == userId))
            .ToListAsync(cancellationToken);
    }
}
