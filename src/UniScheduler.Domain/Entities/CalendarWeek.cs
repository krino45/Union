using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class CalendarWeek : Entity
{
    public Guid CalendarPlanId { get; set; }
    public CalendarPlan CalendarPlan { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public WeekKind Kind { get; set; }
    public string? Note { get; set; }
}
