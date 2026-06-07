using Microsoft.AspNetCore.Http;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Infrastructure.Auth;

public class CurrentUniversityService : ICurrentUniversityService
{
    private const string HeaderName = "X-University-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _override;

    public CurrentUniversityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UniversityId
    {
        get
        {
            if (_override.HasValue) return _override;
            var value = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool HasContext => UniversityId.HasValue;

    public void SetUniversity(Guid universityId) => _override = universityId;
}
