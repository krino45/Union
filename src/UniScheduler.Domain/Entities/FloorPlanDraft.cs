using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class FloorPlanDraft : Entity
{
    public Guid BuildingId { get; set; }
    public string DraftJson { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    public Building Building { get; set; } = null!;
}
