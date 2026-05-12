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

    public GetScheduleByIdQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<ScheduleDto> Handle(GetScheduleByIdQuery request, CancellationToken cancellationToken)
    {
        var s = await db.Schedules.Include(s => s.Faculty)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.Id);

        return new ScheduleDto(
            s.Id, s.AcademicYear, s.Term, s.StartDate, s.EndDate,
            s.FacultyId, s.Faculty?.Name, s.AllowCrossFacultyLessons,
            s.Status, s.GeneratedAt, s.GenerationNotes);
    }
}
