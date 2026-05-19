namespace UniScheduler.Application.Common.Interfaces;

public interface ICurrentUniversityService
{
    Guid? UniversityId { get; }
    bool HasContext { get; }
}
