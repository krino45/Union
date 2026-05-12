using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class Building : Entity
{
    public string ShortCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    /// <summary>Equivalent walking distance (metres) per floor change via stairs. Default 20 m ≈ 15 s.</summary>
    public int StairsDistancePerFloor { get; set; } = 20;

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<BuildingDistance> DistancesFrom { get; set; } = new List<BuildingDistance>();
    public ICollection<BuildingDistance> DistancesTo { get; set; } = new List<BuildingDistance>();
}
