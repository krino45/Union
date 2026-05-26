using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Common.Interfaces;

public interface IConflictDetector
{
    IReadOnlyList<ConflictInfo> DetectConflicts(
        Guid entryId,
        Guid scheduleId,
        Guid? roomId,
        Guid teacherId,
        IEnumerable<Guid> groupIds,
        RussianDayOfWeek dayOfWeek,
        int pairNumber,
        WeekType weekType,
        bool isOnline,
        IEnumerable<ScheduleEntry> existingEntries,
        Guid? parallelGroupId = null,
        bool roomIsDistributed = false);
}
