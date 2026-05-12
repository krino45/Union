using UniScheduler.Application.Common.Models;

namespace UniScheduler.Application.Common.Interfaces;

public interface ISchedulerService
{
    Task<SchedulerOutput> SolveAsync(SchedulerInput input, CancellationToken cancellationToken = default);
}
