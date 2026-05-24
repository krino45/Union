using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.RescheduleRequests.Commands;

public record CreateRescheduleRequestCommand(
    Guid TeacherId, Guid OriginalEntryId, string Reason,
    RussianDayOfWeek? ProposedDayOfWeek, int? ProposedPairNumber, WeekType? ProposedWeekType,
    Guid? ProposedRoomId, bool ProposedIsOnline) : IRequest<RescheduleRequestDto>;

public record ApproveRescheduleRequestCommand(Guid RequestId, RussianDayOfWeek NewDay, int NewPair, WeekType NewWeekType, Guid? NewRoomId, bool NewIsOnline, string? Note) : IRequest;

public record RejectRescheduleRequestCommand(Guid RequestId, string Note) : IRequest;

public class CreateRescheduleRequestCommandHandler : IRequestHandler<CreateRescheduleRequestCommand, RescheduleRequestDto>
{
    private readonly IApplicationDbContext _db;
    public CreateRescheduleRequestCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RescheduleRequestDto> Handle(CreateRescheduleRequestCommand r, CancellationToken ct)
    {
        var teacher = await _db.Teachers.FindAsync(new object[] { r.TeacherId }, ct)
            ?? throw new NotFoundException(nameof(Teacher), r.TeacherId);

        var req = new RescheduleRequest
        {
            RequestedByTeacherId = r.TeacherId, OriginalEntryId = r.OriginalEntryId,
            Reason = r.Reason, ProposedDayOfWeek = r.ProposedDayOfWeek,
            ProposedPairNumber = r.ProposedPairNumber, ProposedWeekType = r.ProposedWeekType,
            ProposedRoomId = r.ProposedIsOnline ? null : r.ProposedRoomId, ProposedIsOnline = r.ProposedIsOnline,
            Status = RescheduleStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        _db.RescheduleRequests.Add(req);
        await _db.SaveChangesAsync(ct);
        return new RescheduleRequestDto(req.Id, req.RequestedByTeacherId, teacher.LastName + " " + teacher.FirstName,
            req.OriginalEntryId, null, req.ProposedDayOfWeek, req.ProposedPairNumber, req.ProposedWeekType,
            req.ProposedRoomId, null, req.ProposedIsOnline,
            req.Reason, req.Status, req.AdminNote, req.CreatedAt, req.ResolvedAt);
    }
}

public class ApproveRescheduleRequestCommandHandler : IRequestHandler<ApproveRescheduleRequestCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public ApproveRescheduleRequestCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task Handle(ApproveRescheduleRequestCommand r, CancellationToken ct)
    {
        var req = await _db.RescheduleRequests.FirstOrDefaultAsync(x => x.Id == r.RequestId, ct)
            ?? throw new NotFoundException(nameof(RescheduleRequest), r.RequestId);

        // Perform the actual move
        await _mediator.Send(new MoveEntryCommand(req.OriginalEntryId, r.NewDay, r.NewPair, r.NewWeekType, r.NewRoomId, r.NewIsOnline), ct);

        req.Status = RescheduleStatus.Approved;
        req.AdminNote = r.Note;
        req.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class RejectRescheduleRequestCommandHandler : IRequestHandler<RejectRescheduleRequestCommand>
{
    private readonly IApplicationDbContext _db;
    public RejectRescheduleRequestCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RejectRescheduleRequestCommand r, CancellationToken ct)
    {
        var req = await _db.RescheduleRequests.FirstOrDefaultAsync(x => x.Id == r.RequestId, ct)
            ?? throw new NotFoundException(nameof(RescheduleRequest), r.RequestId);
        req.Status = RescheduleStatus.Rejected;
        req.AdminNote = r.Note;
        req.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
