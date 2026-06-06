using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class FloorPlanNode : Entity
{
    public Guid BuildingId { get; set; }
    public int Floor { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
    public FloorPlanNodeType NodeType { get; set; }
    public Guid? RoomId { get; set; }
    public string? Label { get; set; }

    public Building Building { get; set; } = null!;
    public Room? Room { get; set; }
    public ICollection<FloorPlanEdge> EdgesFrom { get; set; } = new List<FloorPlanEdge>();
    public ICollection<FloorPlanEdge> EdgesTo { get; set; } = new List<FloorPlanEdge>();
    // Cross-building walking links - only meaningful for Entrance nodes.
    public ICollection<EntranceConnection> Connections { get; set; } = new List<EntranceConnection>();
}
