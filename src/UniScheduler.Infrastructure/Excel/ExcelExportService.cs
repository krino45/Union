using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Excel;

public class ExcelExportService : IExcelExportService
{
    private readonly IApplicationDbContext _db;

    private static readonly string[] DayNames = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };
    private static readonly string[] PairTimes = { "08:00–09:35", "09:50–11:25", "11:40–13:15", "13:45–15:20", "15:35–17:10", "17:25–19:00", "19:15–20:50" };

    // Very subtle row tints
    private static readonly XLColor OddRowBg  = XLColor.FromArgb(0xF2, 0xF7, 0xFF); // pale blue
    private static readonly XLColor EvenRowBg = XLColor.FromArgb(0xF2, 0xFF, 0xF4); // pale green

    public ExcelExportService(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> ExportScheduleAsync(Guid scheduleId, Guid? groupId, Guid? teacherId, CancellationToken ct = default)
    {
        var entries = await _db.ScheduleEntries
            .Include(e => e.Subject)
            .Include(e => e.Teacher)
            .Include(e => e.Room).ThenInclude(r => r!.Building)
            .Include(e => e.StudentGroups).ThenInclude(sg => sg.StudentGroup)
            .Where(e => e.ScheduleId == scheduleId)
            .ToListAsync(ct);

        if (groupId.HasValue)
            entries = entries.Where(e => e.StudentGroups.Any(sg => sg.StudentGroupId == groupId)).ToList();
        if (teacherId.HasValue)
            entries = entries.Where(e => e.TeacherId == teacherId).ToList();

        using var wb = new XLWorkbook();

        if (groupId.HasValue)
        {
            var group = await _db.StudentGroups.FindAsync(new object[] { groupId.Value }, ct);
            BuildGrid(wb.AddWorksheet(Truncate(group?.Name ?? "Группа")), entries, isGroupView: true);
        }
        else if (teacherId.HasValue)
        {
            var teacher = await _db.Teachers.FindAsync(new object[] { teacherId.Value }, ct);
            BuildGrid(wb.AddWorksheet(Truncate(teacher?.LastName ?? "Препод.")), entries, isGroupView: false);
        }
        else
        {
            var groups = await _db.StudentGroups.OrderBy(g => g.Name).ToListAsync(ct);
            foreach (var g in groups)
            {
                var ge = entries.Where(e => e.StudentGroups.Any(sg => sg.StudentGroupId == g.Id)).ToList();
                BuildGrid(wb.AddWorksheet(Truncate(g.Name)), ge, isGroupView: true);
            }
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    //  Grid builder 

    private static void BuildGrid(IXLWorksheet ws, List<Domain.Entities.ScheduleEntry> entries, bool isGroupView)
    {
        const int NumPairs = 7;
        const int NumDays  = 6;
        int lastRow = 1 + NumPairs * 2; // header + 7 pairs × 2 rows

        //  Header 
        ws.Cell(1, 1).Value = "Пара / Время";
        for (int d = 0; d < NumDays; d++)
            ws.Cell(1, d + 2).Value = DayNames[d];

        var headerRange = ws.Range(1, 1, 1, NumDays + 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xD6, 0xE4, 0xF7);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        //  Pair rows 
        for (int p = 0; p < NumPairs; p++)
        {
            int oddRow  = 2 + p * 2;
            int evenRow = 3 + p * 2;

            // Row tints
            ws.Range(oddRow,  1, oddRow,  NumDays + 1).Style.Fill.BackgroundColor = OddRowBg;
            ws.Range(evenRow, 1, evenRow, NumDays + 1).Style.Fill.BackgroundColor = EvenRowBg;

            // Pair label - always spans both rows
            var labelCell = ws.Range(oddRow, 1, evenRow, 1);
            labelCell.Merge();
            var lc = labelCell.FirstCell();
            lc.Value = $"{p + 1}\n{PairTimes[p]}";
            lc.Style.Alignment.WrapText = true;
            lc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            lc.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            lc.Style.Font.Bold = true;

            // Day columns
            for (int d = 0; d < NumDays; d++)
            {
                var day = (RussianDayOfWeek)(d + 1);
                var dayEntries = entries.Where(e => e.DayOfWeek == day && e.PairNumber == p + 1).ToList();

                var oddList  = dayEntries.Where(e => e.WeekType is WeekType.Odd  or WeekType.Both).ToList();
                var evenList = dayEntries.Where(e => e.WeekType is WeekType.Even or WeekType.Both).ToList();

                string oddText  = FormatCellEntries(oddList,  isGroupView);
                string evenText = FormatCellEntries(evenList, isGroupView);

                int col = d + 2;

                if (oddText == evenText)
                {
                    // Same content → merge the two rows
                    var merged = ws.Range(oddRow, col, evenRow, col);
                    merged.Merge();
                    var mc = merged.FirstCell();
                    mc.Value = oddText;
                    mc.Style.Alignment.WrapText = true;
                    mc.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                    mc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    // Clear both row tints so merged cell gets neutral bg
                    if (!string.IsNullOrEmpty(oddText))
                        mc.Style.Fill.BackgroundColor = XLColor.FromArgb(0xEB, 0xF3, 0xFF);
                }
                else
                {
                    var oc = ws.Cell(oddRow,  col);
                    var ec = ws.Cell(evenRow, col);

                    oc.Value = oddText;
                    ec.Value = evenText;

                    foreach (var c in new[] { oc, ec })
                    {
                        c.Style.Alignment.WrapText   = true;
                        c.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Top;
                        c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    }
                }
            }
        }

        //  Column widths 
        ws.Column(1).Width = 13;
        for (int d = 2; d <= NumDays + 1; d++) ws.Column(d).Width = 23;
        ws.Rows().AdjustToContents(6, 60);

        //  Borders 
        ApplyBorders(ws, lastRow, NumDays + 1);
    }

    private static void ApplyBorders(IXLWorksheet ws, int lastRow, int lastCol)
    {
        // 1. Thin borders everywhere inside the data area
        var all = ws.Range(1, 1, lastRow, lastCol);
        all.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        all.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        // 2. Medium line below header
        ws.Range(1, 1, 1, lastCol).Style.Border.BottomBorder = XLBorderStyleValues.Medium;

        // 3. Medium line at the top of each pair (every odd row from row 2 onward)
        for (int p = 0; p < 7; p++)
        {
            int oddRow = 2 + p * 2;
            ws.Range(oddRow, 1, oddRow, lastCol).Style.Border.TopBorder = XLBorderStyleValues.Medium;
        }
    }

    //  Formatting helpers 

    private static string FormatCellEntries(List<Domain.Entities.ScheduleEntry> list, bool isGroupView)
    {
        if (list.Count == 0) return "";
        // Deduplicate by entry ID (Both entries appear in both odd and even lists)
        var distinct = list.DistinctBy(e => e.Id).ToList();
        return string.Join("\n\n", distinct.Select(e => FormatEntry(e, isGroupView)));
    }

    private static string FormatEntry(Domain.Entities.ScheduleEntry e, bool isGroupView)
    {
        var room = e.IsOnline ? "Дист." : (e.Room != null
            ? $"{e.Room.Building?.ShortCode ?? ""}-{e.Room.Number}"
            : "");
        var lt = LtLabel(e.LessonType);
        return isGroupView
            ? $"[{lt}] {e.Subject.ShortName}\n{e.Teacher.LastName} {e.Teacher.FirstName[0]}.{(e.Teacher.MiddleName?.Length > 0 ? e.Teacher.MiddleName[0] + "." : "")}\n{room}"
            : $"[{lt}] {e.Subject.ShortName}\n{string.Join(", ", e.StudentGroups.Select(sg => sg.StudentGroup.Name))}\n{room}";
    }

    private static string LtLabel(LessonType lt) => lt switch
    {
        LessonType.Lecture   => "Лек",
        LessonType.Practical => "Пр",
        LessonType.Lab       => "Лаб",
        LessonType.Seminar   => "Сем",
        _                    => "?"
    };

    private static string Truncate(string s) => s.Length > 31 ? s[..31] : s;
}
