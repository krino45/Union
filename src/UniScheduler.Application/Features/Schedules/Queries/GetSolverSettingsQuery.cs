using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;

namespace UniScheduler.Application.Features.Schedules.Queries;

public record GetSolverSettingsQuery : IRequest<SolverWeights>;

public class GetSolverSettingsQueryHandler : IRequestHandler<GetSolverSettingsQuery, SolverWeights>
{
    private readonly IApplicationDbContext db;
    public GetSolverSettingsQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<SolverWeights> Handle(GetSolverSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = await db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        return s == null ? new SolverWeights() : new SolverWeights(
            s.StudentWindow, s.TeacherWindow, s.ActiveDay, s.SanPinOverload,
            s.ConsecLecture, s.ConsecSeminar, s.ConsecPractical, s.ConsecLab,
            s.EarlyPair, s.MiddlePair, s.LatePair, s.ConsecRunScalar,
            s.SaturdayPenalty, s.DepartmentMismatchPenalty, s.WalkingPenaltyMax);
    }
}
