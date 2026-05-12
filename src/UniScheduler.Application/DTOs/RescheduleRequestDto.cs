using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record RescheduleRequestDto(
    Guid Id,
    Guid RequestedByTeacherId,
    string TeacherName,
    Guid OriginalEntryId,
    RussianDayOfWeek? ProposedDayOfWeek,
    int? ProposedPairNumber,
    WeekType? ProposedWeekType,
    string Reason,
    RescheduleStatus Status,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);
