using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Lns;

// Tunable knobs for a single optimization run. All time-based fields are wall-clock.
public record LnsOptions(
    TimeSpan TotalBudget,
    int KickTimeoutSeconds,
    int MaxIterations,
    int Seed = 12345,
    int MinDestroySize = 8,
    int TargetDestroySize = 40,
    // Late-acceptance history length. 1 == greedy hill-climb
    int LahcHistory = 20,
    // Run a space kick every Nth kick; the rest are time kicks. <=1 means space every kick.
    int SpaceKickEvery = 3);

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
    IReadOnlyDictionary<Guid, Guid> RoomToBuilding,
    IReadOnlyDictionary<Guid, IReadOnlySet<LessonType>>? RoomAllowedLessonTypes,
    IReadOnlyDictionary<Guid, IReadOnlySet<RussianDayOfWeek>>? GroupBlockedDays,
    IReadOnlySet<(Guid teacherId, RussianDayOfWeek day, int pair, int calWeek)>? TeacherBlockedSlots,
    ScoreBreakdown CurrentBreakdown,
    int TargetDestroySize,
    int MinDestroySize,
    Random Rng);

public interface IDestroyOperator
{
    string Name { get; }
    RepairAxis Axis { get; }
    HashSet<int> SelectToDestroy(LnsKickContext ctx);
}
