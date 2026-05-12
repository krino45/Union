using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record TeacherAvailabilityDto(
    Guid Id,
    Guid TeacherId,
    string TeacherName,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    WeekType WeekType,
    string? Reason,
    bool IsRecurring,
    DateOnly? ValidFrom,
    DateOnly? ValidTo
);
