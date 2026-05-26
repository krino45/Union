using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

// Usage: SibSUTISToUnionTranslator [groups.json] [output.json]
//         SibSUTISToUnionTranslator merge <input.json> [output.json]
//
// groups.json format: { "ИП211": "123", "ИП212": "456" }
//   Key = group name, value = ID.
//
// merge: reads an already-scraped schedule JSON, merges entries that share the same
//        room+day+pair+weekType into one entry with combined groups, then writes the result.

if (args.Length >= 2 && args[0].Equals("merge", StringComparison.OrdinalIgnoreCase))
{
    var mergeIn  = args[1];
    var mergeOut = args.Length > 2 ? args[2] : mergeIn; // overwrite in place if no output path given

    if (!File.Exists(mergeIn))
    {
        Console.Error.WriteLine($"File not found: {mergeIn}");
        return 1;
    }

    var opts = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    var entries = JsonSerializer.Deserialize<List<JsonEntryImport>>(
        await File.ReadAllTextAsync(mergeIn, Encoding.UTF8), opts) ?? [];

    Console.WriteLine($"Loaded {entries.Count} entries from {mergeIn}.");

    // Key: (building, room, dayOfWeek, pairNumber, weekType) — the unique constraint the DB enforces.
    // Null/empty room means online; those are keyed by teacher+discipline so they don't merge blindly.
    var slotMap = new Dictionary<string, JsonEntryImport>();
    var merged = new List<JsonEntryImport>();
    int mergedCount = 0;

    foreach (var entry in entries)
    {
        bool hasRoom = !string.IsNullOrWhiteSpace(entry.RoomNumber) && !entry.IsOnline;
        string parallelTag = $"{entry.TeacherLastName}|{entry.TeacherFirstName}|{entry.SubgroupLabel}|{entry.ParallelGroupKey}";
        string key = hasRoom
            ? $"{entry.BuildingShortCode ?? ""}|{entry.RoomNumber}|{entry.DayOfWeek}|{entry.PairNumber}|{entry.WeekType}|{parallelTag}"
            : $"online|{entry.SubjectShortName}|{entry.DayOfWeek}|{entry.PairNumber}|{entry.WeekType}|{parallelTag}";

        if (slotMap.TryGetValue(key, out var existing))
        {
            var combined = existing.GroupNames
                .Union(entry.GroupNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();
            slotMap[key] = existing with { GroupNames = combined };
            mergedCount++;
        }
        else
        {
            slotMap[key] = entry;
        }
    }

    merged = slotMap.Values.ToList();
    Console.WriteLine($"Merged {mergedCount} duplicate slots. {merged.Count} entries remain.");

    await File.WriteAllTextAsync(mergeOut, JsonSerializer.Serialize(merged, opts), Encoding.UTF8);
    Console.WriteLine($"Written to {mergeOut}.");
    return 0;
}

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

// --- empty-group skip list ---
var emptyGroupsPath = args.Length > 2 ? args[2] : "empty_groups.json";
var emptyGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
if (File.Exists(emptyGroupsPath))
{
    try
    {
        var loaded = JsonSerializer.Deserialize<List<string>>(
            File.ReadAllText(emptyGroupsPath, Encoding.UTF8));
        if (loaded != null) foreach (var g in loaded) emptyGroups.Add(g);
        Console.WriteLine($"Loaded {emptyGroups.Count} previously empty groups from {emptyGroupsPath} — will skip them.");
    }
    catch { /* ignore corrupt file */ }
}

void SaveEmptyGroups()
{
    try
    {
        var json = JsonSerializer.Serialize(
            emptyGroups.OrderBy(x => x).ToList(),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(emptyGroupsPath, json, Encoding.UTF8);
    }
    catch (Exception ex) { Console.Error.WriteLine($"Warning: could not save empty groups — {ex.Message}"); }
}

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    SaveEmptyGroups();
    Console.WriteLine($"Interrupted — saved {emptyGroups.Count} empty groups to {emptyGroupsPath}.");
    Environment.Exit(130);
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => SaveEmptyGroups();

var knownGroupNames = new HashSet<string>(groupIds.Keys, StringComparer.OrdinalIgnoreCase);

// Key: canonical entry identity (ignores which week it was seen in, ignores group combination).
// Week parity is tracked PER GROUP, not per key: two groups that share an identical slot but on
// opposite week parities (A odd-only, B even-only) must stay separate, not collapse into one "Both".
var entryData       = new Dictionary<EntryKey, EntryData>();
var entryGroupWeeks = new Dictionary<EntryKey, Dictionary<string, (bool w1, bool w2)>>();

// SibSUTIS requires auth; credentials come from env vars SIBSUTIS_USER / SIBSUTIS_PASS.
var sibUser = Environment.GetEnvironmentVariable("SIBSUTIS_USER") ?? "";
var sibPass = Environment.GetEnvironmentVariable("SIBSUTIS_PASS") ?? "";

var rawCacheDir = Environment.GetEnvironmentVariable("SIBSUTIS_RAW_CACHE") ?? "raw_pages";
Directory.CreateDirectory(rawCacheDir);
Console.WriteLine($"Raw page cache: {Path.GetFullPath(rawCacheDir)}");

IPlaywright? playwright = null;
IBrowser? browser = null;
IPage? page = null;

async Task<IPage?> EnsureBrowserAsync()
{
    if (page != null) return page;

    playwright = await Playwright.CreateAsync();
    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Headless = true,
        Args = ["--no-sandbox", "--disable-dev-shm-usage"]
    });
    page = await browser.NewPageAsync();

    // Navigate once to trigger auth redirect if needed.
    var firstGroupId = groupIds.Values.FirstOrDefault() ?? "1";
    await page.GotoAsync($"https://sibsutis.ru/students/schedule/?type=student&month=2&group={firstGroupId}");

    if (page.Url.Contains("auth"))
    {
        if (string.IsNullOrEmpty(sibUser) || string.IsNullOrEmpty(sibPass))
        {
            Console.Error.WriteLine("Site requires login. Set SIBSUTIS_USER and SIBSUTIS_PASS env vars.");
            return null;
        }
        await page.FillAsync("input[name=USER_LOGIN]", sibUser);
        await page.FillAsync("input[name=USER_PASSWORD]", sibPass);
        await page.ClickAsync("input[name=Login]");
        await page.WaitForURLAsync("https://sibsutis.ru/students/schedule/**");
        Console.WriteLine("Logged in.");
    }

    return page;
}

foreach (var (groupName, groupId) in groupIds)
{
    if (emptyGroups.Contains(groupName))
    {
        Console.WriteLine($"Skipping {groupName} — previously empty.");
        continue;
    }

    string daysJson;
    var cachePath = Path.Combine(rawCacheDir, $"{groupId}.json");
    if (File.Exists(cachePath))
    {
        Console.WriteLine($"Group {groupName} (id={groupId}) — loaded from cache.");
        daysJson = await File.ReadAllTextAsync(cachePath, Encoding.UTF8);
    }
    else
    {
        Console.WriteLine($"Scraping group {groupName} (id={groupId})...");
        var pg = await EnsureBrowserAsync();
        if (pg == null) return 1;

        await pg.GotoAsync($"https://sibsutis.ru/students/schedule/?type=student&month=2&group={groupId}");
        try
        {
            await pg.WaitForSelectorAsync("a[class=calendar__cell]", new() { Timeout = 15_000 });
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.NextDouble()));
        }
        catch
        {
            Console.Error.WriteLine($"  Timed out waiting for calendar — marking as empty and skipping.");
            emptyGroups.Add(groupName);
            continue;
        }

        daysJson = await pg.EvaluateAsync<string>("() => JSON.stringify(days)");
        await File.WriteAllTextAsync(cachePath, daysJson, Encoding.UTF8);
    }

    string[]? days;
    try { days = JsonSerializer.Deserialize<string[]>(daysJson); }
    catch { Console.Error.WriteLine($"  Failed to parse days JSON — skipping."); continue; }

    if (days == null) continue;

    bool groupHadEntries = false;
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
                var discipline = sub.DISCIPLINE ?? "";
                var lessonType = MapLessonType(sub.TYPE_LESSON, discipline);
                var isLanguage = lessonType == LessonType.Language;
                var subgroup = NormalizeSubgroup(sub.SUBGROUP);

                var teacherNames = (sub.TEACHER ?? Array.Empty<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                if (teacherNames.Count == 0) teacherNames.Add("");
                bool emptyRoom = string.IsNullOrWhiteSpace(sub.CLASSROOM);
                bool parallel = teacherNames.Count > 1 && (isLanguage || emptyRoom);
                var streams = parallel ? teacherNames : teacherNames.Take(1).ToList();

                if (isLanguage || parallel) { room = null; building = null; isOnline = false; }

                foreach (var teacherFull in streams)
                {
                    var (lastName, firstName) = SplitTeacher(teacherFull);

                    var key = new EntryKey(
                        dayOfWeek, pairNumber,
                        discipline,
                        lastName, firstName,
                        building ?? "", room ?? "", isOnline, lessonType, subgroup);

                    if (!entryData.ContainsKey(key))
                        entryData[key] = new EntryData(relevantGroups, discipline, lastName, firstName,
                            building, room, isOnline, lessonType, subgroup, parallel);

                    // Track week parity per group so the same physical slot seen on multiple group
                    // pages merges correctly, but groups on opposite parities don't fuse into "Both".
                    if (!entryGroupWeeks.TryGetValue(key, out var groupWeeks))
                    {
                        groupWeeks = new Dictionary<string, (bool, bool)>(StringComparer.OrdinalIgnoreCase);
                        entryGroupWeeks[key] = groupWeeks;
                    }
                    foreach (var g in relevantGroups)
                    {
                        groupWeeks.TryGetValue(g, out var wk);
                        groupWeeks[g] = isWeek2 ? (wk.w1, true) : (true, wk.w2);
                    }
                }

                groupHadEntries = true;
            }
        }
    }

    if (!groupHadEntries)
    {
        emptyGroups.Add(groupName);
        Console.WriteLine($"  No entries found — marked as empty.");
    }
}

if (browser != null) await browser.DisposeAsync();
playwright?.Dispose();

SaveEmptyGroups();
Console.WriteLine($"Saved {emptyGroups.Count} empty groups to {emptyGroupsPath}.");

static WeekType ToWeekType(bool w1, bool w2) => (w1, w2) switch
{
    (true, true)  => WeekType.Both,
    (true, false) => WeekType.Odd,
    _             => WeekType.Even
};

var result = new List<JsonEntryImport>();
foreach (var (key, groupWeeks) in entryGroupWeeks)
{
    var d = entryData[key];

    // Split the key's groups by week parity: groups sharing the same parity become one entry,
    // groups on different parities become separate entries (the merge bug this fixes).
    foreach (var weekGroup in groupWeeks.GroupBy(kv => ToWeekType(kv.Value.w1, kv.Value.w2)))
    {
        var weekType = weekGroup.Key;
        var groups = weekGroup.Select(kv => kv.Key).OrderBy(x => x).ToList();

        // Parallel sessions of one logical class share a key so the importer links them as siblings.
        // Streams/subgroups group by their slot+discipline+groups; each carries a display label.
        bool hasSubgroup = !string.IsNullOrEmpty(d.Subgroup);
        string? parallelKey = (hasSubgroup || d.Parallel)
            ? $"{key.DayOfWeek}|{key.PairNumber}|{weekType}|{d.Discipline}|{string.Join(",", groups)}"
            : null;
        string? subgroupLabel = hasSubgroup ? $"Подгр. {d.Subgroup}"
            : d.Parallel ? d.LastName
            : null;

        result.Add(new JsonEntryImport(
            SubjectShortName: d.Discipline,
            TeacherLastName:  d.LastName,
            TeacherFirstName: d.FirstName,
            GroupNames:       groups,
            BuildingShortCode: d.BuildingShortCode,
            RoomNumber:       d.RoomNumber,
            DayOfWeek:        key.DayOfWeek,
            PairNumber:       key.PairNumber,
            WeekType:         weekType,
            LessonType:       d.LessonType,
            IsOnline:         d.IsOnline,
            ParallelGroupKey: parallelKey,
            SubgroupLabel:    subgroupLabel
        ));
    }
}

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
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.NextDouble()));
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

static LessonType MapLessonType(string? type, string? discipline)
{
    if (IsLanguageDiscipline(discipline))
        return LessonType.Language;
    return type switch
    {
        var t when t?.Contains("Лекц") == true => LessonType.Lecture,
        var t when t?.Contains("Практ") == true => LessonType.Practical,
        var t when t?.Contains("Лаб") == true => LessonType.Lab,
        var t when t?.Contains("Семин") == true => LessonType.Seminar,
        var t when t?.Contains("ин.яз") == true => LessonType.Language,
        _ => LessonType.Lecture
    };
}

static bool IsLanguageDiscipline(string? discipline)
{
    if (string.IsNullOrWhiteSpace(discipline)) return false;
    var d = discipline.ToLowerInvariant();
    return d.Contains("иностранн") || d.Contains("ин.яз") || d.Contains("ин. яз");
}

static string NormalizeSubgroup(string? subgroup)
{
    var s = subgroup?.Trim();
    return string.IsNullOrEmpty(s) || s == "0" ? "" : s;
}

//  enums (mirror UniScheduler.Domain.Enums) 

enum RussianDayOfWeek { Monday = 1, Tuesday, Wednesday, Thursday, Friday, Saturday }
enum WeekType         { Both = 0, Odd = 1, Even = 2 }
enum LessonType       { Lecture = 1, Practical = 2, Lab = 3, Seminar = 4, Language = 5 }

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
    bool IsOnline,
    string? ParallelGroupKey = null,
    string? SubgroupLabel = null
);

// internal types

record EntryKey(
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    string Discipline,
    string LastName,
    string FirstName,
    string BuildingShortCode,
    string RoomNumber,
    bool IsOnline,
    LessonType LessonType,
    string Subgroup
);

record EntryData(
    string[] Groups,
    string Discipline,
    string LastName,
    string FirstName,
    string? BuildingShortCode,
    string? RoomNumber,
    bool IsOnline,
    LessonType LessonType,
    string Subgroup,
    bool Parallel
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
