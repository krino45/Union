namespace UniScheduler.Application.DTOs;

public record BuildingDto(Guid Id, string ShortCode, string Address, int StairsDistancePerFloor);

public record BuildingDistanceDto(Guid FromBuildingId, Guid ToBuildingId, int DistanceMeters, double WalkingMinutes, bool ExceedsPairBreak);

public record SetBuildingDistanceRequest(Guid FromBuildingId, Guid ToBuildingId, int DistanceMeters);
