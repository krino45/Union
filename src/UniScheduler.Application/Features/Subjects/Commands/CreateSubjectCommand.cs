using MediatR;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Subjects.Commands;

public record CreateSubjectCommand(
    string Name, string ShortName,
    int AcademicYear, Term Term,
    Guid? DepartmentId = null,
    bool AllowsSubgroups = false,
    int SubgroupCount = 2,
    bool RequiresProjector = false) : IRequest<SubjectDto>;

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
            DepartmentId = r.DepartmentId,
            AllowsSubgroups = r.AllowsSubgroups,
            SubgroupCount = r.SubgroupCount < 2 ? 2 : r.SubgroupCount,
            RequiresProjector = r.RequiresProjector
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(cancellationToken);
        string? deptName = null;
        if (r.DepartmentId.HasValue)
        {
            var dept = await db.Departments.FindAsync(new object[] { r.DepartmentId.Value }, cancellationToken);
            deptName = dept?.Name;
        }
        return new SubjectDto(subject.Id, subject.Name, subject.ShortName, subject.AcademicYear, subject.Term, subject.DepartmentId, deptName,
            subject.AllowsSubgroups, subject.SubgroupCount, subject.RequiresProjector);
    }
}
