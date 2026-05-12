using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Teachers.Queries;

public record GetTeachersQuery : IRequest<List<TeacherDto>>;

public class GetTeachersQueryHandler : IRequestHandler<GetTeachersQuery, List<TeacherDto>>
{
    private readonly IApplicationDbContext db;

    public GetTeachersQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<List<TeacherDto>> Handle(GetTeachersQuery request, CancellationToken cancellationToken)
    {
        var teachers = await db.Teachers
            .Include(t => t.TeacherSubjects).ThenInclude(ts => ts.Subject)
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .ToListAsync(cancellationToken);

        return teachers.Select(t => new TeacherDto(
            t.Id, t.FirstName, t.LastName, t.MiddleName, t.DisplayName, t.Email,
            t.TeacherSubjects.Select(ts => new TeacherSubjectDto(ts.SubjectId, ts.Subject.Name, ts.LessonType, ts.PreferredRoomType)).ToList()
        )).ToList();
    }
}
