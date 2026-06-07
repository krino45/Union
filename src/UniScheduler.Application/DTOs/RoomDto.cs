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
    bool IsOnline,
    int Floor,
    List<LessonType> AllowedLessonTypes,
    bool IsEnabled,
    Guid? DepartmentId,
    string? DepartmentName,
    int UtilizationPercent = 0
);
