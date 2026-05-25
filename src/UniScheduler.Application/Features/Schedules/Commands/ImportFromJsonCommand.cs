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

// Imports a real-university schedule from the export JSON. Anything that doesn't
// exist yet (teacher, building, room, etc.) is auto-created as a placeholder
public class ImportFromJsonCommandHandler : IRequestHandler<ImportFromJsonCommand, ImportFromJsonResult>
{
    private readonly IApplicationDbContext _db;
    public ImportFromJsonCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ImportFromJsonResult> Handle(ImportFromJsonCommand req, CancellationToken ct)
    {
        var notes = new List<string>();

        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == req.ScheduleId, ct);
        if (schedule == null)
            return new ImportFromJsonResult(0, new List<string> { "Расписание не найдено." });

        var subjects   = await _db.Subjects.ToListAsync(ct);
        var teachers   = await _db.Teachers.ToListAsync(ct);
        var faculties  = await _db.Faculties.ToListAsync(ct);
        var buildings  = await _db.Buildings.ToListAsync(ct);
        var rooms      = (await _db.Rooms.Include(r => r.Building).ToListAsync(ct))
                         .Where(r => r.Building != null).ToList();
        var groups     = (await _db.StudentGroups.Include(g => g.Faculty).ToListAsync(ct))
                         .Where(g => g.Faculty != null).ToList();

        if (req.Replace)
        {
            var existing = await _db.ScheduleEntries
                .Where(e => e.ScheduleId == req.ScheduleId).ToListAsync(ct);
            if (existing.Count > 0)
            {
                var existingIds = existing.Select(e => e.Id).ToList();
                var relatedRequests = await _db.RescheduleRequests
                    .Where(r => existingIds.Contains(r.OriginalEntryId))
                    .ToListAsync(ct);
                _db.RescheduleRequests.RemoveRange(relatedRequests);
            }
            _db.ScheduleEntries.RemoveRange(existing);
        }

        // Counters for the summary line.
        int newSubjects = 0, newTeachers = 0, newBuildings = 0, newRooms = 0, newGroups = 0, newFaculties = 0;

        // A faculty is required to create a group; reuse the schedule's faculty, else the first one,
        // else mint a placeholder.
        Faculty? PlaceholderFaculty()
        {
            var fac = schedule.FacultyId.HasValue
                ? faculties.FirstOrDefault(f => f.Id == schedule.FacultyId.Value)
                : faculties.FirstOrDefault();
            if (fac != null) return fac;

            fac = new Faculty { Name = "Импорт", ShortCode = "IMP" };
            _db.Faculties.Add(fac);
            faculties.Add(fac);
            newFaculties++;
            return fac;
        }

        Subject ResolveSubject(string name)
        {
            var found = subjects.FirstOrDefault(s =>
                s.ShortName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;

            found = new Subject
            {
                Name = name,
                ShortName = name,
                AcademicYear = schedule.AcademicYear,
                Term = schedule.Term
            };
            _db.Subjects.Add(found);
            subjects.Add(found);
            newSubjects++;
            return found;
        }

        Teacher ResolveTeacher(string lastName, string firstName)
        {
            var found = teachers.FirstOrDefault(t =>
                t.LastName.Equals(lastName, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(firstName) || t.FirstName.StartsWith(firstName, StringComparison.OrdinalIgnoreCase)));
            if (found != null) return found;

            found = new Teacher { LastName = lastName, FirstName = firstName, MiddleName = "", Email = $"import-{Guid.NewGuid():N}@placeholder" };
            _db.Teachers.Add(found);
            teachers.Add(found);
            newTeachers++;
            return found;
        }

        Building ResolveBuilding(string shortCode)
        {
            var found = buildings.FirstOrDefault(b =>
                b.ShortCode.Equals(shortCode, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;

            found = new Building { ShortCode = shortCode, Address = "—", NumberOfFloors = 5 };
            _db.Buildings.Add(found);
            buildings.Add(found);
            newBuildings++;
            return found;
        }

        Room ResolveRoom(string number, Building building)
        {
            var found = rooms.FirstOrDefault(r =>
                r.Number.Equals(number, StringComparison.OrdinalIgnoreCase) &&
                ((building.Id != Guid.Empty && r.BuildingId == building.Id) || ReferenceEquals(r.Building, building)));
            if (found != null) return found;

            found = new Room { Building = building, Number = number, Capacity = 0, RoomType = RoomType.RegularCabinet, Floor = 1 };
            _db.Rooms.Add(found);
            rooms.Add(found);
            newRooms++;
            return found;
        }

        StudentGroup ResolveGroup(string name)
        {
            var found = groups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;

            var fac = PlaceholderFaculty()!;
            found = new StudentGroup { Name = name, Year = 1, Specialty = "Импорт", StudentCount = 0, Faculty = fac };
            _db.StudentGroups.Add(found);
            groups.Add(found);
            newGroups++;
            return found;
        }

        int committed = 0;
        foreach (var (item, idx) in req.Entries.Select((x, i) => (x, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(item.SubjectShortName))
            { notes.Add($"[{idx}] Пропущено: не указан предмет."); continue; }
            if (string.IsNullOrWhiteSpace(item.TeacherLastName))
            { notes.Add($"[{idx}] Пропущено: не указан преподаватель."); continue; }

            var subject = ResolveSubject(item.SubjectShortName.Trim());
            var teacher = ResolveTeacher(item.TeacherLastName.Trim(), (item.TeacherFirstName ?? "").Trim());

            var resolvedGroups = item.GroupNames
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => ResolveGroup(g.Trim()))
                .ToList();

            Room? room = null;
            if (!item.IsOnline && !string.IsNullOrWhiteSpace(item.RoomNumber))
            {
                if (!string.IsNullOrWhiteSpace(item.BuildingShortCode))
                {
                    var building = ResolveBuilding(item.BuildingShortCode.Trim());
                    room = ResolveRoom(item.RoomNumber.Trim(), building);
                }
                else
                {
                    // No building code: only reuse an existing room with that number, never invent one
                    // (we wouldn't know which building it belongs to).
                    room = rooms.FirstOrDefault(r => r.Number.Equals(item.RoomNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (room == null)
                        notes.Add($"[{idx}] Аудитория \"{item.RoomNumber}\" без кода корпуса — занятие добавлено без аудитории.");
                }
            }

            var entry = new ScheduleEntry
            {
                ScheduleId = req.ScheduleId,
                Subject = subject,
                Teacher = teacher,
                Room = room,
                DayOfWeek = item.DayOfWeek,
                PairNumber = item.PairNumber,
                WeekType = item.WeekType,
                LessonType = item.LessonType,
                IsOnline = item.IsOnline
            };
            _db.ScheduleEntries.Add(entry);
            foreach (var g in resolvedGroups)
                _db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup { ScheduleEntry = entry, StudentGroup = g });
            committed++;
        }

        schedule.Status = ScheduleStatus.Draft;
        await _db.SaveChangesAsync(ct);

        var created = new List<string>();
        if (newSubjects  > 0) created.Add($"предметов: {newSubjects}");
        if (newTeachers  > 0) created.Add($"преподавателей: {newTeachers}");
        if (newBuildings > 0) created.Add($"корпусов: {newBuildings}");
        if (newRooms     > 0) created.Add($"аудиторий: {newRooms}");
        if (newGroups    > 0) created.Add($"групп: {newGroups}");
        if (newFaculties > 0) created.Add($"факультетов: {newFaculties}");
        if (created.Count > 0)
            notes.Insert(0, "Автоматически создано — " + string.Join(", ", created) + ". Проверьте и дополните данные.");

        return new ImportFromJsonResult(committed, notes);
    }
}
