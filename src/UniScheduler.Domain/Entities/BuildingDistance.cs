namespace UniScheduler.Domain.Entities;

public class BuildingDistance
{
    public Guid FromBuildingId { get; set; }
    public Guid ToBuildingId { get; set; }
    public int DistanceMeters { get; set; }

    public Building FromBuilding { get; set; } = null!;
    public Building ToBuilding { get; set; } = null!;

    // Walking speed assumed 80 m/min; break between pairs is 10 min.
    public double WalkingMinutes => DistanceMeters / 80.0;
    public bool ExceedsPairBreak => WalkingMinutes > 10.0;
}
