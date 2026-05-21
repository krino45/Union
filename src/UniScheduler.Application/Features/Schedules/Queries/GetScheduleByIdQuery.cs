using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Schedules.Queries;

public record GetScheduleByIdQuery(Guid Id) : IRequest<ScheduleDto>;

public class GetScheduleByIdQueryHandler : IRequestHandler<GetScheduleByIdQuery, ScheduleDto>
{
    private readonly IApplicationDbContext db;
    private readonly ICurrentUserService user;

    public GetScheduleByIdQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    { this.db = db; this.user = user; }

    public async Task<ScheduleDto> Handle(GetScheduleByIdQuery request, CancellationToken cancellationToken)
    {
        var s = await db.Schedules.Include(s => s.Faculty).Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.Id);

        if (!ScheduleAccessGuard.CanAccess(s, user))
            throw new ForbiddenException("Этот черновик расписания закрыт для других пользователей.");

        return new ScheduleDto(
            s.Id, s.AcademicYear, s.Term, s.StartDate, s.EndDate,
            s.FacultyId, s.Faculty?.Name, s.AllowCrossFacultyLessons,
            s.Status, s.GeneratedAt, s.GenerationNotes,
            s.Name, s.OwnerUserId, s.Owner?.Username, s.IsOpenToAdmins,
            s.OwnerUserId == user.UserId);
    }
}
