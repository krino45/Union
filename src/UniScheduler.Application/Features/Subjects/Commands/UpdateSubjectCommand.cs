using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Subjects.Commands;

public record UpdateSubjectCommand(
    Guid Id, string Name, string ShortName,
    int AcademicYear, Term Term,
    Guid? DepartmentId = null) : IRequest<SubjectDto>;

public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext db;

    public UpdateSubjectCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<SubjectDto> Handle(UpdateSubjectCommand r, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.Include(s => s.Department).FirstOrDefaultAsync(x => x.Id == r.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Subject), r.Id);
        subject.Name = r.Name; subject.ShortName = r.ShortName;
        subject.AcademicYear = r.AcademicYear; subject.Term = r.Term;
        subject.DepartmentId = r.DepartmentId;
        await db.SaveChangesAsync(cancellationToken);
        return new SubjectDto(subject.Id, subject.Name, subject.ShortName, subject.AcademicYear, subject.Term,
            subject.DepartmentId, subject.Department?.Name);
    }
}
