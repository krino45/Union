using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class Teacher : Entity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? UserId { get; set; }

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();

    public string DisplayName => $"{LastName} {FirstName} {MiddleName}".Trim();

    public string ShortName =>
        (string.IsNullOrEmpty(FirstName) || string.IsNullOrEmpty(MiddleName))
            ? DisplayName
            : $"{LastName} {FirstName[0]}. {MiddleName[0]}.";
}
