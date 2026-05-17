using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record StudyPlanDto(
    Guid Id,
    string Name,
    int AcademicYear,
    Term Term,
    Guid? FacultyId,
    string? FacultyName,
    Guid? CalendarPlanId,
    string? CalendarPlanName,
    List<StudyPlanGroupDto> Groups,
    List<StudyPlanEntryDto> Entries
);

public record StudyPlanGroupDto(Guid StudentGroupId, string GroupName);

public record StudyPlanEntryDto(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    string SubjectShortName,
    double LectureHours,
    double PracticalHours,
    double LabHours,
    double SeminarHours,
    double ThesisHours
);

public record CalendarPlanDto(
    Guid Id,
    string Name,
    int AcademicYear,
    Term Term,
    List<CalendarWeekDto> Weeks
);

public record CalendarWeekDto(
    Guid Id,
    DateOnly StartDate,
    WeekKind Kind,
    string? Note
);

// ── Upsert inputs ─────────────────────────────────────────────────────────────

public record UpsertStudyPlanDto(
    string Name,
    int AcademicYear,
    Term Term,
    Guid? FacultyId,
    Guid? CalendarPlanId,
    List<Guid> GroupIds,
    List<UpsertStudyPlanEntryDto> Entries
);

public record UpsertStudyPlanEntryDto(
    Guid SubjectId,
    double LectureHours,
    double PracticalHours,
    double LabHours,
    double SeminarHours,
    double ThesisHours
);

public record UpsertCalendarPlanDto(
    string Name,
    int AcademicYear,
    Term Term,
    List<UpsertCalendarWeekDto> Weeks
);

public record UpsertCalendarWeekDto(
    DateOnly StartDate,
    WeekKind Kind,
    string? Note
);
