using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

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

        // Weekly academic-hour load aggregated across all Published schedules.
        // 1 pair = 2 academic hours; Both-week pair counts as 2h/week, Odd/Even as 1h/week (averaged).
        var loadRaw = await db.ScheduleEntries
            .Where(e => e.Schedule.Status == ScheduleStatus.Published)
            .GroupBy(e => new { e.TeacherId, e.WeekType })
            .Select(g => new { g.Key.TeacherId, g.Key.WeekType, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var loadByTeacher = loadRaw
            .GroupBy(x => x.TeacherId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.WeekType == WeekType.Both ? x.Count * 2 : x.Count));

        return teachers.Select(t => new TeacherDto(
            t.Id, t.FirstName, t.LastName, t.MiddleName, t.DisplayName, t.Email,
            t.TeacherSubjects.Select(ts => new TeacherSubjectDto(ts.SubjectId, ts.Subject.Name, ts.LessonType, ts.PreferredRoomType)).ToList(),
            loadByTeacher.TryGetValue(t.Id, out var hours) ? hours : 0
        )).ToList();
    }
}
