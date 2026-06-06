using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record UpdateSolverSettingsCommand(SolverWeights Weights) : IRequest;

public class UpdateSolverSettingsCommandHandler : IRequestHandler<UpdateSolverSettingsCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUniversityService _currentUniversity;

    public UpdateSolverSettingsCommandHandler(IApplicationDbContext db, ICurrentUniversityService currentUniversity)
    {
        _db = db;
        _currentUniversity = currentUniversity;
    }

    public async Task Handle(UpdateSolverSettingsCommand request, CancellationToken cancellationToken)
    {
        var w = request.Weights;
        var s = await _db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        if (s == null)
        {
            s = new SolverSettings();
            if (_currentUniversity.HasContext)
                s.UniversityId = _currentUniversity.UniversityId!.Value;
            _db.SolverSettings.Add(s);
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
        s.MiddlePair      = w.MiddlePair;
        s.LatePair        = w.LatePair;
        s.ConsecRunScalar = w.ConsecRunScalar;
        s.SaturdayPenalty = w.SaturdayPenalty;
        s.DepartmentMismatchPenalty = w.DepartmentMismatchPenalty;
        s.WalkingPenaltyMax = w.WalkingPenaltyMax;
        s.StairFloorMeters = w.StairFloorMeters;
        s.MaxPePerDay = w.MaxPePerDay;
        s.PeNotLastPenalty = w.PeNotLastPenalty;
        s.PeConsecutiveReward = w.PeConsecutiveReward;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
