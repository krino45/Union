using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Faculties.Queries;

public record GetFacultiesQuery : IRequest<List<FacultyDto>>;

public class GetFacultiesQueryHandler : IRequestHandler<GetFacultiesQuery, List<FacultyDto>>
{
    private readonly IApplicationDbContext db;

    public GetFacultiesQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<List<FacultyDto>> Handle(GetFacultiesQuery request, CancellationToken cancellationToken)
        => await db.Faculties
            .OrderBy(f => f.ShortCode)
            .Select(f => new FacultyDto(f.Id, f.Name, f.ShortCode))
            .ToListAsync(cancellationToken);
}
