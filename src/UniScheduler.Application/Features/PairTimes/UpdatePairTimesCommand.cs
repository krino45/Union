using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.PairTimes;

public record UpdatePairTimesCommand(List<PairTimeDto> Pairs) : IRequest;

public class UpdatePairTimesCommandHandler : IRequestHandler<UpdatePairTimesCommand>
{
    private readonly IApplicationDbContext db;
    private readonly ICurrentUniversityService currentUniversity;

    public UpdatePairTimesCommandHandler(IApplicationDbContext db, ICurrentUniversityService currentUniversity)
    {
        this.db = db;
        this.currentUniversity = currentUniversity;
    }

    public async Task Handle(UpdatePairTimesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUniversity.HasContext)
            throw new ForbiddenException("Требуется выбрать университет.");
        var universityId = currentUniversity.UniversityId!.Value;

        var pairs = (request.Pairs ?? new List<PairTimeDto>())
            .Where(p => p.PairNumber > 0)
            .GroupBy(p => p.PairNumber)
            .Select(g => g.First())
            .OrderBy(p => p.PairNumber)
            .ToList();
        if (pairs.Count == 0)
            throw Invalid("Нужно задать хотя бы одну пару.");

        // db.PairTimeSlots is already scoped to the current university by the global query filter.
        var existing = await db.PairTimeSlots.ToListAsync(cancellationToken);
        db.PairTimeSlots.RemoveRange(existing);

        foreach (var p in pairs)
        {
            if (!TimeOnly.TryParse(p.StartTime, out var start) || !TimeOnly.TryParse(p.EndTime, out var end))
                throw Invalid($"Неверное время для пары {p.PairNumber}.");
            if (end <= start)
                throw Invalid($"Пара {p.PairNumber}: окончание должно быть позже начала.");

            db.PairTimeSlots.Add(new PairTimeSlot
            {
                UniversityId = universityId,
                PairNumber = p.PairNumber,
                StartTime = start,
                EndTime = end
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ValidationException Invalid(string message)
        => new(new[] { new ValidationFailure(nameof(UpdatePairTimesCommand.Pairs), message) });
}
