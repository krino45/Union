using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class FloorPlanDraft : Entity
{
    public Guid BuildingId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOpenToAdmins { get; set; }
    public string DraftJson { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    public Building Building { get; set; } = null!;
    public AppUser Owner { get; set; } = null!;
}
