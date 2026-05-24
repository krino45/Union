using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class AppUser : Entity
{
    public string Username { get; set; } = string.Empty;
    // Optional e-mail. Required for teacher accounts (their primary login + invitation matching);
    // admins/superadmins may log in by username only. Unique (case-insensitive) when present.
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    // System-level role: "SuperAdmin" | "Admin" | "Teacher"
    // SuperAdmin has no university context and manages all universities.
    public string Role { get; set; } = string.Empty;
    public Guid? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }
    public ICollection<UserUniversityAccess> UniversityAccesses { get; set; } = new List<UserUniversityAccess>();
}
