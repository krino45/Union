using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
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
            .Include(e => e.StudentGroups).ThenInclude(sg => sg.StudentGroup)
            .Include(e => e.Teacher)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(ct);

        //  Study plans 
        var studyPlans = await StudyPlanQ.BaseQuery(db)
            .Where(sp => sp.AcademicYear == schedule.AcademicYear && sp.Term == schedule.Term)
            .ToListAsync(ct);

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

        //  Hard conflicts: double-booking
        for (int i = 0; i < entries.Count; i++)
        for (int j = i + 1; j < entries.Count; j++)
        {
            var a = entries[i]; var b = entries[j];
            if (!SlotsOverlap(a, b)) continue;

            // Parallel sessions of one logical class (language streams / lab subgroups) share the
            // group/teacher/room slot by design - never a real conflict.
            if (a.ParallelGroupId.HasValue && a.ParallelGroupId == b.ParallelGroupId ) continue;

            // Language classes and PE can have multiple teachers per requirement
            bool samePhysicalSession = (a.TeacherId == b.TeacherId ||
                                        a.LessonType is LessonType.Language or LessonType.PhysicalEducation)
                                       && !a.IsOnline && !b.IsOnline
                                       && a.RoomId.HasValue && a.RoomId == b.RoomId;

            string slot = $"{DayLabel(a.DayOfWeek)} пара {a.PairNumber} ({WeekOverlapLabel(a.WeekType, b.WeekType)})";

            if (!samePhysicalSession && !a.IsOnline && !b.IsOnline && a.RoomId.HasValue && a.RoomId == b.RoomId)
                AddUnique(conflicts, seen, "RoomDoubleBooked",
                    $"Аудитория занята дважды: {slot}");

            if (!samePhysicalSession && a.TeacherId == b.TeacherId)
                AddUnique(conflicts, seen, "TeacherDoubleBooked",
                    $"{a.Teacher.LastName}: двойная нагрузка — {slot}");

            var aGroups = a.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            var bGroups = b.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            var sharedGroupIds = aGroups.Intersect(bGroups).ToHashSet();
            if (!samePhysicalSession && sharedGroupIds.Count > 0 && !AreDistinctSubgroups(a.SubgroupLabel, b.SubgroupLabel))
            {
                var names = a.StudentGroups
                    .Where(sg => sharedGroupIds.Contains(sg.StudentGroupId))
                    .Select(sg => sg.StudentGroup?.Name ?? sg.StudentGroupId.ToString())
                    .OrderBy(n => n);
                AddUnique(conflicts, seen, "GroupDoubleBooked",
                    $"Группы пересекаются ({string.Join(", ", names)}): {slot}");
            }
        }

        var rooms = await db.Rooms.Include(r => r.Department).Include(r => r.Building).ToListAsync(ct);
        var entriesByRoom = new Dictionary<Guid, List<ScheduleEntry>>();
        foreach (var e in entries)
        {
            if (e.RoomId is null) continue;
            if (!entriesByRoom.TryGetValue(e.RoomId.Value, out var list))
                entriesByRoom[e.RoomId.Value] = list = [];
            list.Add(e);
        }

        foreach (var room in rooms)
        {
            if (!entriesByRoom.TryGetValue(room.Id, out var list)) continue;
            foreach (var e in list)
            {
                var totalStudents = groups
                    .Where(g => e.StudentGroups.Select(gr => gr.StudentGroupId).Contains(g.Id))
                    .Sum(g => (int?)g.StudentCount) ?? 0;
                if (room.Capacity > 0 && totalStudents > room.Capacity)
                {
                    AddUnique(warnings, seen, "RoomCapacityExceeded",
                        $"Аудитория {room.Building.ShortCode}-{room.Number}: ({DayLabel(e.DayOfWeek)} {e.PairNumber}) " +
                        $"группы не помещаются ({totalStudents}/{room.Capacity}).");
                }

                if (room.AllowedLessonTypes.Count > 0 && !room.AllowedLessonTypes.Contains(e.LessonType))
                {
                    AddUnique(warnings, seen, "RoomLessonTypeMismatch",
                        $"Аудитория {room.Building.ShortCode}-{room.Number}: ({DayLabel(e.DayOfWeek)} {e.PairNumber}) " +
                        $"расхождение типов занятий (надо: {LtLabel(e.LessonType)}, можно: {string.Join(", ", room.AllowedLessonTypes.Select(LtLabel))}).");
                }

                if (!room.IsEnabled)
                {
                    AddUnique(warnings, seen, "RoomDisabled",
                        $"Аудитория {room.Building.ShortCode}-{room.Number}: использование отключенной аудитории");
                }
            }
        }
        
        var entriesByGroup = new Dictionary<Guid, List<ScheduleEntry>>();
        foreach (var e in entries)
        foreach (var sg in e.StudentGroups)
        {
            if (!entriesByGroup.TryGetValue(sg.StudentGroupId, out var list))
                entriesByGroup[sg.StudentGroupId] = list = [];
            list.Add(e);
        }

        foreach (var group in groups)
        {
            var rawGroupEntries = entriesByGroup.TryGetValue(group.Id, out var ge) ? ge : new();
            var groupEntries = CollapseParallel(rawGroupEntries);

            //  Hours check 
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

            //  СанПиН: daily load 
            foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
            {
                int oddDay  = groupEntries.Count(e => e.DayOfWeek == day && (e.WeekType == WeekType.Both || e.WeekType == WeekType.Odd));
                int evenDay = groupEntries.Count(e => e.DayOfWeek == day && (e.WeekType == WeekType.Both || e.WeekType == WeekType.Even));
                int max = Math.Max(oddDay, evenDay);
                if (max > SanPinMaxPairsPerDay)
                    AddUnique(warnings, seen, "SanPinDailyOverload",
                        $"СанПиН: {group.Name} — {DayLabel(day)}: {max} пар/день (макс. {SanPinMaxPairsPerDay})");
            }

            //  СанПиН: weekly load 
            int oddWeek  = groupEntries.Count(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Odd);
            int evenWeek = groupEntries.Count(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Even);
            int maxWeek  = Math.Max(oddWeek, evenWeek);
            if (maxWeek > SanPinMaxPairsPerWeek)
                AddUnique(warnings, seen, "SanPinWeeklyOverload",
                    $"СанПиН: {group.Name} — {maxWeek} пар/нед. (макс. {SanPinMaxPairsPerWeek}, т.е. 36 ак.ч.)");

            //  СанПиН: no day off 
            var oddDays  = groupEntries.Where(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Odd ).Select(e => e.DayOfWeek).Distinct().Count();
            var evenDays = groupEntries.Where(e => e.WeekType == WeekType.Both || e.WeekType == WeekType.Even).Select(e => e.DayOfWeek).Distinct().Count();
            if (Math.Max(oddDays, evenDays) >= 6)
                AddUnique(warnings, seen, "SanPinNoDayOff",
                    $"СанПиН: {group.Name} — занятия все 6 дней, нет выходного");

            //  Windows (gaps) 
            foreach (RussianDayOfWeek day in Enum.GetValues<RussianDayOfWeek>())
            {
                CheckWindows(warnings, seen, group, groupEntries, day, WeekType.Odd);
                CheckWindows(warnings, seen, group, groupEntries, day, WeekType.Even);
            }
        }

        var nodes = await db.FloorPlanNodes.ToListAsync(ct);
        var edges = await db.FloorPlanEdges.ToListAsync(ct);
        var bldDists = await db.BuildingDistances.ToListAsync(ct);
        var pairSlots = await db.PairTimeSlots.ToListAsync(ct);

        var subjectIds = entries.Select(e => e.SubjectId).Distinct().ToList();
        var subjects = await db.Subjects.Include(s => s.Department)
            .Where(s => subjectIds.Contains(s.Id)).ToListAsync(ct);

        var settings = await db.SolverSettings.FirstOrDefaultAsync(ct);
        var scoreCtx = ScheduleScoreCalculator.BuildScoreContext(
            nodes, edges, bldDists, rooms, pairSlots, subjects, new SolverWeights(settings));

        var currentScore = ScheduleScoreCalculator.Compute(entries, scoreCtx);
        return new ScheduleAuditDto(conflicts, warnings, schedule.GenerationNotes, entries.Count, currentScore, schedule.BaseScore);
    }

    //  Validation helpers 

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
        var dayEntries = groupEntries
            .Where(e => e.DayOfWeek == day && (e.WeekType == WeekType.Both || e.WeekType == weekVariant))
            .ToList();
        if (dayEntries.Count < 2) return;

        // A pair is "online" for the group only if every class at that pair is online (no campus presence).
        var onlineByPair = dayEntries.GroupBy(e => e.PairNumber).ToDictionary(g => g.Key, g => g.All(e => e.IsOnline));
        var pairs = onlineByPair.Keys.OrderBy(p => p).ToList();
        string wk = weekVariant == WeekType.Odd ? "нечётная" : "чётная";

        int badWindowPairs = 0;
        for (int i = 0; i + 1 < pairs.Count; i++)
        {
            int a = pairs[i], b = pairs[i + 1];
            int gap = b - a - 1;
            if (gap > 0)
            {
                if (!onlineByPair[a] && !onlineByPair[b]) badWindowPairs += gap;
            }
            else if (onlineByPair[a] != onlineByPair[b])
            {
                AddUnique(warnings, seen, "OnlineTransition",
                    $"Нет окна между онлайн и очной парой: {group.Name} — {DayLabel(day)} ({wk}), пары {a}–{b}");
            }
        }

        if (badWindowPairs > 0)
            AddUnique(warnings, seen, "Window",
                $"Окно: {group.Name} — {DayLabel(day)} ({wk}): {badWindowPairs} пустых пар");
    }

    private static bool AreDistinctSubgroups(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a)
           && !string.IsNullOrWhiteSpace(b)
           && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // Keeps one representative per parallel group; entries without a ParallelGroupId pass through.
    private static List<ScheduleEntry> CollapseParallel(List<ScheduleEntry> source)
    {
        var result = new List<ScheduleEntry>(source.Count);
        var seenGroups = new HashSet<Guid>();
        foreach (var e in source)
        {
            if (e.ParallelGroupId is { } pg && !seenGroups.Add(pg)) continue;
            result.Add(e);
        }
        return result;
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

    // Returns the effective week label for the overlapping pair of entries.
    private static string WeekOverlapLabel(WeekType a, WeekType b)
    {
        if (a == WeekType.Both && b == WeekType.Both) return "каждую нед.";
        if (a == WeekType.Both || b == WeekType.Both)
        {
            var specific = a == WeekType.Both ? b : a;
            return specific == WeekType.Odd ? "нечётная + каждая" : "чётная + каждая";
        }
        return a == WeekType.Odd ? "нечётная" : "чётная";
    }
}
