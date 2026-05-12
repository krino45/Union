using MediatR;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record CreateScheduleCommand(
    int AcademicYear,
    Term Term,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? FacultyId,
    bool AllowCrossFacultyLessons) : IRequest<ScheduleDto>;

public class CreateScheduleCommandHandler : IRequestHandler<CreateScheduleCommand, ScheduleDto>
{
    private readonly IApplicationDbContext db;

    public CreateScheduleCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<ScheduleDto> Handle(CreateScheduleCommand r, CancellationToken cancellationToken)
    {
        var schedule = new Schedule
        {
            AcademicYear = r.AcademicYear,
            Term = r.Term,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            FacultyId = r.FacultyId,
            AllowCrossFacultyLessons = r.AllowCrossFacultyLessons,
            Status = ScheduleStatus.Draft
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        string? facultyName = null;
        if (r.FacultyId.HasValue)
        {
            var faculty = await db.Faculties.FindAsync(new object[] { r.FacultyId.Value }, cancellationToken);
            facultyName = faculty?.Name;
        }

        return new ScheduleDto(schedule.Id, schedule.AcademicYear, schedule.Term,
            schedule.StartDate, schedule.EndDate,
            schedule.FacultyId, facultyName, schedule.AllowCrossFacultyLessons,
            schedule.Status, null, null);
    }
}
