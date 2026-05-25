using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.IntegrationTests.Helpers;

/// <summary>A SuperAdmin current-user stub so access guards short-circuit in handler tests.</summary>
public sealed class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; init; } = Guid.NewGuid();
    public string? Role { get; init; } = "SuperAdmin";
    public bool IsAdmin { get; init; } = true;
    public bool IsSuperAdmin { get; init; } = true;
}

/// <summary>No-context university stub so global query filters pass everything in handler tests.</summary>
public sealed class FakeCurrentUniversity : ICurrentUniversityService
{
    public Guid? UniversityId { get; init; }
    public bool HasContext { get; init; }
}
