using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record UpdateSolverSettingsCommand(SolverWeights Weights) : IRequest;

public class UpdateSolverSettingsCommandHandler : IRequestHandler<UpdateSolverSettingsCommand>
{
    private readonly IApplicationDbContext db;
    public UpdateSolverSettingsCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task Handle(UpdateSolverSettingsCommand request, CancellationToken cancellationToken)
    {
        var w = request.Weights;
        var s = await db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        if (s == null)
        {
            s = new SolverSettings { Id = Guid.Empty };
            db.SolverSettings.Add(s);
        }

        s.StudentWindow   = w.StudentWindow;
        s.TeacherWindow   = w.TeacherWindow;
        s.ActiveDay       = w.ActiveDay;
        s.SanPinOverload  = w.SanPinOverload;
        s.ConsecLecture   = w.ConsecLecture;
        s.ConsecSeminar   = w.ConsecSeminar;
        s.ConsecPractical = w.ConsecPractical;
        s.ConsecLab       = w.ConsecLab;
        s.EarlyPair       = w.EarlyPair;
        s.LatePair        = w.LatePair;
        s.ConsecRunScalar = w.ConsecRunScalar;
        s.SaturdayPenalty = w.SaturdayPenalty;
        s.DepartmentMismatchPenalty = w.DepartmentMismatchPenalty;
        s.WalkingPenaltyMax = w.WalkingPenaltyMax;

        await db.SaveChangesAsync(cancellationToken);
    }
}
