using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record JsonEntryImport(
    string SubjectShortName,
    string TeacherLastName,
    string TeacherFirstName,
    List<string> GroupNames,
    string? BuildingShortCode,
    string? RoomNumber,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    WeekType WeekType,
    LessonType LessonType,
    bool IsOnline
);

public record ImportFromJsonCommand(
    Guid ScheduleId,
    bool Replace,
    List<JsonEntryImport> Entries
) : IRequest<ImportFromJsonResult>;

public record ImportFromJsonResult(int Committed, List<string> Errors);

public class ImportFromJsonCommandHandler : IRequestHandler<ImportFromJsonCommand, ImportFromJsonResult>
{
    private readonly IApplicationDbContext _db;
    public ImportFromJsonCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ImportFromJsonResult> Handle(ImportFromJsonCommand req, CancellationToken ct)
    {
        var errors = new List<string>();

        var subjects = await _db.Subjects.ToListAsync(ct);
        var teachers = await _db.Teachers.ToListAsync(ct);
        var groups   = (await _db.StudentGroups.Include(g => g.Faculty).ToListAsync(ct)).ToList();
        var rooms    = await _db.Rooms.Include(r => r.Building).ToListAsync(ct);

        if (req.Replace)
        {
            var existing = await _db.ScheduleEntries
                .Where(e => e.ScheduleId == req.ScheduleId).ToListAsync(ct);
            _db.ScheduleEntries.RemoveRange(existing);
        }

        // Collect all group names referenced in this import
        var allGroupNames = req.Entries.SelectMany(e => e.GroupNames).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Auto-create missing groups only in replace mode
        if (req.Replace)
        {
            var defaultFaculty = await _db.Faculties.FirstOrDefaultAsync(ct);
            foreach (var gname in allGroupNames)
            {
                var existing = groups.FirstOrDefault(g => g.Name.Equals(gname, StringComparison.OrdinalIgnoreCase));
                if (existing != null) continue;
                if (defaultFaculty == null)
                {
                    errors.Add($"Группа \"{gname}\" не найдена и не может быть создана: нет факультетов в базе.");
                    continue;
                }
                var newGroup = new StudentGroup
                {
                    Name = gname, Year = 1, Specialty = "Импорт",
                    StudentCount = 0, FacultyId = defaultFaculty.Id
                };
                _db.StudentGroups.Add(newGroup);
                groups.Add(newGroup); // update in-memory list for resolution below
                errors.Add($"Группа \"{gname}\" создана автоматически (placeholder) — уточните данные.");
            }
            await _db.SaveChangesAsync(ct); // flush new groups so IDs are set
        }

        int committed = 0;
        foreach (var (item, idx) in req.Entries.Select((x, i) => (x, i + 1)))
        {
            var subject = subjects.FirstOrDefault(s =>
                s.ShortName.Equals(item.SubjectShortName, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Equals(item.SubjectShortName, StringComparison.OrdinalIgnoreCase));
            if (subject == null) { errors.Add($"[{idx}] Предмет не найден: \"{item.SubjectShortName}\""); continue; }

            var teacher = teachers.FirstOrDefault(t =>
                t.LastName.Equals(item.TeacherLastName, StringComparison.OrdinalIgnoreCase) &&
                t.FirstName.StartsWith(item.TeacherFirstName, StringComparison.OrdinalIgnoreCase));
            if (teacher == null) { errors.Add($"[{idx}] Преподаватель не найден: \"{item.TeacherLastName} {item.TeacherFirstName}\""); continue; }

            var resolvedGroups = new List<StudentGroup>();
            bool groupFail = false;
            foreach (var gname in item.GroupNames)
            {
                var g = groups.FirstOrDefault(x => x.Name.Equals(gname, StringComparison.OrdinalIgnoreCase));
                if (g == null) { errors.Add($"[{idx}] Группа не найдена: \"{gname}\""); groupFail = true; }
                else resolvedGroups.Add(g);
            }
            if (groupFail) continue;

            Room? room = null;
            if (!item.IsOnline && !string.IsNullOrWhiteSpace(item.RoomNumber))
            {
                room = rooms.FirstOrDefault(r =>
                    r.Number == item.RoomNumber &&
                    (item.BuildingShortCode == null || (r.Building?.ShortCode ?? "") == item.BuildingShortCode));
                if (room == null)
                    errors.Add($"[{idx}] Аудитория \"{item.BuildingShortCode}-{item.RoomNumber}\" не найдена — занятие добавлено без аудитории");
            }

            var entry = new ScheduleEntry
            {
                ScheduleId = req.ScheduleId, SubjectId = subject.Id, TeacherId = teacher.Id,
                RoomId = room?.Id, DayOfWeek = item.DayOfWeek, PairNumber = item.PairNumber,
                WeekType = item.WeekType, LessonType = item.LessonType, IsOnline = item.IsOnline
            };
            _db.ScheduleEntries.Add(entry);
            foreach (var g in resolvedGroups)
                _db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup { ScheduleEntry = entry, StudentGroupId = g.Id });
            committed++;
        }

        var schedule = await _db.Schedules.FindAsync(new object[] { req.ScheduleId }, ct);
        if (schedule != null) schedule.Status = ScheduleStatus.Draft;

        await _db.SaveChangesAsync(ct);
        return new ImportFromJsonResult(committed, errors);
    }
}
