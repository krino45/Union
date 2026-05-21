using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.Users.Queries;

public record UserDto(Guid Id, string Username, string Role);

public record GetUsersQuery(string? Q = null) : IRequest<List<UserDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, List<UserDto>>
{
    private readonly IApplicationDbContext _db;
    public GetUsersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.AppUsers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(q));
        }
        return await query
            .OrderBy(u => u.Username)
            .Take(50)
            .Select(u => new UserDto(u.Id, u.Username, u.Role))
            .ToListAsync(cancellationToken);
    }
}
