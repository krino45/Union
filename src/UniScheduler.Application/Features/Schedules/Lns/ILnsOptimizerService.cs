using UniScheduler.Application.Features.Schedules.Internal;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Schedules.Lns;

public interface ILnsOptimizerService
{
    Task<LnsResult> OptimizeAsync(
        Guid scheduleId,
        IReadOnlyList<ScheduleEntry> seed,
        SharedData shared,
        LnsOptions opts,
        IProgress<string>? progress,
        CancellationToken ct);
}
