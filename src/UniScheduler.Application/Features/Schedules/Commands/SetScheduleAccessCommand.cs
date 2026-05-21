using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record SetScheduleAccessCommand(Guid ScheduleId, bool IsOpenToAdmins) : IRequest;

public class SetScheduleAccessCommandHandler : IRequestHandler<SetScheduleAccessCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public SetScheduleAccessCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(SetScheduleAccessCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);

        if (schedule.Status != ScheduleStatus.Draft)
            throw new InvalidOperationException("Управление доступом доступно только для черновиков.");

        ScheduleAccessGuard.EnsureOwnerOnly(schedule, _user, "изменить доступ к черновику");

        schedule.IsOpenToAdmins = request.IsOpenToAdmins;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public record RenameScheduleCommand(Guid ScheduleId, string Name) : IRequest;

public class RenameScheduleCommandHandler : IRequestHandler<RenameScheduleCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public RenameScheduleCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task Handle(RenameScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);

        ScheduleAccessGuard.EnsureOwnerOnly(schedule, _user, "переименовать расписание");

        schedule.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        await _db.SaveChangesAsync(cancellationToken);
    }
}
