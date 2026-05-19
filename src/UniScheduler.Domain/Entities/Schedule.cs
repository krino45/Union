using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class Schedule : Entity
{
    public Guid UniversityId { get; set; }
    public int AcademicYear { get; set; }
    public Term Term { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
    public University University { get; set; } = null!;
    public bool AllowCrossFacultyLessons { get; set; }
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Draft;
    public DateTime? GeneratedAt { get; set; }
    public string? GenerationNotes { get; set; }
    public int? BaseScore { get; set; }

    public ICollection<ScheduleEntry> Entries { get; set; } = new List<ScheduleEntry>();
}
