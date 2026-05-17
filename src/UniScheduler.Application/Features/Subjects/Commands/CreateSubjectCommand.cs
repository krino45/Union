using MediatR;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Subjects.Commands;

public record CreateSubjectCommand(
    string Name, string ShortName,
    int AcademicYear, Term Term) : IRequest<SubjectDto>;

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext db;

    public CreateSubjectCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<SubjectDto> Handle(CreateSubjectCommand r, CancellationToken cancellationToken)
    {
        var subject = new Subject
        {
            Name = r.Name, ShortName = r.ShortName,
            AcademicYear = r.AcademicYear, Term = r.Term
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(cancellationToken);
        return new SubjectDto(subject.Id, subject.Name, subject.ShortName, subject.AcademicYear, subject.Term);
    }
}
