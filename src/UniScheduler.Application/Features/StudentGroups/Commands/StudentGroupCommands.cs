using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.StudentGroups.Commands;

public record CreateStudentGroupCommand(string Name, int Year, string Specialty, int StudentCount, DegreeType DegreeType, Guid FacultyId) : IRequest<StudentGroupDto>;
public record UpdateStudentGroupCommand(Guid Id, string Name, int Year, string Specialty, int StudentCount, DegreeType DegreeType, Guid FacultyId) : IRequest<StudentGroupDto>;
public record DeleteStudentGroupCommand(Guid Id) : IRequest;

public class CreateStudentGroupCommandHandler : IRequestHandler<CreateStudentGroupCommand, StudentGroupDto>
{
    private readonly IApplicationDbContext db;

    public CreateStudentGroupCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<StudentGroupDto> Handle(CreateStudentGroupCommand r, CancellationToken cancellationToken)
    {
        var group = new StudentGroup
        {
            Name = r.Name, Year = r.Year, Specialty = r.Specialty,
            StudentCount = r.StudentCount, DegreeType = r.DegreeType, FacultyId = r.FacultyId
        };
        db.StudentGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        var faculty = await db.Faculties.FindAsync(new object[] { r.FacultyId }, cancellationToken);
        return new StudentGroupDto(group.Id, group.Name, group.Year, group.Specialty, group.StudentCount, group.DegreeType, group.FacultyId, faculty?.Name ?? string.Empty);
    }
}

public class UpdateStudentGroupCommandHandler : IRequestHandler<UpdateStudentGroupCommand, StudentGroupDto>
{
    private readonly IApplicationDbContext db;

    public UpdateStudentGroupCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<StudentGroupDto> Handle(UpdateStudentGroupCommand r, CancellationToken cancellationToken)
    {
        var group = await db.StudentGroups.Include(g => g.Faculty).FirstOrDefaultAsync(x => x.Id == r.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(StudentGroup), r.Id);
        group.Name = r.Name; group.Year = r.Year; group.Specialty = r.Specialty;
        group.StudentCount = r.StudentCount; group.DegreeType = r.DegreeType; group.FacultyId = r.FacultyId;
        await db.SaveChangesAsync(cancellationToken);
        return new StudentGroupDto(group.Id, group.Name, group.Year, group.Specialty, group.StudentCount, group.DegreeType, group.FacultyId, group.Faculty.Name);
    }
}

public class DeleteStudentGroupCommandHandler : IRequestHandler<DeleteStudentGroupCommand>
{
    private readonly IApplicationDbContext db;

    public DeleteStudentGroupCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task Handle(DeleteStudentGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await db.StudentGroups.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(StudentGroup), request.Id);
        db.StudentGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
    }
}
