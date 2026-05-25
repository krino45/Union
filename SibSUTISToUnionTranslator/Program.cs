using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

// Usage: SibSUTISToUnionTranslator [groups.json] [output.json]
//
// groups.json format: { "ИП211": "123", "ИП212": "456" }
//   Key = group name, value = ID.

var groupsPath = args.Length > 0 ? args[0] : "groups.json";
var outputPath = args.Length > 1 ? args[1] : "schedule.json";

if (!File.Exists(groupsPath))
{
    Console.WriteLine($"{groupsPath} not found — collecting groups from SibSUTIS API...");
    var collected = await CollectGroupsAsync();
    if (collected.Count == 0)
    {
        Console.Error.WriteLine("No groups found. Check connectivity to sibsutis.ru.");
        return 1;
    }
    var groupsJson = JsonSerializer.Serialize(collected, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(groupsPath, groupsJson, Encoding.UTF8);
    Console.WriteLine($"Saved {collected.Count} groups to {groupsPath}.");
}

var groupIds = JsonSerializer.Deserialize<Dictionary<string, string>>(
    await File.ReadAllTextAsync(groupsPath, Encoding.UTF8),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? [];

Console.WriteLine($"Loaded {groupIds.Count} groups.");

var knownGroupNames = new HashSet<string>(groupIds.Keys, StringComparer.OrdinalIgnoreCase);

// Key: canonical entry identity (ignores which week it was seen in)
// Value: (week1 seen, week2 seen, first-seen data)
var entryMap = new Dictionary<EntryKey, (bool w1, bool w2, EntryData d)>();

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true,
    Args = ["--no-sandbox", "--disable-dev-shm-usage"]
});
var page = await browser.NewPageAsync();

// SibSUTIS requires auth; credentials come from env vars SIBSUTIS_USER / SIBSUTIS_PASS.
var sibUser = Environment.GetEnvironmentVariable("SIBSUTIS_USER") ?? "";
var sibPass = Environment.GetEnvironmentVariable("SIBSUTIS_PASS") ?? "";

// Navigate once to trigger auth redirect if needed
var firstGroupId = groupIds.Values.FirstOrDefault() ?? "1";
await page.GotoAsync($"https://sibsutis.ru/students/schedule/?type=student&month=2&group={firstGroupId}");

if (page.Url.Contains("auth"))
{
    if (string.IsNullOrEmpty(sibUser) || string.IsNullOrEmpty(sibPass))
    {
        Console.Error.WriteLine("Site requires login. Set SIBSUTIS_USER and SIBSUTIS_PASS env vars.");
        return 1;
    }
    await page.FillAsync("input[name=USER_LOGIN]", sibUser);
    await page.FillAsync("input[name=USER_PASSWORD]", sibPass);
    await page.ClickAsync("input[name=Login]");
    await page.WaitForURLAsync("https://sibsutis.ru/students/schedule/**");
    Console.WriteLine("Logged in.");
}

foreach (var (groupName, groupId) in groupIds)
{
    Console.WriteLine($"Scraping group {groupName} (id={groupId})...");

    await page.GotoAsync($"https://sibsutis.ru/students/schedule/?type=student&month=2&group={groupId}");
    try
    {
        await page.WaitForSelectorAsync("a[class=calendar__cell]", new() { Timeout = 15_000 });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }
    catch
    {
        Console.Error.WriteLine($"  Timed out waiting for calendar — skipping.");
        continue;
    }

    var daysJson = await page.EvaluateAsync<string>("() => JSON.stringify(days)");
    string[]? days;
    try { days = JsonSerializer.Deserialize<string[]>(daysJson); }
    catch { Console.Error.WriteLine($"  Failed to parse days JSON — skipping."); continue; }

    if (days == null) continue;

    for (int i = 1; i < days.Length && i <= 14; i++)
    {
        if (string.IsNullOrWhiteSpace(days[i]) || days[i] == "null") continue;

        DaySchedule? day;
        try { day = JsonSerializer.Deserialize<DaySchedule>(days[i]); }
        catch { continue; }
        if (day?.ScheduleCell == null) continue;

        bool isWeek2 = i >= 8;
        int dayIndex = (i - 1) % 7 + 1; // 1=Mon … 7=Sun
        if (dayIndex > 6) continue;       // skip Sunday (RussianDayOfWeek only goes to Saturday)
        var dayOfWeek = (RussianDayOfWeek)dayIndex;

        foreach (var cell in day.ScheduleCell)
        {
            if (cell?.Subgroup == null || cell.Subgroup.Length == 0) continue;

            var pairNumber = TimeToPair(cell.DateBegin);
            if (pairNumber < 1) continue;

            foreach (var sub in cell.Subgroup)
            {
                if (sub.GROUP == null || sub.GROUP.Length == 0) continue;
                // Only process if at least one of the listed groups is one we're tracking
                var relevantGroups = sub.GROUP
                    .Where(g => knownGroupNames.Contains(g))
                    .OrderBy(g => g)
                    .ToArray();
                if (relevantGroups.Length == 0) continue;

                var (room, building, isOnline) = ParseClassroom(sub.CLASSROOM);
                var (lastName, firstName) = SplitTeacher(sub.TEACHER?.FirstOrDefault());
                var lessonType = MapLessonType(sub.TYPE_LESSON);

                var key = new EntryKey(
                    string.Join("|", relevantGroups),
                    dayOfWeek, pairNumber,
                    sub.DISCIPLINE ?? "",
                    lastName, firstName,
                    building ?? "", room ?? "", isOnline, lessonType);

                if (!entryMap.TryGetValue(key, out var existing))
                    existing = (false, false, new EntryData(relevantGroups, sub.DISCIPLINE ?? "", lastName, firstName,
                        building, room, isOnline, lessonType));

                entryMap[key] = isWeek2
                    ? (existing.w1, true, existing.d)
                    : (true, existing.w2, existing.d);
            }
        }
    }
}

var result = entryMap.Select(kvp =>
{
    var (w1, w2, d) = kvp.Value;
    var weekType = (w1, w2) switch
    {
        (true, true)  => WeekType.Both,
        (true, false) => WeekType.Odd,
        _             => WeekType.Even
    };
    return new JsonEntryImport(
        SubjectShortName: d.Discipline,
        TeacherLastName:  d.LastName,
        TeacherFirstName: d.FirstName,
        GroupNames:       d.Groups.ToList(),
        BuildingShortCode: d.BuildingShortCode,
        RoomNumber:       d.RoomNumber,
        DayOfWeek:        kvp.Key.DayOfWeek,
        PairNumber:       kvp.Key.PairNumber,
        WeekType:         weekType,
        LessonType:       d.LessonType,
        IsOnline:         d.IsOnline
    );
}).ToList();

var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
});
await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);
Console.WriteLine($"Done. {result.Count} entries written to {outputPath}.");
return 0;

//  helpers 

static async Task<Dictionary<string, string>> CollectGroupsAsync()
{
    // Iterate over Russian letters а-я plus digits 0-9.
    // The API returns matches for each prefix character; we union them all.
    const string chars = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789";

    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    using var http = new HttpClient();
    http.Timeout = TimeSpan.FromSeconds(15);

    foreach (var ch in chars)
    {
        var url = $"https://sibsutis.ru/ajax/get_groups_soap.php?search_group={Uri.EscapeDataString(ch.ToString())}";
        try
        {
            var body = await http.GetStringAsync(url);
            var resp = JsonSerializer.Deserialize<GroupSearchResponse>(body);
            if (resp?.Results == null) continue;
            foreach (var item in resp.Results)
                result[item.Text] = item.Id;
            await Task.Delay(TimeSpan.FromSeconds(0.5 + Random.Shared.NextDouble()));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Warning: query '{ch}' failed — {ex.Message}");
        }
    }
    return result;
}

static int TimeToPair(string? dateBegin)
{
    if (string.IsNullOrEmpty(dateBegin)) return -1;
    // dateBegin looks like "0001-01-01T08:00:00"
    if (!DateTime.TryParse(dateBegin, out var dt)) return -1;
    return (dt.Hour, dt.Minute) switch
    {
        (8,  0)  => 1,
        (9,  50) => 2,
        (11, 40) => 3,
        (13, 45) => 4,
        (15, 35) => 5,
        (17, 25) => 6,
        (19, 15) => 7,
        _        => -1
    };
}

static (string? room, string? building, bool isOnline) ParseClassroom(string? classroom)
{
    if (string.IsNullOrWhiteSpace(classroom) || classroom.Equals("Дистанционно", StringComparison.OrdinalIgnoreCase))
        return (null, null, true);

    // Expected format: "а.210 (К.1)"  or  "а. 210 (К.1)"
    var m = Regex.Match(classroom, @"а\.?\s*(\S+)\s*\(([^)]+)\)");
    if (m.Success)
        return (m.Groups[1].Value.TrimEnd('.'), m.Groups[2].Value.Trim(), false);

    // Fallback: return whole string as room number
    return (classroom.Trim(), null, false);
}

static (string lastName, string firstName) SplitTeacher(string? fullName)
{
    if (string.IsNullOrWhiteSpace(fullName)) return ("", "");
    var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length switch
    {
        0 => ("", ""),
        1 => (parts[0], ""),
        _ => (parts[0], parts[1])
    };
}

static LessonType MapLessonType(string? type) => type switch
{
    var t when t?.Contains("Лекц")  == true => LessonType.Lecture,
    var t when t?.Contains("Практ") == true => LessonType.Practical,
    var t when t?.Contains("Лаб")   == true => LessonType.Lab,
    var t when t?.Contains("Семин") == true => LessonType.Seminar,
    _                                        => LessonType.Lecture
};

//  enums (mirror UniScheduler.Domain.Enums) 

enum RussianDayOfWeek { Monday = 1, Tuesday, Wednesday, Thursday, Friday, Saturday }
enum WeekType         { Both = 0, Odd = 1, Even = 2 }
enum LessonType       { Lecture = 1, Practical = 2, Lab = 3, Seminar = 4 }

//  output record (mirror JsonEntryImport) 

record JsonEntryImport(
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

// internal types

record EntryKey(
    string GroupsKey,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    string Discipline,
    string LastName,
    string FirstName,
    string BuildingShortCode,
    string RoomNumber,
    bool IsOnline,
    LessonType LessonType
);

record EntryData(
    string[] Groups,
    string Discipline,
    string LastName,
    string FirstName,
    string? BuildingShortCode,
    string? RoomNumber,
    bool IsOnline,
    LessonType LessonType
);

// group discovery models

class GroupSearchResponse
{
    [JsonPropertyName("results")]
    public GroupSearchItem[]? Results { get; set; }
}

class GroupSearchItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

//  deserialization models (match site JS structure) 

class DaySchedule
{
    [JsonPropertyName("ScheduleCell")]
    public ScheduleCell?[]? ScheduleCell { get; set; }
}

class ScheduleCell
{
    [JsonPropertyName("DateBegin")]
    public string? DateBegin { get; set; }

    [JsonPropertyName("DateEnd")]
    public string? DateEnd { get; set; }

    [JsonPropertyName("Subgroup")]
    public Subgroup[]? Subgroup { get; set; }
}

class Subgroup
{
    [JsonPropertyName("DISCIPLINE")]
    public string? DISCIPLINE { get; set; }

    [JsonPropertyName("TYPE_LESSON")]
    public string? TYPE_LESSON { get; set; }

    [JsonPropertyName("GROUP")]
    public string[]? GROUP { get; set; }

    [JsonPropertyName("CLASSROOM")]
    public string? CLASSROOM { get; set; }

    [JsonPropertyName("TEACHER")]
    public string[]? TEACHER { get; set; }

    [JsonPropertyName("SUBGROUP")]
    public string? SUBGROUP { get; set; }
}
