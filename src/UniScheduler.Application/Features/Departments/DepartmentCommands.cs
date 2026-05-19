using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Departments;

public record CreateDepartmentCommand(string Name, string ShortCode, Guid FacultyId) : IRequest<DepartmentDto>;
public record UpdateDepartmentCommand(Guid Id, string Name, string ShortCode, Guid FacultyId) : IRequest<DepartmentDto>;
public record DeleteDepartmentCommand(Guid Id) : IRequest;

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IApplicationDbContext db;
    public CreateDepartmentCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<DepartmentDto> Handle(CreateDepartmentCommand r, CancellationToken cancellationToken)
    {
        var dept = new Department { Name = r.Name, ShortCode = r.ShortCode, FacultyId = r.FacultyId };
        db.Departments.Add(dept);
        await db.SaveChangesAsync(cancellationToken);
        var faculty = await db.Faculties.FindAsync(new object[] { r.FacultyId }, cancellationToken);
        return new DepartmentDto(dept.Id, dept.Name, dept.ShortCode, dept.FacultyId, faculty?.Name ?? string.Empty);
    }
}

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IApplicationDbContext db;
    public UpdateDepartmentCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand r, CancellationToken cancellationToken)
    {
        var dept = await db.Departments.Include(d => d.Faculty).FirstOrDefaultAsync(x => x.Id == r.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), r.Id);
        dept.Name = r.Name; dept.ShortCode = r.ShortCode; dept.FacultyId = r.FacultyId;
        await db.SaveChangesAsync(cancellationToken);
        return new DepartmentDto(dept.Id, dept.Name, dept.ShortCode, dept.FacultyId, dept.Faculty.Name);
    }
}

public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand>
{
    private readonly IApplicationDbContext db;
    public DeleteDepartmentCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.Id);
        db.Departments.Remove(dept);
        await db.SaveChangesAsync(cancellationToken);
    }
}
