using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class StudentGroup : Entity
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Specialty { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public DegreeType DegreeType { get; set; } = DegreeType.Bachelor;

    public Guid FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;

    public ICollection<ScheduleEntryStudentGroup> ScheduleEntryGroups { get; set; } = new List<ScheduleEntryStudentGroup>();
}
