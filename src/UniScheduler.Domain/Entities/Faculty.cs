using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class Faculty : Entity
{
    public Guid UniversityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;

    public University University { get; set; } = null!;
    public ICollection<StudentGroup> Groups { get; set; } = new List<StudentGroup>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
