namespace UniScheduler.Domain.Entities;

public class ScheduleEntryStudentGroup
{
    public Guid ScheduleEntryId { get; set; }
    public Guid StudentGroupId { get; set; }

    public ScheduleEntry ScheduleEntry { get; set; } = null!;
    public StudentGroup StudentGroup { get; set; } = null!;
}
