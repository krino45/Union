using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.StudyPlans;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record BackfillTargets(bool Rooms = true, bool Teachers = true, bool StudyPlans = true, bool RoomBindings = true);

public record RoomBackfillChange(Guid RoomId, string RoomLabel, List<LessonType> AddedTypes);
public record TeacherSubjectAddDto(Guid SubjectId, string SubjectName, LessonType LessonType);
public record TeacherBackfillChange(Guid TeacherId, string TeacherName, List<TeacherSubjectAddDto> Added);
public record StudyPlanHourChangeDto(Guid SubjectId, string SubjectName, string Field, string FieldLabel, double OldHours, double NewHours);
public record StudyPlanBackfillChange(Guid StudyPlanId, string PlanName, List<StudyPlanHourChangeDto> Changes);
public record SubjectRoomBindingChange(Guid SubjectId, string SubjectName, LessonType LessonType, List<string> RoomLabels);

public record BackfillPreviewDto(
    List<RoomBackfillChange> Rooms,
    List<TeacherBackfillChange> Teachers,
    List<StudyPlanBackfillChange> StudyPlans,
    List<SubjectRoomBindingChange> RoomBindings);

public record BackfillResultDto(int RoomsUpdated, int TeacherLinksAdded, int StudyPlanFieldsUpdated, int RoomBindingsAdded);

public record PreviewBackfillQuery(Guid ScheduleId, BackfillTargets Targets) : IRequest<BackfillPreviewDto>;

public class PreviewBackfillQueryHandler : IRequestHandler<PreviewBackfillQuery, BackfillPreviewDto>
{
    private readonly IApplicationDbContext _db;
    public PreviewBackfillQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BackfillPreviewDto> Handle(PreviewBackfillQuery req, CancellationToken ct)
        => (await BackfillEngine.Run(_db, req.ScheduleId, req.Targets, apply: false, ct)).Preview;
}

public record ApplyBackfillCommand(Guid ScheduleId, BackfillTargets Targets) : IRequest<BackfillResultDto>;

public class ApplyBackfillCommandHandler : IRequestHandler<ApplyBackfillCommand, BackfillResultDto>
{
    private readonly IApplicationDbContext _db;
    public ApplyBackfillCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BackfillResultDto> Handle(ApplyBackfillCommand req, CancellationToken ct)
        => (await BackfillEngine.Run(_db, req.ScheduleId, req.Targets, apply: true, ct)).Result;
}

internal static class BackfillEngine
{
    // 1 пара = 2 академических часа.
    private const double HoursPerPair = 2.0;

    public static async Task<(BackfillPreviewDto Preview, BackfillResultDto Result)> Run(
        IApplicationDbContext db, Guid scheduleId, BackfillTargets targets, bool apply, CancellationToken ct)
    {
        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct)
            ?? throw new NotFoundException(nameof(Schedule), scheduleId);

        var entries = await db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == scheduleId)
            .ToListAsync(ct);

        var subjectNames = await db.Subjects.ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var roomChanges = new List<RoomBackfillChange>();
        var roomApply = new List<(Room Room, List<LessonType> Add)>();
        var teacherChanges = new List<TeacherBackfillChange>();
        var teacherApply = new List<(Teacher Teacher, List<(Guid SubjectId, LessonType Lt)> Add)>();
        var planChanges = new List<StudyPlanBackfillChange>();
        var planApply = new List<(StudyPlan Plan, List<StudyPlanHourChangeDto> Changes)>();
        var bindingChanges = new List<SubjectRoomBindingChange>();
        var bindingApply = new List<(Guid SubjectId, LessonType Lt, List<Guid> AddRoomIds)>();

        //  Rooms - observed lesson types per room
        if (targets.Rooms)
        {
            var usedByRoom = entries
                .Where(e => e.RoomId.HasValue)
                .GroupBy(e => e.RoomId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(e => e.LessonType).Distinct().ToList());

            var roomIds = usedByRoom.Keys.ToList();
            var rooms = await db.Rooms.Include(r => r.Building)
                .Where(r => roomIds.Contains(r.Id)).ToListAsync(ct);

            foreach (var room in rooms)
            {
                if (room.IsDistributed) continue;

                var add = usedByRoom[room.Id]
                    .Where(lt => !room.AllowedLessonTypes.Contains(lt)).Distinct().ToList();
                if (add.Count == 0) continue;

                var sc = room.Building?.ShortCode;
                var label = (string.IsNullOrEmpty(sc) ? "" : sc + "-") + room.Number;
                roomChanges.Add(new RoomBackfillChange(room.Id, label, add));
                roomApply.Add((room, add));
            }
        }

        //  Teachers - observed (subject, lesson type) pairs
        if (targets.Teachers)
        {
            var usedByTeacher = entries
                .GroupBy(e => e.TeacherId)
                .ToDictionary(g => g.Key, g => g.Select(e => (e.SubjectId, e.LessonType)).Distinct().ToList());

            var teacherIds = usedByTeacher.Keys.ToList();
            var teachers = await db.Teachers.Include(t => t.TeacherSubjects)
                .Where(t => teacherIds.Contains(t.Id)).ToListAsync(ct);

            foreach (var teacher in teachers)
            {
                var existing = teacher.TeacherSubjects
                    .Select(ts => (ts.SubjectId, ts.LessonType)).ToHashSet();
                var add = usedByTeacher[teacher.Id].Where(p => !existing.Contains(p)).ToList();
                if (add.Count == 0) continue;

                var dtos = add.Select(p => new TeacherSubjectAddDto(
                        p.SubjectId, subjectNames.GetValueOrDefault(p.SubjectId, "?"), p.LessonType))
                    .OrderBy(d => d.SubjectName).ToList();
                teacherChanges.Add(new TeacherBackfillChange(teacher.Id, teacher.DisplayName, dtos));
                teacherApply.Add((teacher, add));
            }
        }

        //  Subject -> lab room bindings: learn "this discipline's lab uses these rooms" from the
        //  observed schedule and turn it into a hard constraint (the equipped-lab-room case).
        if (targets.RoomBindings)
        {
            var labEntries = entries.Where(e =>
                    e.LessonType == LessonType.Lab && !e.IsOnline &&
                    e.RoomId.HasValue && e.RoomId.Value != SchedulerSentinels.OverflowRoomId)
                .ToList();
            if (labEntries.Count > 0)
            {
                var usedRoomsBySubject = labEntries
                    .GroupBy(e => e.SubjectId)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.RoomId!.Value).Distinct().ToList());

                var subjIds = usedRoomsBySubject.Keys.ToList();
                var existingBindings = await db.SubjectRoomBindings
                    .Where(b => subjIds.Contains(b.SubjectId) && b.LessonType == LessonType.Lab)
                    .ToListAsync(ct);
                var existingBySubject = existingBindings
                    .GroupBy(b => b.SubjectId)
                    .ToDictionary(g => g.Key, g => g.Select(b => b.RoomId).ToHashSet());

                var bindRoomIds = usedRoomsBySubject.SelectMany(kv => kv.Value).Distinct().ToList();
                var bindRooms = await db.Rooms.Include(r => r.Building)
                    .Where(r => bindRoomIds.Contains(r.Id) && !r.IsDistributed).ToListAsync(ct);
                var roomLabelById = bindRooms.ToDictionary(r => r.Id,
                    r => (string.IsNullOrEmpty(r.Building?.ShortCode) ? "" : r.Building!.ShortCode + "-") + r.Number);

                foreach (var (subjId, roomIds) in usedRoomsBySubject)
                {
                    var existing = existingBySubject.GetValueOrDefault(subjId) ?? new HashSet<Guid>();
                    var add = roomIds.Where(rid => !existing.Contains(rid) && roomLabelById.ContainsKey(rid)).ToList();
                    if (add.Count == 0) continue;
                    bindingChanges.Add(new SubjectRoomBindingChange(
                        subjId, subjectNames.GetValueOrDefault(subjId, "?"), LessonType.Lab,
                        add.Select(rid => roomLabelById[rid]).OrderBy(x => x).ToList()));
                    bindingApply.Add((subjId, LessonType.Lab, add));
                }
            }
        }

        if (targets.StudyPlans)
        {
            var studyPlans = await StudyPlanQ.BaseQuery(db)
                .Where(sp => sp.AcademicYear == schedule.AcademicYear && sp.Term == schedule.Term)
                .ToListAsync(ct);

            // entries indexed by the group that attends them
            var entriesByGroup = new Dictionary<Guid, List<ScheduleEntry>>();
            foreach (var e in entries)
                foreach (var sg in e.StudentGroups)
                {
                    if (!entriesByGroup.TryGetValue(sg.StudentGroupId, out var list))
                        entriesByGroup[sg.StudentGroupId] = list = new();
                    list.Add(e);
                }

            foreach (var sp in studyPlans)
            {
                int weeks = StudyPlanQ.StudyWeeksFromPlan(sp.CalendarPlan);

                // Hours are per-student, so we measure each group separately and take the fullest
                // group as the plan figure - never the sum across groups (labs/practicals are taught
                // group-by-group, which would multiply the curriculum hours by the group count).
                var computed = new Dictionary<(Guid Subj, LessonType Lt), double>();
                foreach (var g in sp.Groups)
                {
                    if (!entriesByGroup.TryGetValue(g.StudentGroupId, out var ge)) continue;
                    var perGroup = ge
                        .GroupBy(e => (e.SubjectId, e.LessonType))
                        .ToDictionary(
                            grp => grp.Key,
                            // Distinct weekly slots: parallel siblings / subgroups share a slot and
                            // collapse here. Both = 1 pair/week, Odd|Even = 0.5 - the plan-progress metric.
                            grp => grp.Select(e => (e.DayOfWeek, e.PairNumber, e.WeekType)).Distinct()
                                      .Sum(s => s.WeekType == WeekType.Both ? 1.0 : 0.5));
                    foreach (var kv in perGroup)
                        computed[kv.Key] = Math.Max(computed.GetValueOrDefault(kv.Key), kv.Value * weeks * HoursPerPair);
                }
                if (computed.Count == 0) continue;

                var entryBySubj = sp.Entries.ToDictionary(en => en.SubjectId);
                var changes = new List<StudyPlanHourChangeDto>();
                foreach (var kv in computed)
                {
                    var (subjId, lt) = kv.Key;
                    var (field, label) = FieldFor(lt);
                    if (field.Length == 0 || kv.Value <= 0) continue;
                    double current = entryBySubj.TryGetValue(subjId, out var en) ? GetField(en, lt) : 0;
                    if (Math.Abs(kv.Value - current) < 0.01) continue; // already matches the schedule
                    changes.Add(new StudyPlanHourChangeDto(
                        subjId, subjectNames.GetValueOrDefault(subjId, "?"), field, label, current, kv.Value));
                }
                if (changes.Count == 0) continue;
                changes = changes.OrderBy(c => c.SubjectName).ThenBy(c => c.FieldLabel).ToList();
                planChanges.Add(new StudyPlanBackfillChange(sp.Id, sp.Name, changes));
                planApply.Add((sp, changes));
            }
        }

        int roomsUpdated = 0, teacherLinks = 0, planFields = 0, bindingsAdded = 0;
        if (apply)
        {
            foreach (var (room, add) in roomApply)
            {
                room.AllowedLessonTypes = room.AllowedLessonTypes.Concat(add).Distinct().ToList();
                roomsUpdated++;
            }
            foreach (var (subjId, lt, addRoomIds) in bindingApply)
                foreach (var rid in addRoomIds)
                {
                    db.SubjectRoomBindings.Add(new SubjectRoomBinding { SubjectId = subjId, LessonType = lt, RoomId = rid });
                    bindingsAdded++;
                }
            foreach (var (teacher, add) in teacherApply)
                foreach (var (subjId, lt) in add)
                {
                    db.TeacherSubjects.Add(new TeacherSubject { TeacherId = teacher.Id, SubjectId = subjId, LessonType = lt });
                    teacherLinks++;
                }
            foreach (var (sp, changes) in planApply)
            {
                var entryBySubj = sp.Entries.ToDictionary(en => en.SubjectId);
                foreach (var c in changes)
                {
                    if (!entryBySubj.TryGetValue(c.SubjectId, out var en))
                    {
                        en = new StudyPlanEntry { StudyPlanId = sp.Id, SubjectId = c.SubjectId };
                        db.StudyPlanEntries.Add(en);
                        entryBySubj[c.SubjectId] = en;
                    }
                    SetField(en, c.Field, c.NewHours);
                    planFields++;
                }
            }
            await db.SaveChangesAsync(ct);
        }

        return (
            new BackfillPreviewDto(roomChanges, teacherChanges, planChanges, bindingChanges),
            new BackfillResultDto(roomsUpdated, teacherLinks, planFields, bindingsAdded));
    }

    private static (string Field, string Label) FieldFor(LessonType lt) => lt switch
    {
        LessonType.Lecture           => ("LectureHours",           "Лекции"),
        LessonType.Practical         => ("PracticalHours",         "Практики"),
        LessonType.Lab               => ("LabHours",               "Лабораторные"),
        LessonType.Seminar           => ("SeminarHours",           "Семинары"),
        LessonType.Language          => ("LanguageHours",          "Язык"),
        LessonType.PhysicalEducation => ("PhysicalEducationHours", "Физкультура"),
        _                            => ("", "")
    };

    private static double GetField(StudyPlanEntry e, LessonType lt) => lt switch
    {
        LessonType.Lecture           => e.LectureHours,
        LessonType.Practical         => e.PracticalHours,
        LessonType.Lab               => e.LabHours,
        LessonType.Seminar           => e.SeminarHours,
        LessonType.Language          => e.LanguageHours,
        LessonType.PhysicalEducation => e.PhysicalEducationHours,
        _                            => 0
    };

    private static void SetField(StudyPlanEntry e, string field, double value)
    {
        switch (field)
        {
            case "LectureHours":           e.LectureHours           = value; break;
            case "PracticalHours":         e.PracticalHours         = value; break;
            case "LabHours":               e.LabHours               = value; break;
            case "SeminarHours":           e.SeminarHours           = value; break;
            case "LanguageHours":          e.LanguageHours          = value; break;
            case "PhysicalEducationHours": e.PhysicalEducationHours = value; break;
        }
    }
}
