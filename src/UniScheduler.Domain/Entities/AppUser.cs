using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class AppUser : Entity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    // System-level role: "SuperAdmin" | "Admin" | "Teacher"
    // SuperAdmin has no university context and manages all universities.
    public string Role { get; set; } = string.Empty;
    public Guid? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }
    public ICollection<UserUniversityAccess> UniversityAccesses { get; set; } = new List<UserUniversityAccess>();
}
