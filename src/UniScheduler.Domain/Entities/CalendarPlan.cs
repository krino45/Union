using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class CalendarPlan : Entity
{
    public Guid UniversityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AcademicYear { get; set; }
    public Term Term { get; set; }

    public University University { get; set; } = null!;
    public ICollection<CalendarWeek> Weeks { get; set; } = new List<CalendarWeek>();
    public ICollection<StudyPlan> StudyPlans { get; set; } = new List<StudyPlan>();
}
