using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.StudentGroups.Queries;

public record GetStudentGroupsQuery(int? Year = null, Guid? FacultyId = null) : IRequest<List<StudentGroupDto>>;

public class GetStudentGroupsQueryHandler : IRequestHandler<GetStudentGroupsQuery, List<StudentGroupDto>>
{
    private readonly IApplicationDbContext db;

    public GetStudentGroupsQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<List<StudentGroupDto>> Handle(GetStudentGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = db.StudentGroups.Include(g => g.Faculty).AsQueryable();
        if (request.Year.HasValue) query = query.Where(g => g.Year == request.Year);
        if (request.FacultyId.HasValue) query = query.Where(g => g.FacultyId == request.FacultyId);
        return await query.OrderBy(g => g.Faculty.ShortCode).ThenBy(g => g.Year).ThenBy(g => g.Name)
            .Select(g => new StudentGroupDto(g.Id, g.Name, g.Year, g.Specialty, g.StudentCount, g.DegreeType, g.FacultyId, g.Faculty.Name))
            .ToListAsync(cancellationToken);
    }
}
