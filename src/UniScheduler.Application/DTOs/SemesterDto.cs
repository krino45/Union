namespace UniScheduler.Application.DTOs;

public record SemesterDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, int TotalWeeks);
