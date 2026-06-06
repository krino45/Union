using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Schedules.Internal;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

// Best-effort manual filler for a single not-placed plan item (subject + group + lesson type).
// Rebuilds the full expected requirement exactly as generation would, then
// greedily drops each into the first conflict-free slot.
public record InsertUnplacedEntriesCommand(
    Guid ScheduleId, Guid SubjectId, Guid GroupId, LessonType LessonType)
    : IRequest<InsertUnplacedResult>;

public record InsertUnplacedResult(int Inserted, int Failed, string Message);

public class InsertUnplacedEntriesCommandHandler
    : IRequestHandler<InsertUnplacedEntriesCommand, InsertUnplacedResult>
{
    private const int NumDays = 6;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public InsertUnplacedEntriesCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<InsertUnplacedResult> Handle(InsertUnplacedEntriesCommand r, CancellationToken ct)
    {
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == r.ScheduleId, ct)
            ?? throw new NotFoundException(nameof(Schedule), r.ScheduleId);
        if (schedule.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        ScheduleAccessGuard.EnsureCanEdit(schedule, _user);

        var settingsEntity = await _db.SolverSettings.FirstOrDefaultAsync(ct);
        var weights = settingsEntity == null
            ? new Common.Models.SolverWeights()
            : new Common.Models.SolverWeights(settingsEntity);
        int maxPePerDay = Math.Max(1, weights.MaxPePerDay);
        var shared = await ScheduleBuildContext.LoadSharedDataAsync(_db, schedule, weights, ct);
        int numPairs = shared.PairsPerDay;

        var (allReqs, _) = ScheduleRequirementBuilder.BuildAllRequirementsStable(shared);
        var target = allReqs
            .Where(req => req.SubjectId == r.SubjectId && req.LessonType == r.LessonType
                          && req.GroupIds.Contains(r.GroupId))
            .ToList();
        if (target.Count == 0)
            return new InsertUnplacedResult(0, 0, "Нечего вставлять: требований для этого пункта не найдено.");

        var units = target
            .Select((req, ord) => (req, ord))
            .GroupBy(x => x.req.ParallelKey is int pk ? $"p{pk}" : $"s{x.ord}")
            .Select(g => g.Select(x => x.req).ToList())
            .ToList();

        var existing = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == r.ScheduleId)
            .ToListAsync(ct);

        var occ = BuildOccupancy(existing, shared, numPairs);

        var distributedRoomId = shared.Rooms.FirstOrDefault(rm => rm.IsDistributed)?.Id;
        var sportsHallId = shared.Rooms.FirstOrDefault(rm => rm.RoomType == RoomType.SportsHall && !rm.IsDistributed)?.Id;
        var regularRooms = shared.Rooms
            .Where(rm => !rm.IsDistributed && rm.RoomType != RoomType.SportsHall && !rm.IsOnline)
            .ToList();

        var parallelGuids = new Dictionary<int, Guid>();
        int inserted = 0, failed = 0;

        foreach (var unit in units)
        {
            if (TryPlaceUnit(unit, shared, occ, numPairs, maxPePerDay, distributedRoomId, sportsHallId, regularRooms,
                    parallelGuids, r.ScheduleId, out var newEntries))
            {
                foreach (var e in newEntries)
                {
                    _db.ScheduleEntries.Add(e);
                    foreach (var sg in e.StudentGroups) _db.ScheduleEntryStudentGroups.Add(sg);
                }
                inserted++;
            }
            else
            {
                failed++;
            }
        }

        if (inserted > 0 && schedule.Status == ScheduleStatus.Published)
        {
            schedule.Status = ScheduleStatus.Draft;
            ScheduleAccessGuard.TransferOwnershipOnDemote(schedule, _user);
        }

        await _db.SaveChangesAsync(ct);

        var msg = failed == 0
            ? $"Вставлено занятий: {inserted}."
            : $"Вставлено: {inserted}, не удалось разместить: {failed} (нет свободного слота/аудитории).";
        return new InsertUnplacedResult(inserted, failed, msg);
    }

    private bool TryPlaceUnit(
        List<Common.Models.SchedulerRequirement> unit, SharedData shared, Occupancy occ, int numPairs,
        int maxPePerDay, Guid? distributedRoomId, Guid? sportsHallId, List<Room> regularRooms,
        Dictionary<int, Guid> parallelGuids, Guid scheduleId, out List<ScheduleEntry> placed)
    {
        placed = new List<ScheduleEntry>();
        var weekType = unit[0].WeekType;
        var calWis = CalWeeks(weekType);
        bool isPe = unit[0].RequiresSportsHall;

        var unitGroupIds = unit.SelectMany(req => req.GroupIds).Distinct().ToList();

        foreach (int d in DayOrder())
        {
            if (unitGroupIds.Any(gid => occ.GroupBlockedDay.Contains((gid, d)))) continue;

            foreach (int p in PairOrder(numPairs, isPe))
            {
                // Group / teacher hard conflicts + teacher availability across every affected week.
                bool slotFree = true;
                foreach (int wi in calWis)
                {
                    foreach (var gid in unitGroupIds)
                        if (occ.GroupBusy.Contains((gid, d, p, wi))) { slotFree = false; break; }
                    if (!slotFree) break;
                    foreach (var req in unit)
                        if (occ.TeacherBusy.Contains((req.TeacherId, d, p, wi))
                            || occ.TeacherBlocked.Contains((req.TeacherId, d, p, wi))) { slotFree = false; break; }
                    if (!slotFree) break;
                }
                if (!slotFree) continue;

                // SanPiN PE-per-day cap.
                if (isPe && calWis.Any(wi => unitGroupIds.Any(gid =>
                        occ.PeCount.GetValueOrDefault((gid, d, wi)) >= maxPePerDay)))
                    continue;

                // Assign a room to every sibling at this slot.
                var rooms = AssignRooms(unit, shared, occ, d, p, calWis, distributedRoomId, sportsHallId, regularRooms);
                if (rooms == null) continue;

                // Commit the unit.
                foreach (var req in unit)
                {
                    Guid? parallelGroupId = null;
                    if (req.ParallelKey is int pk)
                    {
                        if (!parallelGuids.TryGetValue(pk, out var g)) parallelGuids[pk] = g = Guid.NewGuid();
                        parallelGroupId = g;
                    }
                    var roomId = rooms[req.Index];
                    var entry = new ScheduleEntry
                    {
                        ScheduleId = scheduleId,
                        SubjectId = req.SubjectId,
                        TeacherId = req.TeacherId,
                        RoomId = req.IsOnline ? null : roomId,
                        DayOfWeek = (RussianDayOfWeek)(d + 1),
                        PairNumber = p,
                        WeekType = weekType,
                        LessonType = req.LessonType,
                        IsOnline = req.IsOnline,
                        ParallelGroupId = parallelGroupId,
                        SubgroupLabel = req.SubgroupLabel
                    };
                    foreach (var gid in req.GroupIds)
                        entry.StudentGroups.Add(new ScheduleEntryStudentGroup { ScheduleEntry = entry, StudentGroupId = gid });
                    placed.Add(entry);

                    // Mark occupancy so later units in this same insert see it.
                    foreach (int wi in calWis)
                    {
                        foreach (var gid in req.GroupIds) occ.GroupBusy.Add((gid, d, p, wi));
                        occ.TeacherBusy.Add((req.TeacherId, d, p, wi));
                        if (roomId.HasValue && IsSingleOccupancy(roomId.Value, distributedRoomId, sportsHallId))
                            occ.RoomBusy.Add((roomId.Value, d, p, wi));
                        if (isPe)
                            foreach (var gid in req.GroupIds)
                                occ.PeCount[(gid, d, wi)] = occ.PeCount.GetValueOrDefault((gid, d, wi)) + 1;
                    }
                }
                return true;
            }
        }
        return false;
    }

    // Returns req.Index -> chosen room (null only for online). Null result => no room fit this slot.
    private Dictionary<int, Guid?>? AssignRooms(
        List<Common.Models.SchedulerRequirement> unit, SharedData shared, Occupancy occ,
        int d, int p, int[] calWis, Guid? distributedRoomId, Guid? sportsHallId, List<Room> regularRooms)
    {
        var result = new Dictionary<int, Guid?>();
        var takenRegular = new HashSet<Guid>(); // distinct regular rooms within one slot (lab subgroups)

        foreach (var req in unit)
        {
            if (req.IsOnline) { result[req.Index] = null; continue; }

            if (req.RequiresDistributedRoom)
            {
                if (distributedRoomId == null) return null;
                result[req.Index] = distributedRoomId; // multi-occupancy
                continue;
            }
            if (req.RequiresSportsHall)
            {
                if (sportsHallId == null) return null;
                result[req.Index] = sportsHallId; // multi-occupancy
                continue;
            }

            int headcount = req.HeadcountOverride ?? req.GroupIds.Sum(g => shared.GroupSizes.GetValueOrDefault(g, 0));
            Guid? pick = null;
            foreach (var room in regularRooms)
            {
                if (takenRegular.Contains(room.Id)) continue;
                if (!RegularRoomFits(req, room, headcount)) continue;
                if (calWis.Any(wi => occ.RoomBusy.Contains((room.Id, d, p, wi)))) continue;
                pick = room.Id;
                break;
            }
            if (pick == null) return null;
            takenRegular.Add(pick.Value);
            result[req.Index] = pick;
        }
        return result;
    }

    private static bool RegularRoomFits(Common.Models.SchedulerRequirement req, Room room, int headcount)
    {
        if (req.NeedsProjector && !room.HasProjector) return false;
        if (req.NeedsComputers && !room.HasComputers) return false;
        if (req.NeedsLab && !room.HasLab) return false;
        if (room.AllowedLessonTypes is { Count: > 0 } allowed && !allowed.Contains(req.LessonType)) return false;
        if (room.Capacity > 0 && headcount > 0 && room.Capacity < headcount) return false;
        return true;
    }

    private static bool IsSingleOccupancy(Guid roomId, Guid? distributedRoomId, Guid? sportsHallId)
        => roomId != distributedRoomId && roomId != sportsHallId;

    private static Occupancy BuildOccupancy(List<ScheduleEntry> existing, SharedData shared, int numPairs)
    {
        var occ = new Occupancy();

        foreach (var g in shared.Groups)
            foreach (var bd in g.BlockedDays)
                occ.GroupBlockedDay.Add((g.Id, (int)bd.DayOfWeek - 1));

        foreach (var ta in shared.TeacherAvailabilities)
        {
            int d = (int)ta.DayOfWeek - 1;
            int p = ta.PairNumber;
            foreach (int wi in CalWeeks(ta.WeekType))
                occ.TeacherBlocked.Add((ta.TeacherId, d, p, wi));
        }

        foreach (var e in existing)
        {
            int d = (int)e.DayOfWeek - 1;
            int p = e.PairNumber;
            bool multiOcc = e.RoomId.HasValue
                && (shared.Rooms.Any(rm => rm.Id == e.RoomId &&
                    (rm.IsDistributed || rm.RoomType == RoomType.SportsHall)));
            bool isPe = e.LessonType == LessonType.PhysicalEducation;
            foreach (int wi in CalWeeks(e.WeekType))
            {
                foreach (var sg in e.StudentGroups) occ.GroupBusy.Add((sg.StudentGroupId, d, p, wi));
                occ.TeacherBusy.Add((e.TeacherId, d, p, wi));
                if (e.RoomId.HasValue && !multiOcc) occ.RoomBusy.Add((e.RoomId.Value, d, p, wi));
                if (isPe)
                    foreach (var sg in e.StudentGroups)
                        occ.PeCount[(sg.StudentGroupId, d, wi)] = occ.PeCount.GetValueOrDefault((sg.StudentGroupId, d, wi)) + 1;
            }
        }
        return occ;
    }

    // Calendar week indices an entry occupies: 0 = odd week, 1 = even week.
    private static int[] CalWeeks(WeekType wt) => wt switch
    {
        WeekType.Odd => new[] { 0 },
        WeekType.Even => new[] { 1 },
        _ => new[] { 0, 1 }
    };

    // Saturday (index 5) last - we already iterate Mon..Sat, so natural order suffices.
    private static IEnumerable<int> DayOrder() => Enumerable.Range(0, NumDays);

    // Middle pairs first for nicer placement; PE prefers the 5th pair (SanPiN-exempt from overload).
    private static IEnumerable<int> PairOrder(int numPairs, bool isPe)
    {
        var all = Enumerable.Range(1, numPairs);
        if (isPe && numPairs >= 5)
            return new[] { 5 }.Concat(all.Where(p => p != 5));
        return all.OrderBy(p => p switch { 3 => 0, 4 => 1, 2 => 2, 5 => 3, 1 => 4, _ => 5 + p });
    }

    private sealed class Occupancy
    {
        public HashSet<(Guid groupId, int d, int p, int wi)> GroupBusy { get; } = new();
        public HashSet<(Guid teacherId, int d, int p, int wi)> TeacherBusy { get; } = new();
        public HashSet<(Guid teacherId, int d, int p, int wi)> TeacherBlocked { get; } = new();
        public HashSet<(Guid roomId, int d, int p, int wi)> RoomBusy { get; } = new();
        public HashSet<(Guid groupId, int d)> GroupBlockedDay { get; } = new();
        public Dictionary<(Guid groupId, int d, int wi), int> PeCount { get; } = new();
    }
}
