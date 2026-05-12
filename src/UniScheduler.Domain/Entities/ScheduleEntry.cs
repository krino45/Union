using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class ScheduleEntry : Entity
{
    public Guid ScheduleId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid? RoomId { get; set; }

    public RussianDayOfWeek DayOfWeek { get; set; }
    public int PairNumber { get; set; }
    public WeekType WeekType { get; set; }
    public LessonType LessonType { get; set; }
    public bool IsOnline { get; set; }

    public Schedule Schedule { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public Room? Room { get; set; }
    public ICollection<ScheduleEntryStudentGroup> StudentGroups { get; set; } = new List<ScheduleEntryStudentGroup>();
}
