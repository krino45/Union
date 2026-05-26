using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class StudyPlan : Entity
{
    public Guid UniversityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AcademicYear { get; set; }
    public Term Term { get; set; }
    public Guid? FacultyId { get; set; }

    public University University { get; set; } = null!;
    public Faculty? Faculty { get; set; }

    public Guid? CalendarPlanId { get; set; }
    public CalendarPlan? CalendarPlan { get; set; }

    public ICollection<StudyPlanGroup> Groups { get; set; } = new List<StudyPlanGroup>();
    public ICollection<StudyPlanEntry> Entries { get; set; } = new List<StudyPlanEntry>();
}
