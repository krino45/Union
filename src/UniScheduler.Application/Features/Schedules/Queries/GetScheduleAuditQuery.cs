using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.StudyPlans;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Queries;

public record GetScheduleAuditQuery(Guid ScheduleId) : IRequest<ScheduleAuditDto>;

public record ScheduleAuditDto(
    List<AuditIssueDto> Conflicts,
    List<AuditIssueDto> Warnings,
    string? GenerationNotes,
    int TotalEntries,
    int CurrentScore,
    int? BaseScore
);

public record AuditIssueDto(string Type, string Description);

public class GetScheduleAuditQueryHandler : IRequestHandler<GetScheduleAuditQuery, ScheduleAuditDto>
{
    private const int SanPinMaxPairsPerDay  = 4;
    private const int SanPinMaxPairsPerWeek = 18;

    private readonly IApplicationDbContext db;
    public GetScheduleAuditQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<ScheduleAuditDto> Handle(GetScheduleAuditQuery request, CancellationToken ct)
    {
        var schedule = await db.Schedules
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId, ct)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);

        var entries = await db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Include(e => e.Teacher)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(ct);

        // ── Study plans ───────────────────────────────────────────────────────
        var studyPlans = await StudyPlanQ.BaseQuery(db)
            .Where(sp => sp.AcademicYear == schedule.AcademicYear && sp.Term == schedule.Term)
            .ToListAsync(ct);

        // group → its plan (take first if somehow in multiple plans for same semester)
        var planByGroup = studyPlans
            .SelectMany(sp => sp.Groups.Select(g => (g.StudentGroupId, sp)))
            .GroupBy(x => x.StudentGroupId)
            .ToDictionary(g => g.Key, g => g.First().sp);

        var groupsQuery = db.StudentGroups.AsQueryable();
        if (schedule.FacultyId.HasValue && !schedule.AllowCrossFacultyLessons)
            groupsQuery = groupsQuery.Where(g => g.FacultyId == schedule.FacultyId);
        var groups = await groupsQuery.ToListAsync(ct);

        var conflicts = new List<AuditIssueDto>();
        var warnings  = new List<AuditIssueDto>();
        var seen = new HashSet<string>();

        // ── Hard conflicts: double-booking ────────────────────────────────────
        for (int i = 0; i < entries.Count; i++)
        for (int j = i + 1; j < entries.Count; j++)
        {
            var a = entries[i]; var b = entries[j];
            if (!SlotsOverlap(a, b)) continue;

            if (!a.IsOnline && !b.IsOnline && a.RoomId.HasValue && a.RoomId == b.RoomId)
                AddUnique(conflicts, seen, "RoomDoubleBooked",
                    $"Аудитория занята дважды: {DayLabel(a.DayOfWeek)} пара {a.PairNumber}");

            if (a.TeacherId == b.TeacherId)
                AddUnique(conflicts, seen, "TeacherDoubleBooked",
                    $"{a.Teacher.LastName}: двойная нагрузка — {DayLabel(a.DayOfWeek)} пара {a.PairNumber}");

            var aGroups = a.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            var bGroups = b.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            if (aGroups.Overlaps(bGroups))
                AddUnique(conflicts, seen, "GroupDoubleBooked",
                    $"Группы пересекаются: {DayLabel(a.DayOfWeek)} пара {a.PairNumber}");
        }

        // Build group → entries lookup
        var entriesByGroup = new Dictionary<Guid, List<ScheduleEntry>>();
        foreach (var e in entries)
        foreach (var sg in e.StudentGroups)
        {
            if (!entriesByGroup.TryGetValue(sg.StudentGroupId, out var list))
                entriesByGroup[sg.StudentGroupId] = list = new();
            list.Add(e);
        }

        foreach (var group in groups)
        {
            var groupEntries = entriesByGroup.TryGetValue(group.Id, out var ge) ? ge : new();

            // ── Hours check: study plan only ──────────────────────────────────
            if (planByGroup.TryGetValue(group.Id, out var plan))
            {
                int studyWeeks = StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan);
                foreach (var spe in plan.Entries)
                {
                    CheckHours(warnings, seen, group, spe.Subject, groupEntries, LessonType.Lecture,   spe.LectureHours,   studyWeeks);
                    CheckHours(warnings, seen, group, spe.Subject, groupEntries, LessonType.Practical, spe.PracticalHours, studyWeeks);
                    CheckHours(warnings, seen, group, spe.Subject, groupEntries, LessonType.Lab,       spe.LabHours,       studyWeeks);
                    CheckHours(warnings, seen, group, spe.Subject, groupEntries, LessonType.Seminar,   spe.SeminarHours,   studyWeeks);
                }
            }

            // ── СанПиН: daily load ────────────────────────────────────────────
            foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
            {
                int oddDay  = groupEntries.Count(e => e.DayOfWeek == day && (e.WeekType == WeekType.Both || e.WeekType == WeekType.Odd));
                int evenDay = groupEntries.Count(e => e.DayOfWeek == day && (e.WeekType == WeekType.Both || e.WeekType == WeekType.Even));
                int max = Math.Max(oddDay, evenDay);
                if (max > SanPinMaxPairsPerDay)
                    AddUnique(warnings, seen, "SanPinDailyOverload",
                        $"СанПиН: {group.Name} — {DayLabel(day)}: {max} пар/день (макс. {SanPinMaxPairsPerDay})");
            }

            // ── СанПиН: weekly load ───────────────────────────────────────────
            int oddWeek  = groupEntries.Count(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Odd);
            int evenWeek = groupEntries.Count(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Even);
            int maxWeek  = Math.Max(oddWeek, evenWeek);
            if (maxWeek > SanPinMaxPairsPerWeek)
                AddUnique(warnings, seen, "SanPinWeeklyOverload",
                    $"СанПиН: {group.Name} — {maxWeek} пар/нед. (макс. {SanPinMaxPairsPerWeek}, т.е. 36 ак.ч.)");

            // ── СанПиН: no day off ────────────────────────────────────────────
            var oddDays  = groupEntries.Where(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Odd ).Select(e => e.DayOfWeek).Distinct().Count();
            var evenDays = groupEntries.Where(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Even).Select(e => e.DayOfWeek).Distinct().Count();
            if (Math.Max(oddDays, evenDays) >= 6)
                AddUnique(warnings, seen, "SanPinNoDayOff",
                    $"СанПиН: {group.Name} — занятия все 6 дней, нет выходного");

            // ── Windows (gaps) ────────────────────────────────────────────────
            foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
            {
                CheckWindows(warnings, seen, group, groupEntries, day, WeekType.Odd);
                CheckWindows(warnings, seen, group, groupEntries, day, WeekType.Even);
            }
        }

        var nodes = await db.FloorPlanNodes.ToListAsync(ct);
        var edges = await db.FloorPlanEdges.ToListAsync(ct);
        var bldDists = await db.BuildingDistances.ToListAsync(ct);
        var rooms = await db.Rooms.ToListAsync(ct);
        var pairSlots = await db.PairTimeSlots.ToListAsync(ct);
        var scoreCtx = ScheduleScoreCalculator.BuildScoreContext(nodes, edges, bldDists, rooms, pairSlots);

        var currentScore = ScheduleScoreCalculator.Compute(entries, scoreCtx);
        return new ScheduleAuditDto(conflicts, warnings, schedule.GenerationNotes, entries.Count, currentScore, schedule.BaseScore);
    }

    // ── Validation helpers ────────────────────────────────────────────────────

    private static void CheckHours(
        List<AuditIssueDto> warnings, HashSet<string> seen,
        StudentGroup group, Subject subject, List<ScheduleEntry> groupEntries,
        LessonType lt, double expectedTotalHours, int studyWeeks)
    {
        if (expectedTotalHours <= 0) return;

        // Odd/Even entries each contribute 0.5 pair/week on average; Both = 1.0
        double actualPairsPerWeek = groupEntries
            .Where(e => e.SubjectId == subject.Id && e.LessonType == lt)
            .Sum(e => e.WeekType == WeekType.Both ? 1.0 : 0.5);

        // 1 pair = 2 ак.ч.  ×  studyWeeks = total semester hours
        double actualTotal = actualPairsPerWeek * 2.0 * studyWeeks;
        const double tolerance = 9.0; // biweekly model rounds to nearest 9h (half odd/even cycle)

        if (actualTotal < expectedTotalHours - tolerance)
            AddUnique(warnings, seen, "HoursUnderScheduled",
                $"{group.Name}: {subject.ShortName} ({LtLabel(lt)}) — {actualTotal:F0} ак.ч. из {expectedTotalHours} (за {studyWeeks} нед.)");
        else if (actualTotal > expectedTotalHours + tolerance)
            AddUnique(warnings, seen, "HoursOverScheduled",
                $"{group.Name}: {subject.ShortName} ({LtLabel(lt)}) — {actualTotal:F0} ак.ч. вместо {expectedTotalHours} (за {studyWeeks} нед.)");
    }

    private static void CheckWindows(
        List<AuditIssueDto> warnings, HashSet<string> seen,
        StudentGroup group, List<ScheduleEntry> groupEntries,
        RussianDayOfWeek day, WeekType weekVariant)
    {
        var pairs = groupEntries
            .Where(e => e.DayOfWeek == day && (e.WeekType == WeekType.Both || e.WeekType == weekVariant))
            .Select(e => e.PairNumber).OrderBy(p => p).ToList();
        if (pairs.Count < 2) return;
        int windows = pairs[^1] - pairs[0] + 1 - pairs.Count;
        if (windows > 0)
        {
            string wk = weekVariant == WeekType.Odd ? "нечётная" : "чётная";
            AddUnique(warnings, seen, "Window",
                $"Окно: {group.Name} — {DayLabel(day)} ({wk}): {windows} пустых пар между {pairs[0]} и {pairs[^1]}");
        }
    }

    private static bool SlotsOverlap(ScheduleEntry a, ScheduleEntry b)
    {
        if (a.DayOfWeek != b.DayOfWeek || a.PairNumber != b.PairNumber) return false;
        if (a.WeekType == WeekType.Both || b.WeekType == WeekType.Both) return true;
        return a.WeekType == b.WeekType;
    }

    private static void AddUnique(List<AuditIssueDto> list, HashSet<string> seen, string type, string desc)
    {
        if (seen.Add($"{type}:{desc}")) list.Add(new AuditIssueDto(type, desc));
    }

    private static string DayLabel(RussianDayOfWeek day) => day switch
    {
        RussianDayOfWeek.Monday    => "Пн",
        RussianDayOfWeek.Tuesday   => "Вт",
        RussianDayOfWeek.Wednesday => "Ср",
        RussianDayOfWeek.Thursday  => "Чт",
        RussianDayOfWeek.Friday    => "Пт",
        RussianDayOfWeek.Saturday  => "Сб",
        _                          => day.ToString()
    };

    private static string LtLabel(LessonType lt) => lt switch
    {
        LessonType.Lecture   => "лек.",
        LessonType.Practical => "пр.",
        LessonType.Lab       => "лаб.",
        LessonType.Seminar   => "сем.",
        _                    => ""
    };
}
