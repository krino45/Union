using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Scheduler;

public class ConflictDetector : IConflictDetector
{
    public IReadOnlyList<ConflictInfo> DetectConflicts(
        Guid entryId,
        Guid scheduleId,
        Guid? roomId,
        Guid teacherId,
        IEnumerable<Guid> groupIds,
        RussianDayOfWeek dayOfWeek,
        int pairNumber,
        WeekType weekType,
        bool isOnline,
        IEnumerable<ScheduleEntry> existingEntries)
    {
        var conflicts = new List<ConflictInfo>();
        var groupIdSet = groupIds.ToHashSet();

        foreach (var entry in existingEntries)
        {
            if (entry.Id == entryId) continue;

            // Only compare entries that could overlap (same slot)
            bool slotsOverlap = entry.DayOfWeek == dayOfWeek
                && entry.PairNumber == pairNumber
                && SlotsOverlapWeekType(entry.WeekType, weekType);

            if (!slotsOverlap) continue;

            // Room double-booking (non-online)
            if (!isOnline && roomId.HasValue && entry.RoomId == roomId)
            {
                conflicts.Add(new ConflictInfo(ConflictType.RoomDoubleBooked,
                    $"Room is already booked at {dayOfWeek} pair {pairNumber} ({weekType})"));
            }

            // Teacher double-booking
            if (entry.TeacherId == teacherId)
            {
                conflicts.Add(new ConflictInfo(ConflictType.TeacherDoubleBooked,
                    $"Teacher is already teaching at {dayOfWeek} pair {pairNumber} ({weekType})"));
            }

            // Group double-booking
            var entryGroupIds = entry.StudentGroups.Select(sg => sg.StudentGroupId).ToHashSet();
            var sharedGroups = groupIdSet.Intersect(entryGroupIds).ToList();
            if (sharedGroups.Any())
            {
                conflicts.Add(new ConflictInfo(ConflictType.GroupDoubleBooked,
                    $"Student group(s) already have a class at {dayOfWeek} pair {pairNumber} ({weekType})"));
            }
        }

        return conflicts;
    }

    private static bool SlotsOverlapWeekType(WeekType a, WeekType b)
    {
        if (a == WeekType.Both || b == WeekType.Both) return true;
        return a == b;
    }
}
