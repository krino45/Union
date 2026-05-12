using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class Subject : Entity
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int AcademicYear { get; set; }
    public Term Term { get; set; }

    public double LectureHoursPerWeek { get; set; }
    public double PracticalHoursPerWeek { get; set; }
    public double LabHoursPerWeek { get; set; }

    public WeekType LectureWeekType { get; set; } = WeekType.Both;
    public WeekType PracticalWeekType { get; set; } = WeekType.Both;
    public WeekType LabWeekType { get; set; } = WeekType.Both;

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}
