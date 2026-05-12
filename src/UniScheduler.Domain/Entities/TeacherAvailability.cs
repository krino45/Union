using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class TeacherAvailability : Entity
{
    public Guid TeacherId { get; set; }
    public RussianDayOfWeek DayOfWeek { get; set; }
    public int PairNumber { get; set; }
    public WeekType WeekType { get; set; } = WeekType.Both;
    public string? Reason { get; set; }

    // For recurring blocks (e.g. every Monday pair 1)
    public bool IsRecurring { get; set; } = true;

    // For one-off blocks (date range overrides IsRecurring)
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public Teacher Teacher { get; set; } = null!;
}
