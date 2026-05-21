using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class Building : Entity
{
    public Guid UniversityId { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int NumberOfFloors { get; set; } = 5;
    public int NumberOfBasementFloors { get; set; } = 0;

    public University University { get; set; } = null!;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<BuildingDistance> DistancesFrom { get; set; } = new List<BuildingDistance>();
    public ICollection<BuildingDistance> DistancesTo { get; set; } = new List<BuildingDistance>();
    public ICollection<FloorPlanNode> FloorPlanNodes { get; set; } = new List<FloorPlanNode>();
    public ICollection<FloorPlanEdge> FloorPlanEdges { get; set; } = new List<FloorPlanEdge>();
    public ICollection<FloorPlan> FloorPlans { get; set; } = new List<FloorPlan>();
}
