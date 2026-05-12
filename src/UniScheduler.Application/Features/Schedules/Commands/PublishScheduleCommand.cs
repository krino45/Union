using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record PublishScheduleCommand(Guid ScheduleId) : IRequest;

public class PublishScheduleCommandHandler : IRequestHandler<PublishScheduleCommand>
{
    private readonly IApplicationDbContext _db;
    public PublishScheduleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(PublishScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);
        schedule.Status = ScheduleStatus.Published;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
