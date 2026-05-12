using UniScheduler.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Teachers.Commands;

public record UpdateTeacherCommand(Guid Id, string FirstName, string LastName, string MiddleName, string Email) : IRequest<TeacherDto>;

public class UpdateTeacherCommandHandler : IRequestHandler<UpdateTeacherCommand, TeacherDto>
{
    private readonly IApplicationDbContext db;

    public UpdateTeacherCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<TeacherDto> Handle(UpdateTeacherCommand r, CancellationToken cancellationToken)
    {
        var teacher = await db.Teachers.Include(t => t.TeacherSubjects).ThenInclude(ts => ts.Subject)
            .FirstOrDefaultAsync(t => t.Id == r.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), r.Id);

        teacher.FirstName = r.FirstName; teacher.LastName = r.LastName;
        teacher.MiddleName = r.MiddleName; teacher.Email = r.Email;
        await db.SaveChangesAsync(cancellationToken);
        return new TeacherDto(teacher.Id, teacher.FirstName, teacher.LastName, teacher.MiddleName, teacher.DisplayName, teacher.Email,
            teacher.TeacherSubjects.Select(ts => new TeacherSubjectDto(ts.SubjectId, ts.Subject.Name, ts.LessonType, ts.PreferredRoomType)).ToList());
    }
}
