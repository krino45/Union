using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record SubjectDto(
    Guid Id,
    string Name,
    string ShortName,
    int AcademicYear,
    Term Term,
    double LectureHoursPerWeek,
    double PracticalHoursPerWeek,
    double LabHoursPerWeek,
    WeekType LectureWeekType,
    WeekType PracticalWeekType,
    WeekType LabWeekType
);
