using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record DeleteEntryCommand(Guid EntryId) : IRequest;

public class DeleteEntryCommandHandler : IRequestHandler<DeleteEntryCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public DeleteEntryCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(DeleteEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _db.ScheduleEntries.FirstOrDefaultAsync(e => e.Id == request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduleEntry), request.EntryId);

        var schedule = await _db.Schedules.FindAsync(new object[] { entry.ScheduleId }, cancellationToken);
        if (schedule == null)
            throw new NotFoundException(nameof(Schedule), entry.ScheduleId);
        if (schedule.Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived schedule.");
        ScheduleAccessGuard.EnsureCanEdit(schedule, _user);
        if (schedule.Status == ScheduleStatus.Published)
        {
            schedule.Status = ScheduleStatus.Draft;
            ScheduleAccessGuard.TransferOwnershipOnDemote(schedule, _user);
        }

        _db.ScheduleEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
