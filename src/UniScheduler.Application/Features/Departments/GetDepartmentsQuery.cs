using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Departments;

public record GetDepartmentsQuery(Guid? FacultyId = null) : IRequest<List<DepartmentDto>>;

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, List<DepartmentDto>>
{
    private readonly IApplicationDbContext db;
    public GetDepartmentsQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<List<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Departments.Include(d => d.Faculty).AsQueryable();
        if (request.FacultyId.HasValue) query = query.Where(d => d.FacultyId == request.FacultyId);
        var depts = await query.OrderBy(d => d.Faculty.ShortCode).ThenBy(d => d.Name).ToListAsync(cancellationToken);
        return depts.Select(d => new DepartmentDto(d.Id, d.Name, d.ShortCode, d.FacultyId, d.Faculty.Name)).ToList();
    }
}
