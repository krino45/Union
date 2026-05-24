using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record EntranceConnectionDto(Guid ToBuildingId, int DistanceMeters);

public record FloorPlanNodeDto(
    Guid Id,
    Guid BuildingId,
    int Floor,
    double X,
    double Y,
    FloorPlanNodeType NodeType,
    Guid? RoomId,
    string? Label,
    List<EntranceConnectionDto> Connections
);

public record FloorPlanEdgeDto(
    Guid Id,
    Guid BuildingId,
    Guid FromNodeId,
    Guid ToNodeId,
    int DistanceMeters
);

public record FloorPlanDto(
    Guid BuildingId,
    List<FloorPlanNodeDto> Nodes,
    List<FloorPlanEdgeDto> Edges
);

public record SaveFloorPlanRequest(
    List<SaveFloorPlanNodeRequest> Nodes,
    List<SaveFloorPlanEdgeRequest> Edges
);

public record SaveFloorPlanNodeRequest(
    Guid Id,
    int Floor,
    double X,
    double Y,
    FloorPlanNodeType NodeType,
    Guid? RoomId,
    string? Label,
    List<SaveEntranceConnectionRequest>? Connections = null
);

public record SaveEntranceConnectionRequest(Guid ToBuildingId, int DistanceMeters);

public record SaveFloorPlanEdgeRequest(
    Guid FromNodeId,
    Guid ToNodeId,
    int DistanceMeters
);
