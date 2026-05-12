using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Faculties.Commands;

public record UpdateFacultyCommand(Guid Id, string Name, string ShortCode) : IRequest<FacultyDto>;

public class UpdateFacultyCommandHandler : IRequestHandler<UpdateFacultyCommand, FacultyDto>
{
    private readonly IApplicationDbContext db;

    public UpdateFacultyCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<FacultyDto> Handle(UpdateFacultyCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Faculty), request.Id);
        faculty.Name = request.Name;
        faculty.ShortCode = request.ShortCode;
        await db.SaveChangesAsync(cancellationToken);
        return new FacultyDto(faculty.Id, faculty.Name, faculty.ShortCode);
    }
}
