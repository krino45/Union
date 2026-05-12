using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Teachers.Commands;

public record DeleteTeacherCommand(Guid Id) : IRequest;

public class DeleteTeacherCommandHandler : IRequestHandler<DeleteTeacherCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteTeacherCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteTeacherCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.Id);
        _db.Teachers.Remove(teacher);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
