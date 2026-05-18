using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class FloorPlanEdge : Entity
{
    public Guid BuildingId { get; set; }
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public int DistanceMeters { get; set; }

    public Building Building { get; set; } = null!;
    public FloorPlanNode FromNode { get; set; } = null!;
    public FloorPlanNode ToNode { get; set; } = null!;
}
