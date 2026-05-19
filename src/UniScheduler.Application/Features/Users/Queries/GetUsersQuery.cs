using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.Users.Queries;

public record UserDto(Guid Id, string Username, string Role);

public record GetUsersQuery : IRequest<List<UserDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, List<UserDto>>
{
    private readonly IApplicationDbContext _db;
    public GetUsersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        => await _db.AppUsers
            .OrderBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.Username, u.Role))
            .ToListAsync(cancellationToken);
}
