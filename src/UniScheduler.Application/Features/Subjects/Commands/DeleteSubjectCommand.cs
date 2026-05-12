using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Subjects.Commands;

public record DeleteSubjectCommand(Guid Id) : IRequest;

public class DeleteSubjectCommandHandler : IRequestHandler<DeleteSubjectCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteSubjectCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Subject), request.Id);
        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
