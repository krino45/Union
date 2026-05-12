using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class AppUser : Entity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Admin" | "Teacher"
    public Guid? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }
}
