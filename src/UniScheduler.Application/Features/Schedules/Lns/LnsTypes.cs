using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Schedules.Lns;

// Tunable knobs for a single optimization run. All time-based fields are wall-clock.
public record LnsOptions(
    TimeSpan TotalBudget,
    int KickTimeoutSeconds,
    int MaxIterations,
    int Seed = 12345,
    // Minimum number of requirement indices a destroy operator is asked to free.
    // Most operators clamp their natural selection to at least this many.
    int MinDestroySize = 8,
    // Soft target for destroy size. Operators may exceed when their natural unit is bigger
    // (a teacher's full week, all reqs of a parallel group, etc.).
    int TargetDestroySize = 30);

public record LnsResult(
    IReadOnlyList<ScheduleEntry> Entries,
    ScoreBreakdown BeforeBreakdown,
    ScoreBreakdown AfterBreakdown,
    int TotalKicks,
    int AcceptedKicks,
    IReadOnlyDictionary<string, OperatorTelemetry> PerOperator)
{
    public bool AcceptedAny => AcceptedKicks > 0;
}

public record OperatorTelemetry(int Attempts, int Accepted, int Failed, long TotalImprovement);

// Per-kick context. Operators only see read-only views.
public record LnsKickContext(
    IReadOnlyList<ScheduleEntry> Incumbent,
    IReadOnlyDictionary<int, ScheduleEntry> EntryByRi,
    IReadOnlyDictionary<Guid, int> RiByEntryId,
    IReadOnlyList<SchedulerRequirement> Requirements,
    IReadOnlyDictionary<int, Guid> RiToPlanId,
    ScoreBreakdown CurrentBreakdown,
    int TargetDestroySize,
    int MinDestroySize,
    Random Rng);

public interface IDestroyOperator
{
    string Name { get; }
    HashSet<int> SelectToDestroy(LnsKickContext ctx);
}
