using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Excel;

public class ExcelImportService : IExcelImportService
{
    private readonly IApplicationDbContext _db;

    private static readonly string[] DayHeaders =
        { "понедельник", "вторник", "среда", "четверг", "пятница", "суббота" };

    public ExcelImportService(IApplicationDbContext db) => _db = db;

    public async Task<ImportPreviewDto> ParseAsync(Stream stream, Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var preview = new ImportPreviewDto();
        using var workbook = new XLWorkbook(stream);

        var teachers = await _db.Teachers.ToListAsync(cancellationToken);
        var rooms = await _db.Rooms.Include(r => r.Building).ToListAsync(cancellationToken);
        var subjects = await _db.Subjects.ToListAsync(cancellationToken);
        var groups = await _db.StudentGroups.ToListAsync(cancellationToken);

        foreach (var ws in workbook.Worksheets)
        {
            var groupName = ws.Name.Trim();
            var group = groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                preview.Warnings.Add($"Sheet '{groupName}': No matching student group found — skipping.");
                continue;
            }

            // Find header row (row 1), days in columns 2-7
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            for (int pairRow = 2; pairRow <= lastRow; pairRow++)
            {
                var pairCell = ws.Cell(pairRow, 1).GetString().Trim();
                if (!int.TryParse(pairCell.Split('/')[0].Trim(), out int pairNumber) || pairNumber < 1 || pairNumber > 7)
                    continue;

                for (int col = 2; col <= 7; col++)
                {
                    var dayOfWeek = (RussianDayOfWeek)(col - 1);
                    var cellText = ws.Cell(pairRow, col).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(cellText)) continue;

                    // Parse cell: may contain "Ч: ...\nЗ: ..." or just single entry
                    var lines = cellText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    if (lines.Length >= 2 && lines[0].StartsWith("Ч:") && lines.Any(l => l.StartsWith("З:")))
                    {
                        // Alternating weeks
                        var numText = string.Join(" ", lines.TakeWhile(l => !l.StartsWith("З:"))).Replace("Ч:", "").Trim();
                        var denText = string.Join(" ", lines.SkipWhile(l => !l.StartsWith("З:"))).Replace("З:", "").Trim();
                        TryParseEntry(numText, groupName, dayOfWeek, pairNumber, WeekType.Numerator, teachers, rooms, subjects, preview, pairRow, col);
                        TryParseEntry(denText, groupName, dayOfWeek, pairNumber, WeekType.Denominator, teachers, rooms, subjects, preview, pairRow, col);
                    }
                    else
                    {
                        TryParseEntry(cellText, groupName, dayOfWeek, pairNumber, WeekType.Both, teachers, rooms, subjects, preview, pairRow, col);
                    }
                }
            }
        }

        return preview;
    }

    private static void TryParseEntry(
        string text, string groupName,
        RussianDayOfWeek day, int pair, WeekType weekType,
        List<Domain.Entities.Teacher> teachers,
        List<Domain.Entities.Room> rooms,
        List<Domain.Entities.Subject> subjects,
        ImportPreviewDto preview, int row, int col)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
        if (lines.Length < 2)
        {
            preview.Errors.Add(new ImportErrorDto(row, col, $"Cannot parse cell: '{text}'"));
            return;
        }

        var subjectName = lines[0];
        var teacherName = lines.Length >= 2 ? lines[1] : "";
        var roomNumber = lines.Length >= 3 ? lines[2] : "";

        var subject = subjects.FirstOrDefault(s =>
            string.Equals(s.Name, subjectName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.ShortName, subjectName, StringComparison.OrdinalIgnoreCase));

        var teacher = teachers.FirstOrDefault(t =>
            t.LastName.Equals(teacherName.Split(' ')[0], StringComparison.OrdinalIgnoreCase));

        if (subject == null)
            preview.Warnings.Add($"Row {row}, Col {col}: Subject '{subjectName}' not found.");
        if (teacher == null)
            preview.Warnings.Add($"Row {row}, Col {col}: Teacher '{teacherName}' not found.");

        preview.ValidEntries.Add(new ImportEntryDto(
            groupName, subjectName, teacherName, roomNumber,
            day, pair, weekType, LessonType.Lecture));
    }

    public async Task<int> CommitAsync(ImportPreviewDto preview, Guid scheduleId, CancellationToken cancellationToken = default)
    {
        if (preview.HasErrors) throw new InvalidOperationException("Cannot commit import with errors.");

        var teachers = await _db.Teachers.ToListAsync(cancellationToken);
        var rooms = await _db.Rooms.Include(r => r.Building).ToListAsync(cancellationToken);
        var subjects = await _db.Subjects.ToListAsync(cancellationToken);
        var groups = await _db.StudentGroups.ToListAsync(cancellationToken);

        int committed = 0;
        foreach (var entry in preview.ValidEntries)
        {
            var subject = subjects.FirstOrDefault(s =>
                string.Equals(s.Name, entry.SubjectName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.ShortName, entry.SubjectName, StringComparison.OrdinalIgnoreCase));
            var teacher = teachers.FirstOrDefault(t =>
                t.LastName.Equals(entry.TeacherName.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
            var room = rooms.FirstOrDefault(r => r.Number == entry.RoomNumber ||
                $"{r.Building?.ShortCode}-{r.Number}" == entry.RoomNumber);
            var group = groups.FirstOrDefault(g => string.Equals(g.Name, entry.GroupName, StringComparison.OrdinalIgnoreCase));

            if (subject == null || teacher == null || group == null) continue;

            var schedEntry = new Domain.Entities.ScheduleEntry
            {
                ScheduleId = scheduleId,
                SubjectId = subject.Id,
                TeacherId = teacher.Id,
                RoomId = room?.Id,
                DayOfWeek = entry.DayOfWeek,
                PairNumber = entry.PairNumber,
                WeekType = entry.WeekType,
                LessonType = entry.LessonType,
                IsOnline = room == null
            };
            _db.ScheduleEntries.Add(schedEntry);
            _db.ScheduleEntryStudentGroups.Add(new Domain.Entities.ScheduleEntryStudentGroup
            {
                ScheduleEntry = schedEntry,
                StudentGroupId = group.Id
            });
            committed++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return committed;
    }
}
