using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.StudyPlans;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Queries;

public record GetPlanProgressQuery(Guid ScheduleId) : IRequest<List<PlanProgressItem>>;

public record PlanProgressItem(
    Guid SubjectId,
    string SubjectName,
    string SubjectShortName,
    Guid GroupId,
    string GroupName,
    string LessonType,
    double ExpectedHours,
    double ActualPairsPerWeek,
    int StudyWeeks,
    bool IsUnplaced
);

public class GetPlanProgressQueryHandler : IRequestHandler<GetPlanProgressQuery, List<PlanProgressItem>>
{
    private readonly IApplicationDbContext _db;
    public GetPlanProgressQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<PlanProgressItem>> Handle(GetPlanProgressQuery req, CancellationToken ct)
    {
        var schedule = await _db.Schedules
            .FirstOrDefaultAsync(s => s.Id == req.ScheduleId, ct)
            ?? throw new NotFoundException(nameof(Schedule), req.ScheduleId);

        var entries = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == req.ScheduleId)
            .ToListAsync(ct);

        var studyPlans = await StudyPlanQ.BaseQuery(_db)
            .Where(sp => sp.AcademicYear == schedule.AcademicYear && sp.Term == schedule.Term)
            .ToListAsync(ct);

        // group → its plan (take first if a group appears in multiple plans for the same semester)
        var planByGroup = studyPlans
            .SelectMany(sp => sp.Groups.Select(g => (g.StudentGroupId, sp)))
            .GroupBy(x => x.StudentGroupId)
            .ToDictionary(g => g.Key, g => g.First().sp);

        // entries per group
        var entriesByGroup = new Dictionary<Guid, List<ScheduleEntry>>();
        foreach (var e in entries)
        foreach (var sg in e.StudentGroups)
        {
            if (!entriesByGroup.TryGetValue(sg.StudentGroupId, out var list))
                entriesByGroup[sg.StudentGroupId] = list = new();
            list.Add(e);
        }

        var result = new List<PlanProgressItem>();

        foreach (var (groupId, plan) in planByGroup)
        {
            int studyWeeks = StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan);
            var groupName = plan.Groups.First(g => g.StudentGroupId == groupId).StudentGroup.Name;
            var groupEntries = entriesByGroup.TryGetValue(groupId, out var ge) ? ge : new();

            foreach (var spe in plan.Entries)
            {
                AddIfNeeded(result, spe.Subject, groupId, groupName, LessonType.Lecture,
                    spe.LectureHours, groupEntries, studyWeeks);
                AddIfNeeded(result, spe.Subject, groupId, groupName, LessonType.Practical,
                    spe.PracticalHours, groupEntries, studyWeeks);
                AddIfNeeded(result, spe.Subject, groupId, groupName, LessonType.Lab,
                    spe.LabHours, groupEntries, studyWeeks);
                AddIfNeeded(result, spe.Subject, groupId, groupName, LessonType.Seminar,
                    spe.SeminarHours, groupEntries, studyWeeks);
                AddIfNeeded(result, spe.Subject, groupId, groupName, LessonType.Language,
                    spe.LanguageHours, groupEntries, studyWeeks);
                AddIfNeeded(result, spe.Subject, groupId, groupName, LessonType.PhysicalEducation,
                    spe.PhysicalEducationHours, groupEntries, studyWeeks);
            }
        }

        return result.OrderBy(x => x.GroupName).ThenBy(x => x.SubjectName).ToList();
    }

    private static void AddIfNeeded(
        List<PlanProgressItem> result, Subject subject,
        Guid groupId, string groupName, LessonType lt, double expectedHours,
        List<ScheduleEntry> groupEntries, int studyWeeks)
    {
        if (expectedHours <= 0) return;
        double actualPerWeek = groupEntries
            .Where(e => e.SubjectId == subject.Id && e.LessonType == lt)
            .Select(e => (e.DayOfWeek, e.PairNumber, e.WeekType))
            .Distinct()
            .Sum(s => s.WeekType == WeekType.Both ? 1.0 : 0.5);
        result.Add(new PlanProgressItem(
            subject.Id, subject.Name, subject.ShortName,
            groupId, groupName,
            lt.ToString(),
            expectedHours,
            actualPerWeek,
            studyWeeks,
            actualPerWeek == 0
        ));
    }
}
