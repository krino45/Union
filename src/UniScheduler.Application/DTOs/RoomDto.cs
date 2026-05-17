using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record RoomDto(
    Guid Id,
    Guid BuildingId,
    string BuildingShortCode,
    string Number,
    RoomType RoomType,
    int Capacity,
    bool HasProjector,
    bool HasComputers,
    bool HasLab,
    bool IsOnline,
    int Floor,
    int DistanceFromStairsMeters,
    List<LessonType> AllowedLessonTypes
);
