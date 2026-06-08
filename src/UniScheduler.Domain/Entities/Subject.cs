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

    /// <summary>When true, lab sessions split this subject's groups into SubgroupCount parallel
    /// sessions (each its own teacher/room) that are not treated as group double-booking.</summary>
    public bool AllowsSubgroups { get; set; }
    public int SubgroupCount { get; set; } = 2;

    /// <summary>When true, this subject's lectures require a room with a projector.</summary>
    public bool RequiresProjector { get; set; }

    public Guid? DepartmentId { get; set; }

    public University University { get; set; } = null!;
    public Department? Department { get; set; }
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}
