using MediatR;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Faculties.Commands;

public record CreateFacultyCommand(string Name, string ShortCode) : IRequest<FacultyDto>;

public class CreateFacultyCommandHandler : IRequestHandler<CreateFacultyCommand, FacultyDto>
{
    private readonly IApplicationDbContext db;

    public CreateFacultyCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<FacultyDto> Handle(CreateFacultyCommand request, CancellationToken cancellationToken)
    {
        var faculty = new Faculty { Name = request.Name, ShortCode = request.ShortCode };
        db.Faculties.Add(faculty);
        await db.SaveChangesAsync(cancellationToken);
        return new FacultyDto(faculty.Id, faculty.Name, faculty.ShortCode);
    }
}
