using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record SubjectDto(
    Guid Id,
    string Name,
    string ShortName,
    int AcademicYear,
    Term Term,
    Guid? DepartmentId,
    string? DepartmentName,
    bool AllowsSubgroups = false,
    int SubgroupCount = 2
);
