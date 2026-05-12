using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class Faculty : Entity
{
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;

    public ICollection<StudentGroup> Groups { get; set; } = new List<StudentGroup>();
}
