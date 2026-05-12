using MediatR;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Subjects.Commands;

public record CreateSubjectCommand(
    string Name, string ShortName,
    int AcademicYear, Term Term,
    double LectureHoursPerWeek, double PracticalHoursPerWeek, double LabHoursPerWeek,
    WeekType LectureWeekType, WeekType PracticalWeekType, WeekType LabWeekType) : IRequest<SubjectDto>;

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext db;

    public CreateSubjectCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<SubjectDto> Handle(CreateSubjectCommand r, CancellationToken cancellationToken)
    {
        var subject = new Subject
        {
            Name = r.Name, ShortName = r.ShortName,
            AcademicYear = r.AcademicYear, Term = r.Term,
            LectureHoursPerWeek = r.LectureHoursPerWeek,
            PracticalHoursPerWeek = r.PracticalHoursPerWeek,
            LabHoursPerWeek = r.LabHoursPerWeek,
            LectureWeekType = r.LectureWeekType,
            PracticalWeekType = r.PracticalWeekType,
            LabWeekType = r.LabWeekType
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(cancellationToken);
        return new SubjectDto(subject.Id, subject.Name, subject.ShortName, subject.AcademicYear, subject.Term,
            subject.LectureHoursPerWeek, subject.PracticalHoursPerWeek, subject.LabHoursPerWeek,
            subject.LectureWeekType, subject.PracticalWeekType, subject.LabWeekType);
    }
}
