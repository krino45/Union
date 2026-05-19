using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class Department : Entity
{
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public Guid FacultyId { get; set; }

    public Faculty Faculty { get; set; } = null!;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
}
