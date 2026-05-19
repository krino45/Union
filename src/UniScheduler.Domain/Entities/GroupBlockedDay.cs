using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class GroupBlockedDay : Entity
{
    public Guid GroupId { get; set; }
    public RussianDayOfWeek DayOfWeek { get; set; }

    public StudentGroup Group { get; set; } = null!;
}
