namespace UniScheduler.Domain.Entities;

// A walking link from a building's Entrance node to another building, in metres.
// This is the source of truth for inter-building distances; BuildingDistance is derived
// as the shortest such link between any two buildings. No row = no connection.
public class EntranceConnection
{
    public Guid FromNodeId { get; set; }     // the Entrance FloorPlanNode
    public Guid FromBuildingId { get; set; } // denormalised owner building (= FromNode.BuildingId)
    public Guid ToBuildingId { get; set; }   // the building this entrance reaches
    public int DistanceMeters { get; set; }

    public FloorPlanNode FromNode { get; set; } = null!;
    public Building FromBuilding { get; set; } = null!;
    public Building ToBuilding { get; set; } = null!;
}
