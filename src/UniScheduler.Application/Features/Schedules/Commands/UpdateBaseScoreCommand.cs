using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record UpdateBaseScoreCommand(Guid ScheduleId) : IRequest<int>;

public class UpdateBaseScoreCommandHandler : IRequestHandler<UpdateBaseScoreCommand, int>
{
    private readonly IApplicationDbContext _db;

    public UpdateBaseScoreCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<int> Handle(UpdateBaseScoreCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _db.Schedules.FindAsync(new object[] { request.ScheduleId }, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);

        var entries = await _db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(cancellationToken);

        var nodes = await _db.FloorPlanNodes.ToListAsync(cancellationToken);
        var edges = await _db.FloorPlanEdges.ToListAsync(cancellationToken);
        var bldDists = await _db.BuildingDistances.ToListAsync(cancellationToken);
        var rooms = await _db.Rooms.Include(r => r.Department).ToListAsync(cancellationToken);
        var pairSlots = await _db.PairTimeSlots.ToListAsync(cancellationToken);

        var subjectIds = entries.Select(e => e.SubjectId).Distinct().ToList();
        var subjects = await _db.Subjects.Include(s => s.Department)
            .Where(s => subjectIds.Contains(s.Id)).ToListAsync(cancellationToken);

        var settings = await _db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        var ctx = ScheduleScoreCalculator.BuildScoreContext(
            nodes, edges, bldDists, rooms, pairSlots, subjects, new SolverWeights(settings));
        var score = ScheduleScoreCalculator.Compute(entries, ctx);
        schedule.BaseScore = score;
        await _db.SaveChangesAsync(cancellationToken);
        return score;
    }
}
