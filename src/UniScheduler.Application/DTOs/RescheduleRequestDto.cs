using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record RescheduleRequestDto(
    Guid Id,
    Guid RequestedByTeacherId,
    string TeacherName,
    Guid OriginalEntryId,
    string? OriginalEntryDescription,
    RussianDayOfWeek? ProposedDayOfWeek,
    int? ProposedPairNumber,
    WeekType? ProposedWeekType,
    Guid? ProposedRoomId,
    string? ProposedRoomName,
    bool ProposedIsOnline,
    string Reason,
    RescheduleStatus Status,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);
