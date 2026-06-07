using UniScheduler.Application.Features.Schedules.Internal;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Lns;

// Phase 0 of polish: drive hard conflicts toward zero before the LNS optimize loop.
public static class FeasibilityRepair
{
    private const int NumDays = 6;

    public sealed record Result(List<ScheduleEntry> Entries, int Before, int After, int Relocated, int Unresolved,
        int BlockedBefore, int BlockedAfter)
    {
        public bool ChangedAnything => Relocated > 0;
        public string Summary => Before == 0 && BlockedBefore == 0
            ? "Конфликты: нет."
            : $"Конфликты: было {Before}, перемещено {Relocated}, осталось {Unresolved}.";
    }

    public static Result Resolve(IReadOnlyList<ScheduleEntry> entries, SharedData shared)
    {
        int numPairs = shared.PairsPerDay;
        int maxPe = Math.Max(1, shared.Weights.MaxPePerDay);
        int before = ScheduleScoreCalculator.Score_HardConflicts(entries, shared.ScoreCtx) / 100_000;
        int blockedBefore = ScheduleScoreCalculator.Score_BlockedPlacement(entries, shared.ScoreCtx) / ScheduleScoreCalculator.BlockedPlacementPenalty;
        if (before == 0 && blockedBefore == 0)
            return new Result(entries.ToList(), 0, 0, 0, 0, 0, 0);

        var multiOcc = shared.Rooms
            .Where(r => r.IsDistributed || r.RoomType == RoomType.SportsHall)
            .Select(r => r.Id).ToHashSet();
        var regularRooms = shared.Rooms
            .Where(r => !r.IsDistributed && r.RoomType != RoomType.SportsHall && !r.IsOnline)
            .ToList();
        var roomById = shared.Rooms.ToDictionary(r => r.Id);

        var occ = new Occ();
        foreach (var g in shared.Groups)
            foreach (var bd in g.BlockedDays)
                occ.GroupBlockedDay.Add((g.Id, (int)bd.DayOfWeek - 1));
        foreach (var ta in shared.TeacherAvailabilities)
            foreach (int wi in CalWeeks(ta.WeekType))
                occ.TeacherBlocked.Add((ta.TeacherId, (int)ta.DayOfWeek - 1, ta.PairNumber, wi));

        var units = entries
            .GroupBy(e => e.ParallelGroupId.HasValue ? $"p{e.ParallelGroupId}" : $"s{e.Id}")
            .Select(g => g.ToList())
            .OrderBy(u => u[0].ParallelGroupId.HasValue ? 0 : 1)
            .ToList();

        var result = new List<ScheduleEntry>(entries.Count);
        int relocated = 0, unresolved = 0;

        foreach (var unit in units)
        {
            int d = (int)unit[0].DayOfWeek - 1;
            int p = unit[0].PairNumber;

            if (UnitFitsCurrent(unit, occ, multiOcc))
            {
                CommitAtCurrent(unit, occ, multiOcc, result);
                continue;
            }

            if (TryRelocate(unit, occ, numPairs, maxPe, multiOcc, regularRooms, roomById, shared, result))
                relocated++;
            else
            {
                CommitAtCurrent(unit, occ, multiOcc, result); // cant fix - leave, still counts as a conflict
                unresolved++;
            }
        }

        int after = ScheduleScoreCalculator.Score_HardConflicts(result, shared.ScoreCtx) / 100_000;
        int blockedAfter = ScheduleScoreCalculator.Score_BlockedPlacement(entries, shared.ScoreCtx) / ScheduleScoreCalculator.BlockedPlacementPenalty;

        return new Result(result, before, after, relocated, unresolved, blockedBefore, blockedAfter);
    }

    // True if the units current placement collides with nothing accepted so far. Multi-occupancy venues never collide.
    private static bool UnitFitsCurrent(List<ScheduleEntry> unit, Occ occ, HashSet<Guid> multiOcc)
    {
        int d = (int)unit[0].DayOfWeek - 1;
        int p = unit[0].PairNumber;
        return CollisionFree(unit, d, p, occ, multiOcc, checkRoomId: true);
    }

    private static bool CollisionFree(
        List<ScheduleEntry> unit, int d, int p, Occ occ, HashSet<Guid> multiOcc, bool checkRoomId)
    {
        foreach (var e in unit)
        foreach (int wi in CalWeeks(e.WeekType))
        {
            foreach (var sg in e.StudentGroups)
            {
                if (occ.GroupBusy.Contains((sg.StudentGroupId, d, p, wi))) return false;
                if (occ.GroupBlockedDay.Contains((sg.StudentGroupId, d))) return false;
            }

            if (occ.TeacherBusy.Contains((e.TeacherId, d, p, wi))) return false;
            if (occ.TeacherBlocked.Contains((e.TeacherId, d, p, wi))) return false;

            if (checkRoomId && e.RoomId.HasValue && !multiOcc.Contains(e.RoomId.Value)
                && occ.RoomBusy.Contains((e.RoomId.Value, d, p, wi))) return false;
        }

        return true;
    }

    private static void CommitAtCurrent(
        List<ScheduleEntry> unit, Occ occ, HashSet<Guid> multiOcc, List<ScheduleEntry> result)
    {
        int d = (int)unit[0].DayOfWeek - 1;
        foreach (var e in unit)
        {
            var kept = Clone(e, e.DayOfWeek, e.PairNumber, e.RoomId);
            Mark(kept, d, kept.PairNumber, kept.RoomId, occ, multiOcc);
            result.Add(kept);
        }
    }

    private static bool TryRelocate(
        List<ScheduleEntry> unit, Occ occ, int numPairs, int maxPe, HashSet<Guid> multiOcc,
        List<Room> regularRooms, Dictionary<Guid, Room> roomById, SharedData shared, List<ScheduleEntry> result)
    {
        var weekType = unit[0].WeekType;
        var calWis = CalWeeks(weekType);
        bool isPe = unit.Any(e => e.LessonType == LessonType.PhysicalEducation);
        var groupIds = unit.SelectMany(e => e.StudentGroups.Select(sg => sg.StudentGroupId)).Distinct().ToList();

        foreach (int d in Enumerable.Range(0, NumDays))
        {
            if (groupIds.Any(g => occ.GroupBlockedDay.Contains((g, d)))) continue;

            foreach (int p in PairOrder(numPairs, isPe))
            {
                bool ok = true;
                foreach (int wi in calWis)
                {
                    foreach (var g in groupIds) if (occ.GroupBusy.Contains((g, d, p, wi))) { ok = false; break; }
                    if (!ok) break;
                    foreach (var e in unit)
                        if (occ.TeacherBusy.Contains((e.TeacherId, d, p, wi))
                            || occ.TeacherBlocked.Contains((e.TeacherId, d, p, wi))) { ok = false; break; }
                    if (!ok) break;
                }
                if (!ok) continue;

                if (isPe && calWis.Any(wi => groupIds.Any(g => occ.PeCount.GetValueOrDefault((g, d, wi)) >= maxPe)))
                    continue;

                var rooms = AssignRooms(unit, d, p, calWis, occ, multiOcc, regularRooms, roomById, shared);
                if (rooms == null) continue;

                foreach (var e in unit)
                {
                    var newRoom = rooms[e.Id];
                    var moved = Clone(e, (RussianDayOfWeek)(d + 1), p, newRoom);
                    Mark(moved, d, p, newRoom, occ, multiOcc);
                    result.Add(moved);
                }
                return true;
            }
        }
        return false;
    }

    private static Dictionary<Guid, Guid?>? AssignRooms(
        List<ScheduleEntry> unit, int d, int p, int[] calWis, Occ occ, HashSet<Guid> multiOcc,
        List<Room> regularRooms, Dictionary<Guid, Room> roomById, SharedData shared)
    {
        var picks = new Dictionary<Guid, Guid?>();
        var takenThisSlot = new HashSet<Guid>();

        foreach (var e in unit)
        {
            if (e.IsOnline || !e.RoomId.HasValue) { picks[e.Id] = null; continue; }
            if (multiOcc.Contains(e.RoomId.Value)) { picks[e.Id] = e.RoomId; continue; }

            int headcount = e.StudentGroups.Sum(sg => shared.GroupSizes.GetValueOrDefault(sg.StudentGroupId, 0));
            bool RoomFree(Guid id) => !takenThisSlot.Contains(id)
                && calWis.All(wi => !occ.RoomBusy.Contains((id, d, p, wi)));

            Guid? pick = null;
            // Prefer keeping the same room when it is free at the new slot.
            if (RoomFree(e.RoomId.Value)) pick = e.RoomId;
            else
            {
                foreach (var room in regularRooms)
                {
                    if (!RoomFree(room.Id)) continue;
                    if (room.Capacity > 0 && headcount > 0 && room.Capacity < headcount) continue;
                    pick = room.Id; break;
                }
            }
            if (pick == null) return null;
            takenThisSlot.Add(pick.Value);
            picks[e.Id] = pick;
        }
        return picks;
    }

    private static void Mark(ScheduleEntry e, int d, int p, Guid? roomId, Occ occ, HashSet<Guid> multiOcc)
    {
        bool isPe = e.LessonType == LessonType.PhysicalEducation;
        foreach (int wi in CalWeeks(e.WeekType))
        {
            foreach (var sg in e.StudentGroups) occ.GroupBusy.Add((sg.StudentGroupId, d, p, wi));
            occ.TeacherBusy.Add((e.TeacherId, d, p, wi));
            if (roomId.HasValue && !multiOcc.Contains(roomId.Value)) occ.RoomBusy.Add((roomId.Value, d, p, wi));
            if (isPe)
                foreach (var sg in e.StudentGroups)
                    occ.PeCount[(sg.StudentGroupId, d, wi)] = occ.PeCount.GetValueOrDefault((sg.StudentGroupId, d, wi)) + 1;
        }
    }

    private static ScheduleEntry Clone(ScheduleEntry e, RussianDayOfWeek day, int pair, Guid? roomId)
    {
        var clone = new ScheduleEntry
        {
            ScheduleId = e.ScheduleId,
            SubjectId = e.SubjectId,
            TeacherId = e.TeacherId,
            RoomId = e.IsOnline ? null : roomId,
            DayOfWeek = day,
            PairNumber = pair,
            WeekType = e.WeekType,
            LessonType = e.LessonType,
            IsOnline = e.IsOnline,
            ParallelGroupId = e.ParallelGroupId,
            SubgroupLabel = e.SubgroupLabel
        };
        foreach (var sg in e.StudentGroups)
            clone.StudentGroups.Add(new ScheduleEntryStudentGroup { StudentGroupId = sg.StudentGroupId, ScheduleEntry = clone });
        return clone;
    }

    private static int[] CalWeeks(WeekType wt) => wt switch
    {
        WeekType.Odd => new[] { 0 },
        WeekType.Even => new[] { 1 },
        _ => new[] { 0, 1 }
    };

    private static IEnumerable<int> PairOrder(int numPairs, bool isPe)
    {
        var all = Enumerable.Range(1, numPairs);
        if (isPe && numPairs >= 5) return new[] { 5 }.Concat(all.Where(p => p != 5));
        return all.OrderBy(p => p switch { 3 => 0, 4 => 1, 2 => 2, 5 => 3, 1 => 4, _ => 5 + p });
    }

    private sealed class Occ
    {
        public HashSet<(Guid, int, int, int)> GroupBusy { get; } = new();
        public HashSet<(Guid, int, int, int)> TeacherBusy { get; } = new();
        public HashSet<(Guid, int, int, int)> TeacherBlocked { get; } = new();
        public HashSet<(Guid, int, int, int)> RoomBusy { get; } = new();
        public HashSet<(Guid, int)> GroupBlockedDay { get; } = new();
        public Dictionary<(Guid, int, int), int> PeCount { get; } = new();
    }
}
