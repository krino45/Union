namespace UniScheduler.Domain.Entities;

public class StudyPlanGroup
{
    public Guid StudyPlanId { get; set; }
    public StudyPlan StudyPlan { get; set; } = null!;
    public Guid StudentGroupId { get; set; }
    public StudentGroup StudentGroup { get; set; } = null!;
}
