using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Faculties.Commands;

public record DeleteFacultyCommand(Guid Id) : IRequest;

public class DeleteFacultyCommandHandler : IRequestHandler<DeleteFacultyCommand>
{
    private readonly IApplicationDbContext db;

    public DeleteFacultyCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task Handle(DeleteFacultyCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Faculty), request.Id);
        db.Faculties.Remove(faculty);
        await db.SaveChangesAsync(cancellationToken);
    }
}
