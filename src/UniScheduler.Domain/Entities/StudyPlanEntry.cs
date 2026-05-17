using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class StudyPlanEntry : Entity
{
    public Guid StudyPlanId { get; set; }
    public StudyPlan StudyPlan { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public double LectureHours   { get; set; }
    public double PracticalHours { get; set; }
    public double LabHours       { get; set; }
    public double SeminarHours   { get; set; }
}
