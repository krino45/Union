using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateToken(AppUser user);
}
