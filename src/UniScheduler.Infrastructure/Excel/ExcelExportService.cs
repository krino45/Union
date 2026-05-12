using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Excel;

public class ExcelExportService : IExcelExportService
{
    private readonly IApplicationDbContext _db;

    private static readonly string[] DayNames = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };
    private static readonly string[] PairTimes = { "08:00-09:30", "09:40-11:10", "11:20-12:50", "13:00-14:30", "14:40-16:10", "16:20-17:50" };
    private static readonly string[] WeekTypeLabels = { "Числитель", "Знаменатель" };

    public ExcelExportService(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> ExportScheduleAsync(Guid scheduleId, Guid? groupId, Guid? teacherId, CancellationToken cancellationToken = default)
    {
        var entries = await _db.ScheduleEntries
            .Include(e => e.Subject)
            .Include(e => e.Teacher)
            .Include(e => e.Room).ThenInclude(r => r!.Building)
            .Include(e => e.StudentGroups).ThenInclude(sg => sg.StudentGroup)
            .Where(e => e.ScheduleId == scheduleId)
            .ToListAsync(cancellationToken);

        if (groupId.HasValue)
            entries = entries.Where(e => e.StudentGroups.Any(sg => sg.StudentGroupId == groupId)).ToList();
        if (teacherId.HasValue)
            entries = entries.Where(e => e.TeacherId == teacherId).ToList();

        using var workbook = new XLWorkbook();

        if (groupId.HasValue)
        {
            var group = await _db.StudentGroups.FindAsync(new object[] { groupId.Value }, cancellationToken);
            AddGroupSheet(workbook, entries, group?.Name ?? "Группа");
        }
        else if (teacherId.HasValue)
        {
            var teacher = await _db.Teachers.FindAsync(new object[] { teacherId.Value }, cancellationToken);
            AddTeacherSheet(workbook, entries, teacher?.LastName ?? "Преподаватель");
        }
        else
        {
            // Export all groups
            var groups = await _db.StudentGroups.OrderBy(g => g.Name).ToListAsync(cancellationToken);
            foreach (var g in groups)
            {
                var groupEntries = entries.Where(e => e.StudentGroups.Any(sg => sg.StudentGroupId == g.Id)).ToList();
                AddGroupSheet(workbook, groupEntries, g.Name);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddGroupSheet(XLWorkbook wb, List<Domain.Entities.ScheduleEntry> entries, string sheetName)
    {
        var ws = wb.AddWorksheet(sheetName.Length > 31 ? sheetName[..31] : sheetName);
        BuildGrid(ws, entries, isGroupView: true);
    }

    private void AddTeacherSheet(XLWorkbook wb, List<Domain.Entities.ScheduleEntry> entries, string sheetName)
    {
        var ws = wb.AddWorksheet(sheetName.Length > 31 ? sheetName[..31] : sheetName);
        BuildGrid(ws, entries, isGroupView: false);
    }

    private static void BuildGrid(IXLWorksheet ws, List<Domain.Entities.ScheduleEntry> entries, bool isGroupView)
    {
        // Header row
        ws.Cell(1, 1).Value = "Пара / Время";
        for (int d = 0; d < 6; d++)
            ws.Cell(1, d + 2).Value = DayNames[d];

        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Pair rows
        for (int p = 0; p < 6; p++)
        {
            int baseRow = 2 + p * 3; // 3 rows per pair (one per numerator, denominator, separator)
            ws.Cell(baseRow, 1).Value = $"{p + 1}\n{PairTimes[p]}";
            ws.Cell(baseRow, 1).Style.Alignment.WrapText = true;

            for (int d = 0; d < 6; d++)
            {
                var day = (RussianDayOfWeek)(d + 1);
                var dayEntries = entries.Where(e => e.DayOfWeek == day && e.PairNumber == p + 1).ToList();

                var numerator = dayEntries.Where(e => e.WeekType == WeekType.Numerator || e.WeekType == WeekType.Both).FirstOrDefault();
                var denominator = dayEntries.Where(e => e.WeekType == WeekType.Denominator || e.WeekType == WeekType.Both).FirstOrDefault();

                string numText = FormatEntry(numerator, isGroupView);
                string denText = denominator != numerator ? FormatEntry(denominator, isGroupView) : "";

                var cell = ws.Cell(baseRow, d + 2);
                if (!string.IsNullOrEmpty(denText) && numText != denText)
                    cell.Value = $"Ч: {numText}\nЗ: {denText}";
                else
                    cell.Value = numText;

                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            }
        }

        // Auto-size columns
        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 14;
        for (int d = 2; d <= 7; d++) ws.Column(d).Width = 22;

        // Borders
        var range = ws.Range(1, 1, 2 + 6 * 3, 7);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static string FormatEntry(Domain.Entities.ScheduleEntry? e, bool isGroupView)
    {
        if (e == null) return "";
        var room = e.IsOnline ? "Дист." : (e.Room != null ? $"{e.Room.Building?.ShortCode ?? ""}-{e.Room.Number}" : "");
        if (isGroupView)
            return $"{e.Subject.ShortName}\n{e.Teacher.LastName} {e.Teacher.FirstName[0]}.{e.Teacher.MiddleName[0]}.\n{room}";
        else
            return $"{e.Subject.ShortName}\n{string.Join(", ", e.StudentGroups.Select(sg => sg.StudentGroup.Name))}\n{room}";
    }
}
