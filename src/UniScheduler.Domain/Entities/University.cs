using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class University : Entity
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? City { get; set; }

    public ICollection<Faculty> Faculties { get; set; } = new List<Faculty>();
    public ICollection<Building> Buildings { get; set; } = new List<Building>();
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
    public ICollection<CalendarPlan> CalendarPlans { get; set; } = new List<CalendarPlan>();
    public ICollection<SolverSettings> SolverSettings { get; set; } = new List<SolverSettings>();
    public ICollection<UserUniversityAccess> UserAccesses { get; set; } = new List<UserUniversityAccess>();
}
