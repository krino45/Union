using UniScheduler.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record DeleteScheduleCommand(Guid ScheduleId) : IRequest;

public class DeleteScheduleCommandHandler : IRequestHandler<DeleteScheduleCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public DeleteScheduleCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(DeleteScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);

        if (schedule.Status == ScheduleStatus.Published)
            throw new InvalidOperationException("Cannot delete a published schedule. Archive it first.");

        if (schedule.Status == ScheduleStatus.Draft)
            ScheduleAccessGuard.EnsureOwnerOnly(schedule, _user, "удалить черновик");

        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
