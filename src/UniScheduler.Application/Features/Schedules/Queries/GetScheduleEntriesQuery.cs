using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Queries;

public record GetScheduleEntriesQuery(
    Guid ScheduleId,
    Guid? GroupId = null,
    Guid? TeacherId = null,
    RussianDayOfWeek? DayOfWeek = null) : IRequest<List<ScheduleEntryDto>>;

public class GetScheduleEntriesQueryHandler : IRequestHandler<GetScheduleEntriesQuery, List<ScheduleEntryDto>>
{
    private readonly IApplicationDbContext _db;
    public GetScheduleEntriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ScheduleEntryDto>> Handle(GetScheduleEntriesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ScheduleEntries
            .Include(e => e.Subject)
            .Include(e => e.Teacher)
            .Include(e => e.Room).ThenInclude(r => r!.Building)
            .Include(e => e.StudentGroups).ThenInclude(sg => sg.StudentGroup)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .AsQueryable();

        if (request.GroupId.HasValue)
            query = query.Where(e => e.StudentGroups.Any(sg => sg.StudentGroupId == request.GroupId));
        if (request.TeacherId.HasValue)
            query = query.Where(e => e.TeacherId == request.TeacherId);
        if (request.DayOfWeek.HasValue)
            query = query.Where(e => e.DayOfWeek == request.DayOfWeek);

        var entries = await query.ToListAsync(cancellationToken);

        return entries.Select(e => new ScheduleEntryDto(
            e.Id, e.ScheduleId, e.SubjectId, e.Subject.Name, e.Subject.ShortName,
            e.TeacherId, e.Teacher.DisplayName,
            e.RoomId, e.Room?.Number, e.Room?.Building?.ShortCode,
            e.DayOfWeek, e.PairNumber, e.WeekType, e.LessonType, e.IsOnline,
            e.StudentGroups.Select(sg => new GroupRefDto(sg.StudentGroupId, sg.StudentGroup.Name)).ToList()
        )).OrderBy(e => e.DayOfWeek).ThenBy(e => e.PairNumber).ToList();
    }
}
