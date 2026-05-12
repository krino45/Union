using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record StudentGroupDto(Guid Id, string Name, int Year, string Specialty, int StudentCount, DegreeType DegreeType, Guid FacultyId, string FacultyName);
