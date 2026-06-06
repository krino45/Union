using Microsoft.Extensions.Logging;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.Schedules.Internal;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Lns;

public class LnsOptimizerService : ILnsOptimizerService
{
    private readonly ISchedulerService scheduler;
    private readonly ILogger<LnsOptimizerService> logger;

    public LnsOptimizerService(ISchedulerService scheduler, ILogger<LnsOptimizerService> logger)
    {
        this.scheduler = scheduler;
        this.logger = logger;
    }

    public async Task<LnsResult> OptimizeAsync(
        Guid scheduleId,
        IReadOnlyList<ScheduleEntry> seed,
        SharedData shared,
        LnsOptions opts,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var rng = new Random(opts.Seed);

        // 1. Rebuild requirements deterministically and try to match each incumbent entry to its ri.
        var (reqs, riToPlanId) = ScheduleRequirementBuilder.BuildAllRequirementsStable(shared);
        var (entryByRi, riByEntryId, unmatchedEntries, unmatchedReqs) = MatchIncumbent(reqs, seed);

        var beforeBreakdown = ScheduleScoreCalculator.ComputeBreakdown(seed, shared.ScoreCtx);

        // Coverage gate — if the seed and the rebuilt requirement set drift too far, polish is
        // not the right tool; the user should rerun the seed pass. Skip cleanly.
        double coverage = reqs.Count == 0 ? 1.0 : (double)entryByRi.Count / reqs.Count;
        if (coverage < 0.85)
        {
            progress?.Report($"LNS: пропуск — низкое совпадение требований ({entryByRi.Count}/{reqs.Count}); перегенерируйте план.");
            return new LnsResult(seed, beforeBreakdown, beforeBreakdown, 0, 0,
                new Dictionary<string, OperatorTelemetry>());
        }

        progress?.Report($"LNS: матчинг {entryByRi.Count}/{reqs.Count} (исх. оценка {beforeBreakdown.Total})");

        // 2. Build parallel-sibling index — destroy sets get expanded to include all siblings.
        var siblingsByRi = BuildParallelSiblings(reqs);

        // 3. Operator bag.
        var operators = new IDestroyOperator[]
        {
            new DestroyWorstK(shared.Weights),
            new DestroyTeacherWeek(),
            new DestroyDay(),
            new DestroyPlan(),
            new DestroyRandomK(),
        };
        var weights = operators.ToDictionary(o => o.Name, _ => 1.0);
        var telemetry = operators.ToDictionary(o => o.Name, _ => (Attempts: 0, Accepted: 0, Failed: 0, Total: 0L));

        // 4. Main loop.
        var incumbent = seed.ToList();
        var currentBreakdown = beforeBreakdown;

        // Best-ever snapshot — LAHC lets the incumbent drift uphill, so the result must return the
        // best schedule we ever saw, not the last accepted one.
        var bestEntries = seed.ToList();
        var bestBreakdown = beforeBreakdown;
        long bestScore = beforeBreakdown.Total;

        // Late-acceptance history: accept a candidate if it beats the current cost OR the cost from L kicks ago
        long currentCost = beforeBreakdown.Total;
        int lahcL = Math.Max(1, opts.LahcHistory);
        var lahcHistory = new long[lahcL];
        Array.Fill(lahcHistory, currentCost);

        var deadline = DateTime.UtcNow + opts.TotalBudget;
        int kicks = 0, accepted = 0;

        while (DateTime.UtcNow < deadline && kicks < opts.MaxIterations && !ct.IsCancellationRequested)
        {
            var op = PickOperator(operators, weights, rng);
            int attemptNum = kicks + 1;
            var kickPrefix = $"LNS k={attemptNum}/{opts.MaxIterations} op={op.Name}";
            var kickProgress = progress == null
                ? null
                : (IProgress<string>)new Progress<string>(s => progress.Report($"{kickPrefix} | {s}"));

            var kickCtx = new LnsKickContext(
                Incumbent: incumbent,
                EntryByRi: entryByRi,
                RiByEntryId: riByEntryId,
                Requirements: reqs,
                RiToPlanId: riToPlanId,
                CurrentBreakdown: currentBreakdown,
                TargetDestroySize: opts.TargetDestroySize,
                MinDestroySize: opts.MinDestroySize,
                Rng: rng);

            var destroy = op.SelectToDestroy(kickCtx);
            // Expand to parallel siblings to keep H_par satisfiable.
            ExpandToSiblings(destroy, siblingsByRi);
            // Always also include unmatched reqs so the solve produces full schedules.
            foreach (var ri in unmatchedReqs) destroy.Add(ri);

            if (destroy.Count == 0)
            {
                MarkFailed(telemetry, op.Name);
                kicks++;
                progress?.Report($"{kickPrefix} | пустое разрушение, пропуск (score={currentBreakdown.Total}, best={bestScore})");
                continue;
            }

            // 5. Build pins for ri NOT in destroy, hints for ri that ARE in destroy
            var pins = new List<SchedulerPin>(reqs.Count - destroy.Count);
            var hints = new List<SchedulerHint>(destroy.Count);
            foreach (var (ri, entry) in entryByRi)
            {
                if (destroy.Contains(ri))
                {
                    if (!entry.RoomId.HasValue) continue;
                    hints.Add(new SchedulerHint(ri, entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.RoomId.Value));
                }
                else
                {
                    if (!entry.RoomId.HasValue) continue;      // online entries have no room — skip pin
                    pins.Add(new SchedulerPin(ri, entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.RoomId.Value));
                }
            }

            // 6. Call solver as repair. roomBlocks / teacherBlocks empty (everything is in the
            //    pinned-or-freed model; cross-tenant blocks shouldn't apply since we own the whole
            //    schedule slice in LNS).
            var input = ScheduleBuildContext.BuildSchedulerInputForPlan(
                scheduleId, shared, reqs,
                roomBlocks: Array.Empty<SchedulerRoomBlock>(),
                extraTeacherBlocks: Array.Empty<SchedulerBlock>(),
                timeoutSeconds: opts.KickTimeoutSeconds,
                weights: shared.Weights,
                pinnings: pins,
                hints: hints,
                isRepairSolve: true,
                skipTravel: true);

            SchedulerOutput? output;
            try
            {
                output = await scheduler.SolveAsync(input, ct, kickProgress);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LNS kick {Kick} failed in solver ({Op})", attemptNum, op.Name);
                MarkFailed(telemetry, op.Name);
                kicks++;
                progress?.Report($"{kickPrefix} | исключение в решателе: {ex.Message}");
                continue;
            }

            kicks++;

            if (output.Status != SolverStatus.Feasible && output.Status != SolverStatus.Optimal)
            {
                MarkFailed(telemetry, op.Name);
                progress?.Report($"{kickPrefix} | решатель: {output.Status} (best {bestScore})");
                continue;
            }

            // 7. Convert assignments to entries, score, accept/reject via LAHC.
            var candidate = AssignmentsToEntries(output.Assignments, reqs, scheduleId, incumbent);
            var newBreakdown = ScheduleScoreCalculator.ComputeBreakdown(candidate, shared.ScoreCtx);

            long candCost = newBreakdown.Total;
            int vIdx = kicks % lahcL;
            bool acceptThis = candCost <= currentCost || candCost <= lahcHistory[vIdx];

            if (acceptThis)
            {
                long prevBest = bestScore;
                incumbent = candidate;
                currentBreakdown = newBreakdown;
                currentCost = candCost;
                // Incumbent changed — rebuild the ri-entry map so the next kick pins/hints correctly.
                (entryByRi, riByEntryId, _, _) = MatchIncumbent(reqs, incumbent);

                if (candCost < bestScore)
                {
                    bestScore = candCost;
                    bestEntries = candidate;
                    bestBreakdown = newBreakdown;
                    accepted++;
                    UpdateWeights(weights, op.Name, prevBest - candCost);
                    MarkAccepted(telemetry, op.Name, prevBest - candCost);
                }
                else
                {
                    MarkAccepted(telemetry, op.Name, 0);
                }
                progress?.Report($"{kickPrefix} | принято score={currentCost} (best {bestScore})");
            }
            else
            {
                DecayWeight(weights, op.Name);
                MarkRejected(telemetry, op.Name);
                progress?.Report($"{kickPrefix} | отклонено score={candCost} (best {bestScore})");
            }

            if (currentCost < lahcHistory[vIdx]) lahcHistory[vIdx] = currentCost;
        }

        var perOp = telemetry.ToDictionary(kv => kv.Key,
            kv => new OperatorTelemetry(kv.Value.Attempts, kv.Value.Accepted, kv.Value.Failed, kv.Value.Total));
        return new LnsResult(bestEntries, beforeBreakdown, bestBreakdown, kicks, accepted, perOp);
    }

    private static IDestroyOperator PickOperator(IDestroyOperator[] ops, Dictionary<string, double> weights, Random rng)
    {
        double total = 0;
        foreach (var o in ops) total += Math.Max(0.01, weights[o.Name]);
        double pick = rng.NextDouble() * total;
        double cum = 0;
        foreach (var o in ops)
        {
            cum += Math.Max(0.01, weights[o.Name]);
            if (pick <= cum) return o;
        }
        return ops[^1];
    }

    private static void UpdateWeights(Dictionary<string, double> weights, string name, long improvement)
    {
        // Reward by sqrt(improvement) so a single huge improvement doesn't dominate forever.
        double bonus = improvement > 0 ? Math.Sqrt(improvement) : 0;
        weights[name] = weights[name] * 0.9 + (1.0 + bonus) * 0.1;
    }

    private static void DecayWeight(Dictionary<string, double> weights, string name)
    {
        weights[name] = Math.Max(0.05, weights[name] * 0.97);
    }

    private static (Dictionary<int, ScheduleEntry>, Dictionary<Guid, int>, List<ScheduleEntry>, HashSet<int>)
        MatchIncumbent(IReadOnlyList<SchedulerRequirement> reqs, IReadOnlyList<ScheduleEntry> entries)
    {
        // Natural key buckets — within each bucket, walk pairwise in stable order.
        var reqsByKey = reqs.GroupBy(r => NaturalKey(r))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Index).Select(r => r.Index).ToList());

        var entriesByKey = entries.GroupBy(e => NaturalKey(e))
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Id).ToList());

        var entryByRi = new Dictionary<int, ScheduleEntry>();
        var riByEntryId = new Dictionary<Guid, int>();
        var unmatched = new List<ScheduleEntry>();
        var matchedRis = new HashSet<int>();

        foreach (var (key, entriesInBucket) in entriesByKey)
        {
            if (!reqsByKey.TryGetValue(key, out var ris))
            {
                unmatched.AddRange(entriesInBucket);
                continue;
            }
            int n = Math.Min(ris.Count, entriesInBucket.Count);
            for (int i = 0; i < n; i++)
            {
                entryByRi[ris[i]] = entriesInBucket[i];
                riByEntryId[entriesInBucket[i].Id] = ris[i];
                matchedRis.Add(ris[i]);
            }
            for (int i = n; i < entriesInBucket.Count; i++) unmatched.Add(entriesInBucket[i]);
        }

        var unmatchedReqs = new HashSet<int>();
        for (int i = 0; i < reqs.Count; i++)
            if (!matchedRis.Contains(reqs[i].Index)) unmatchedReqs.Add(reqs[i].Index);

        return (entryByRi, riByEntryId, unmatched, unmatchedReqs);
    }

    private static string NaturalKey(SchedulerRequirement r)
    {
        var g = string.Join(",", r.GroupIds.OrderBy(x => x));
        return $"{r.SubjectId}|{r.TeacherId}|{r.LessonType}|{r.WeekType}|{g}|{r.SubgroupLabel ?? ""}|{(r.IsOnline ? 1 : 0)}";
    }

    private static string NaturalKey(ScheduleEntry e)
    {
        var g = string.Join(",", e.StudentGroups.Select(sg => sg.StudentGroupId).OrderBy(x => x));
        return $"{e.SubjectId}|{e.TeacherId}|{e.LessonType}|{e.WeekType}|{g}|{e.SubgroupLabel ?? ""}|{(e.IsOnline ? 1 : 0)}";
    }

    private static Dictionary<int, List<int>> BuildParallelSiblings(IReadOnlyList<SchedulerRequirement> reqs)
    {
        var byKey = new Dictionary<int, List<int>>();
        foreach (var r in reqs)
        {
            if (r.ParallelKey is not int pk) continue;
            if (!byKey.TryGetValue(pk, out var lst)) byKey[pk] = lst = new List<int>();
            lst.Add(r.Index);
        }
        var siblingsByRi = new Dictionary<int, List<int>>();
        foreach (var r in reqs)
        {
            if (r.ParallelKey is not int pk) continue;
            siblingsByRi[r.Index] = byKey[pk];
        }
        return siblingsByRi;
    }

    private static void ExpandToSiblings(HashSet<int> destroy, Dictionary<int, List<int>> siblingsByRi)
    {
        var toAdd = new List<int>();
        foreach (var ri in destroy)
            if (siblingsByRi.TryGetValue(ri, out var siblings))
                toAdd.AddRange(siblings);
        foreach (var s in toAdd) destroy.Add(s);
    }

    private static List<ScheduleEntry> AssignmentsToEntries(
        IReadOnlyList<SchedulerAssignment> assignments,
        IReadOnlyList<SchedulerRequirement> reqs,
        Guid scheduleId,
        IReadOnlyList<ScheduleEntry> incumbentForCarryOver)
    {
        var parallelGuids = new Dictionary<int, Guid>();
        var result = new List<ScheduleEntry>(assignments.Count);
        foreach (var a in assignments)
        {
            var req = reqs[a.RequirementIndex];
            Guid? parallelGroupId = null;
            if (req.ParallelKey is int pk)
            {
                if (!parallelGuids.TryGetValue(pk, out var g)) parallelGuids[pk] = g = Guid.NewGuid();
                parallelGroupId = g;
            }
            var entry = new ScheduleEntry
            {
                ScheduleId = scheduleId,
                SubjectId = req.SubjectId,
                TeacherId = req.TeacherId,
                RoomId = req.IsOnline ? null : a.RoomId,
                DayOfWeek = a.Day,
                PairNumber = a.PairNumber,
                WeekType = a.WeekType,
                LessonType = req.LessonType,
                IsOnline = req.IsOnline,
                ParallelGroupId = parallelGroupId,
                SubgroupLabel = req.SubgroupLabel
            };
            foreach (var gId in req.GroupIds)
                entry.StudentGroups.Add(new ScheduleEntryStudentGroup { StudentGroupId = gId, ScheduleEntry = entry });
            result.Add(entry);
        }
        return result;
    }

    private static void MarkAccepted(Dictionary<string, (int Attempts, int Accepted, int Failed, long Total)> tel,
        string name, long improvement)
    {
        var t = tel[name];
        tel[name] = (t.Attempts + 1, t.Accepted + 1, t.Failed, t.Total + Math.Max(0, improvement));
    }

    private static void MarkRejected(Dictionary<string, (int Attempts, int Accepted, int Failed, long Total)> tel, string name)
    {
        var t = tel[name];
        tel[name] = (t.Attempts + 1, t.Accepted, t.Failed, t.Total);
    }

    private static void MarkFailed(Dictionary<string, (int Attempts, int Accepted, int Failed, long Total)> tel, string name)
    {
        var t = tel[name];
        tel[name] = (t.Attempts + 1, t.Accepted, t.Failed + 1, t.Total);
    }
}
