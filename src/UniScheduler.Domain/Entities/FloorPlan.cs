using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class FloorPlan : Entity
{
    public Guid BuildingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FloorPlanJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    public Building Building { get; set; } = null!;
    public AppUser? CreatedByUser { get; set; }
}
