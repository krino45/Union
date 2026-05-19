namespace UniScheduler.Application.DTOs;

public record DepartmentDto(Guid Id, string Name, string ShortCode, Guid FacultyId, string FacultyName);
