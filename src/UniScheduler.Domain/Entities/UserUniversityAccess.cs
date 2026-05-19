using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class UserUniversityAccess
{
    public Guid UserId { get; set; }
    public Guid UniversityId { get; set; }
    public UniversityRole Role { get; set; }

    public AppUser User { get; set; } = null!;
    public University University { get; set; } = null!;
}
