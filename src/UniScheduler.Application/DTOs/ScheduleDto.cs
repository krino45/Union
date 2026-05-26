using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record ScheduleDto(
    Guid Id,
    int AcademicYear,
    Term Term,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? FacultyId,
    string? FacultyName,
    bool AllowCrossFacultyLessons,
    ScheduleStatus Status,
    DateTime? GeneratedAt,
    string? GenerationNotes,
    string? Name = null,
    Guid? OwnerUserId = null,
    string? OwnerUsername = null,
    bool IsOpenToAdmins = false,
    bool IsMine = false
);

public record ScheduleEntryDto(
    Guid Id,
    Guid ScheduleId,
    Guid SubjectId,
    string SubjectName,
    string SubjectShortName,
    Guid TeacherId,
    string TeacherDisplayName,
    Guid? RoomId,
    string? RoomNumber,
    string? BuildingShortCode,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    WeekType WeekType,
    LessonType LessonType,
    bool IsOnline,
    List<GroupRefDto> StudentGroups,
    Guid? ParallelGroupId = null,
    string? SubgroupLabel = null
);

public record GroupRefDto(Guid Id, string Name);

public record GenerateScheduleResult(
    bool Success,
    string Status,
    string? Message,
    int EntriesCreated
);
