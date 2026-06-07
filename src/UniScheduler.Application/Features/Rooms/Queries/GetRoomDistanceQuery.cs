using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.Schedules;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Rooms.Queries;

public record RoomDistanceDto(
    bool Reachable,
    bool SameBuilding,
    int Meters,
    double WalkMinutes,
    string FromLabel,
    string ToLabel);

public record GetRoomDistanceQuery(Guid FromRoomId, Guid ToRoomId) : IRequest<RoomDistanceDto>;

public class GetRoomDistanceQueryHandler : IRequestHandler<GetRoomDistanceQuery, RoomDistanceDto>
{
    private readonly IApplicationDbContext _db;
    public GetRoomDistanceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<RoomDistanceDto> Handle(GetRoomDistanceQuery r, CancellationToken ct)
    {
        var rooms = await _db.Rooms.Include(rm => rm.Building).ToListAsync(ct);
        var from = rooms.FirstOrDefault(rm => rm.Id == r.FromRoomId)
            ?? throw new NotFoundException(nameof(Room), r.FromRoomId);
        var to = rooms.FirstOrDefault(rm => rm.Id == r.ToRoomId)
            ?? throw new NotFoundException(nameof(Room), r.ToRoomId);

        var nodes = await _db.FloorPlanNodes.ToListAsync(ct);
        var edges = await _db.FloorPlanEdges.ToListAsync(ct);
        var bldDists = await _db.BuildingDistances.ToListAsync(ct);
        var entranceConns = await _db.EntranceConnections.ToListAsync(ct);
        var pairSlots = await _db.PairTimeSlots.ToListAsync(ct);
        var settings = await _db.SolverSettings.FirstOrDefaultAsync(ct);
        var weights = settings == null ? new SolverWeights() : new SolverWeights(settings);

        var ctx = ScheduleScoreCalculator.BuildScoreContext(
            nodes, edges, bldDists, rooms, pairSlots, subjects: null, penalties: weights,
            entranceConnections: entranceConns);

        int meters = ScheduleScoreCalculator.RoomDistanceMeters(from.Id, to.Id, ctx);
        bool sameBuilding = from.BuildingId == to.BuildingId;
        bool reachable = meters < ScheduleScoreCalculator.UnreachableDistance;
        double walk = reachable ? Math.Round(meters / ScheduleScoreCalculator.WalkMetersPerMinute, 1) : 0;

        return new RoomDistanceDto(
            reachable, sameBuilding, reachable ? meters : 0, walk, Label(from), Label(to));
    }

    private static string Label(Room rm)
        => (rm.Building?.ShortCode is { Length: > 0 } code ? code + "-" : "") + rm.Number;
}
