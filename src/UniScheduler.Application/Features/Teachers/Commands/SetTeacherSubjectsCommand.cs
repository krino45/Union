using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Teachers.Commands;

public record TeacherSubjectAssignment(Guid SubjectId, LessonType LessonType, RoomType? PreferredRoomType);

public record SetTeacherSubjectsCommand(Guid TeacherId, List<TeacherSubjectAssignment> Subjects) : IRequest;

public class SetTeacherSubjectsCommandHandler : IRequestHandler<SetTeacherSubjectsCommand>
{
    private readonly IApplicationDbContext db;
    public SetTeacherSubjectsCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task Handle(SetTeacherSubjectsCommand request, CancellationToken cancellationToken)
    {
        var teacher = await db.Teachers.Include(t => t.TeacherSubjects)
            .FirstOrDefaultAsync(t => t.Id == request.TeacherId, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.TeacherId);

        db.TeacherSubjects.RemoveRange(teacher.TeacherSubjects);

        foreach (var s in request.Subjects)
        {
            db.TeacherSubjects.Add(new TeacherSubject
            {
                TeacherId = request.TeacherId,
                SubjectId = s.SubjectId,
                LessonType = s.LessonType,
                PreferredRoomType = s.PreferredRoomType
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
