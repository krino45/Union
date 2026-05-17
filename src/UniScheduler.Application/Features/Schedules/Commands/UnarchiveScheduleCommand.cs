using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record UnarchiveScheduleCommand(Guid ScheduleId) : IRequest;

public class UnarchiveScheduleCommandHandler : IRequestHandler<UnarchiveScheduleCommand>
{
    private readonly IApplicationDbContext _db;
    public UnarchiveScheduleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UnarchiveScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);
        schedule.Status = ScheduleStatus.Draft;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
