using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class RescheduleRequest : Entity
{
    public Guid RequestedByTeacherId { get; set; }
    public Guid OriginalEntryId { get; set; }

    // Teacher's optional suggestion for new slot (null = "any available")
    public RussianDayOfWeek? ProposedDayOfWeek { get; set; }
    public int? ProposedPairNumber { get; set; }
    public WeekType? ProposedWeekType { get; set; }

    public string Reason { get; set; } = string.Empty;
    public RescheduleStatus Status { get; set; } = RescheduleStatus.Pending;
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public Teacher RequestedByTeacher { get; set; } = null!;
    public ScheduleEntry OriginalEntry { get; set; } = null!;
}
