using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class Subject : Entity
{
    public Guid UniversityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int AcademicYear { get; set; }
    public Term Term { get; set; }

    public Guid? DepartmentId { get; set; }

    public University University { get; set; } = null!;
    public Department? Department { get; set; }
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}
