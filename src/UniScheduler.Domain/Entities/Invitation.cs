using UniScheduler.Domain.Enums;
using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class Invitation : Entity
{
    public string Email { get; set; } = string.Empty;
    public Guid UniversityId { get; set; }
    public UniversityRole UniversityRole { get; set; }
    // "Admin" | "Teacher" - system role granted on registration
    public string SystemRole { get; set; } = "Teacher";
    // Optional: when set, registration links AppUser.TeacherId to this teacher,
    // and only the AppUser already linked to this teacher may accept the invitation while logged in.
    public Guid? TeacherId { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid InvitedByUserId { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public Guid? ConsumedByUserId { get; set; }

    public University University { get; set; } = null!;
    public AppUser InvitedBy { get; set; } = null!;
    public AppUser? ConsumedBy { get; set; }
    public Teacher? Teacher { get; set; }
}
