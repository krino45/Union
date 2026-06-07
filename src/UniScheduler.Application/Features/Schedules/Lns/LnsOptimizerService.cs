using Microsoft.Extensions.Logging;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.Schedules.Internal;
using UniScheduler.Domain.Entities;

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

        var beforeBreakdown = ScheduleScoreCalculator.ComputeBreakdown(seed, shared.ScoreCtx);

        // Phase 0: feasibility repair.
        var feas = FeasibilityRepair.Resolve(seed, shared);
        if (feas.Before > 0 || feas.BlockedBefore > 0)
        {
            var conflictStr = feas.Before > 0 ? $"конфликтов {feas.Before-feas.After}/{feas.Before}, " : "";
            var blockStr = feas.BlockedBefore > 0 ? $"блокировок {feas.Before-feas.BlockedAfter}/{feas.BlockedBefore}" : "";

            progress?.Report($"Починка конфликтов: исправлено {conflictStr}{blockStr}");
            seed = feas.Entries;
        }
        var seedBreakdown = feas.ChangedAnything
            ? ScheduleScoreCalculator.ComputeBreakdown(seed, shared.ScoreCtx)
            : beforeBreakdown;

        // 1. Rebuild requirements deterministically and try to match each incumbent entry to its ri.
        var (reqs, riToPlanId) = ScheduleRequirementBuilder.BuildAllRequirementsStable(shared);
        var (entryByRi, riByEntryId, unmatchedEntries, unmatchedReqs) = MatchIncumbent(reqs, seed);

        // Recover parallel drift: language and PE pick the teacher with a load check we canat reproduce,
        // in a stable manner, so the rebuilt req has a different teacher than the real one.
        // Rebind those reqs to the actual teacher.
        int driftBefore = unmatchedEntries.Count;
        ReconcileParallelStreams(reqs, entryByRi, riByEntryId, unmatchedEntries, unmatchedReqs);
        if (driftBefore != unmatchedEntries.Count)
            progress?.Report($"LNS: согласовано {driftBefore - unmatchedEntries.Count} паралл. потоков по преподавателю");

        // Coverage gate - if the seed and the rebuilt requirement set drift too far, polish is
        // not the right tool; the user should rerun the seed pass. Skip cleanly (but keep the
        // feasibility-repaired entries, which are still an improvement).
        double coverage = reqs.Count == 0 ? 1.0 : (double)entryByRi.Count / reqs.Count;
        if (coverage < 0.85)
        {
            progress?.Report($"LNS: пропуск оптимизации — низкое совпадение требований ({entryByRi.Count}/{reqs.Count}); перегенерируйте план.");
            return new LnsResult(seed, beforeBreakdown, seedBreakdown, 0, 0,
                new Dictionary<string, OperatorTelemetry>());
        }

        progress?.Report($"LNS: матчинг {entryByRi.Count}/{reqs.Count} (исх. оценка {beforeBreakdown.Total})");

        // Diagnostic: what kind of entries fail to match a rebuilt requirement? If it's dominated by
        // multi-teacher subjects + language/PE, the cause is builder teacher↔group assignment drift
        // (BuildRequirementsForPlan vs BuildAllRequirementsStable), not user edits.
        if (unmatchedEntries.Count > 0)
        {
            var byType = unmatchedEntries.GroupBy(e => e.LessonType)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}={g.Count()}");
            progress?.Report($"LNS: не сопоставлено {unmatchedEntries.Count} занятий по типу — {string.Join(", ", byType)}");
        }

        // 2. Build parallel-sibling index - destroy sets get expanded to include all siblings.
        var siblingsByRi = BuildParallelSiblings(reqs);

        // Reqs the incumbent never placed (rebuild/match drift)
        var excludedReqs = new List<int>();
        foreach (var ri in unmatchedReqs)
        {
            if (reqs[ri].ParallelKey is int)
            {
                if (siblingsByRi[ri].All(s => unmatchedReqs.Contains(s))) excludedReqs.Add(ri);
            }
            else excludedReqs.Add(ri);
        }
        var excludedSet = excludedReqs.ToHashSet();

        var carryRoomBlocks = new List<SchedulerRoomBlock>();
        var carryTeacherBlocks = new List<SchedulerBlock>();
        var carryGroupBlocks = new List<SchedulerGroupBlock>();
        foreach (var e in unmatchedEntries)
        {
            carryTeacherBlocks.Add(new SchedulerBlock(e.TeacherId, e.DayOfWeek, e.PairNumber, e.WeekType));
            if (e.RoomId is { } rid && rid != SchedulerSentinels.OverflowRoomId)
                carryRoomBlocks.Add(new SchedulerRoomBlock(rid, e.DayOfWeek, e.PairNumber, e.WeekType));
            foreach (var sg in e.StudentGroups)
                carryGroupBlocks.Add(new SchedulerGroupBlock(sg.StudentGroupId, e.DayOfWeek, e.PairNumber, e.WeekType));
        }
        if (unmatchedEntries.Count > 0)
            progress?.Report($"LNS: {unmatchedEntries.Count} занятий вне модели зафиксированы как блокировки (комн./препод./группа)");

        // 3. Operator bags - alternated each kick. Time ops retime (rooms locked to own/overflow,
        //    SkipTravel); Space ops reroom (times locked, full distances on, overflow re-homed).
        var timeOps = new IDestroyOperator[]
        {
            new DestroyWorstK(shared.Weights),
            new DestroyTeacherWeek(),
            new DestroyDay(),
            new DestroyMultiDay(),
            new DestroyGroupWeek(),
            new DestroySubject(),
            new DestroyPlan(),
            new DestroyRandomK(),
        };
        var spaceOps = new IDestroyOperator[]
        {
            new DestroyRoomSpace(),
            new DestroyBuildingSpace(),
            new DestroyGroupDaySpace(),
            new DestroyGroupWeekSpace(),
            new DestroyWorstDistanceSpace(shared.Weights),
        };

        var wrongRoomOp = new DestroyWrongRoom();
        var blockedSlotOp = new DestroyBlockedSlot();
        var overflowOp = new DestroyOverflowRooms();
        var forcedOps = new IDestroyOperator[] { wrongRoomOp, blockedSlotOp };

        // Room -> building lookup for the building-local space op (entries only carry RoomId).
        var roomToBuilding = shared.Rooms.ToDictionary(r => r.Id, r => r.BuildingId);
        var allOps = timeOps.Concat(spaceOps).Concat(forcedOps).ToArray();
        var weights = allOps.ToDictionary(o => o.Name, _ => 1.0);
        var telemetry = allOps.ToDictionary(o => o.Name, _ => (Attempts: 0, Accepted: 0, Failed: 0, Total: 0L));

        var stuckOps = new HashSet<string>();
        var forcedCount = new Dictionary<string, int>();
        const int maxForcedTries = 6;

        // 4. Main loop.
        var incumbent = seed.ToList();
        var currentBreakdown = seedBreakdown;

        // Best-ever snapshot - LAHC lets the incumbent drift uphill, so the result must return the
        // best schedule we ever saw, not the last accepted one.
        var bestEntries = seed.ToList();
        var bestBreakdown = seedBreakdown;
        long bestScore = seedBreakdown.Total;

        // Late-acceptance history: accept a candidate if it beats the current cost OR the cost from L kicks ago
        long currentCost = seedBreakdown.Total;
        int lahcL = Math.Max(1, opts.LahcHistory);
        var lahcHistory = new long[lahcL];
        Array.Fill(lahcHistory, currentCost);

        var deadline = DateTime.UtcNow + opts.TotalBudget;
        int kicks = 0, accepted = 0;

        IDestroyOperator? forced = null;
        while (DateTime.UtcNow < deadline && kicks < opts.MaxIterations && !ct.IsCancellationRequested)
        {
            if (currentBreakdown.S11_RoomTypeMismatch > 0 && !stuckOps.Contains(wrongRoomOp.Name))
                forced = wrongRoomOp;
            else if (currentBreakdown.S12_BlockedPlacement > 0 && !stuckOps.Contains(blockedSlotOp.Name))
                forced = blockedSlotOp;
            else if (currentBreakdown.S10_Overflow > 0)
                forced = overflowOp;
            if (forced != null && (forcedCount[forced.Name] = forcedCount.GetValueOrDefault(forced.Name) + 1) > maxForcedTries)
            {
                stuckOps.Add(forced.Name);
                forced = null;
            }

            IDestroyOperator[] axisOps;
            IDestroyOperator op;
            if (forced != null)
            {
                axisOps = forced.Axis == RepairAxis.Space ? spaceOps : timeOps;
                op = forced;
                if (forced.Name == overflowOp.Name) forced = null; // one-time op
            }
            else
            {
                // Space kicks are mostly cleanup, so they run only every Nth kick.
                bool spaceKick = opts.SpaceKickEvery <= 1 || (kicks % opts.SpaceKickEvery) == opts.SpaceKickEvery - 1;
                axisOps = spaceKick ? spaceOps : timeOps;
                var untried = axisOps.Where(o => telemetry[o.Name].Attempts == 0).ToList();
                op = untried.Count > 0 ? untried[rng.Next(untried.Count)] : PickOperator(axisOps, weights, rng);
            }
            int kickTimeout = Math.Max(1, (int)Math.Round(opts.KickTimeoutSeconds * op.Factor));
            int attemptNum = kicks + 1;
            var kickPrefix = $"LNS k={attemptNum}/{opts.MaxIterations} op={op.Name}/{op.Axis} t={kickTimeout}s";
            IProgress<string>? kickProgress = null;

            var kickCtx = new LnsKickContext(
                Incumbent: incumbent,
                EntryByRi: entryByRi,
                RiByEntryId: riByEntryId,
                Requirements: reqs,
                RiToPlanId: riToPlanId,
                RoomToBuilding: roomToBuilding,
                RoomAllowedLessonTypes: shared.ScoreCtx.RoomAllowedLessonTypes,
                GroupBlockedDays: shared.ScoreCtx.GroupBlockedDays,
                TeacherBlockedSlots: shared.ScoreCtx.TeacherBlockedSlots,
                ScoreCtx: shared.ScoreCtx,
                CurrentBreakdown: currentBreakdown,
                TargetDestroySize: opts.TargetDestroySize,
                MinDestroySize: opts.MinDestroySize, Rng: rng);

            var destroy = op.SelectToDestroy(kickCtx);
            // Expand to parallel siblings to keep H_par satisfiable.
            ExpandToSiblings(destroy, siblingsByRi);
            foreach (var (ri, e) in entryByRi)
                if (e.RoomId == SchedulerSentinels.OverflowRoomId) destroy.Add(ri);
            destroy.ExceptWith(excludedSet);

            if (destroy.Count == 0)
            {
                MarkFailed(telemetry, op.Name);
                weights[op.Name] = Math.Max(MinWeight, weights[op.Name] * 0.2);
                string note = "";
                if (forced != null)
                {
                    stuckOps.Add(op.Name);
                    note = " — нарушение на занятии вне модели (не сопоставлено с требованием); полировка не может его передвинуть";
                }
                kicks++;
                progress?.Report($"{kickPrefix} | пустое разрушение, пропуск (score={currentBreakdown.Total}, best={bestScore}){note}");
                continue;
            }

            var pins = new List<SchedulerPin>(reqs.Count - destroy.Count);
            var hints = new List<SchedulerHint>(destroy.Count);
            var freed = new List<SchedulerFreedReq>(destroy.Count);
            foreach (var (ri, entry) in entryByRi)
            {
                if (!entry.RoomId.HasValue) continue; // online - no room anchor; stays fully free
                bool isOverflow = entry.RoomId.Value == SchedulerSentinels.OverflowRoomId;
                if (destroy.Contains(ri))
                {
                    freed.Add(new SchedulerFreedReq(ri, op.Axis, entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.RoomId.Value));
                    if (!isOverflow) // hinting a req back into the sentinel is pointless
                        hints.Add(new SchedulerHint(ri, entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.RoomId.Value));
                }
                else if (!isOverflow) // never pin to the sentinel (overflow is always in destroy anyway)
                {
                    pins.Add(new SchedulerPin(ri, entry.DayOfWeek, entry.PairNumber, entry.WeekType, entry.RoomId.Value));
                }
            }

            // 6. Call solver as repair.  Time kicks SkipTravel
            bool timeAxis = op.Axis == RepairAxis.Time;
            var input = ScheduleBuildContext.BuildSchedulerInputForPlan(
                scheduleId, shared, reqs,
                roomBlocks: carryRoomBlocks,
                extraTeacherBlocks: carryTeacherBlocks,
                timeoutSeconds: kickTimeout,
                weights: shared.Weights,
                pinnings: pins,
                hints: hints,
                isRepairSolve: true,
                skipTravel: timeAxis,
                freedReqs: freed,
                overflowPenalty: SchedulerSentinels.OverflowPenalty,
                excludedReqs: excludedReqs,
                groupBlocks: carryGroupBlocks);

            SchedulerOutput? output;
            try
            {
                output = await scheduler.SolveAsync(input, ct, kickProgress);
            }
            catch (OperationCanceledException) { break; }
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
                var why = string.IsNullOrWhiteSpace(output.Message) ? "" : $" — {Trunc(output.Message, 240)}";
                progress?.Report($"{kickPrefix} | решатель: {output.Status}{why} (best {bestScore})");
                continue;
            }

            // 7. Convert assignments to entries, score, accept/reject via LAHC.
            var candidate = AssignmentsToEntries(output.Assignments, reqs, scheduleId, unmatchedEntries);
            var newBreakdown = ScheduleScoreCalculator.ComputeBreakdown(candidate, shared.ScoreCtx);

            long candCost = newBreakdown.Total;
            int vIdx = kicks % lahcL;

            var candHard = (newBreakdown.HardConflicts, newBreakdown.S10_Overflow, newBreakdown.S11_RoomTypeMismatch, newBreakdown.S12_BlockedPlacement);
            int hardCmp = newBreakdown.HardConflicts.CompareTo(currentBreakdown.HardConflicts);
            bool softAccept = candCost <= currentCost || candCost <= lahcHistory[vIdx];
            bool acceptThis = hardCmp < 0 || (hardCmp == 0 && softAccept);

            if (acceptThis)
            {
                long prevBest = bestScore;
                incumbent = candidate;
                currentBreakdown = newBreakdown;
                currentCost = candCost;
                // Incumbent changed - rebuild the ri-entry map so the next kick pins/hints correctly.
                (entryByRi, riByEntryId, _, _) = MatchIncumbent(reqs, incumbent);

                var bestHard = (bestBreakdown.HardConflicts, bestBreakdown.S10_Overflow, bestBreakdown.S11_RoomTypeMismatch, bestBreakdown.S12_BlockedPlacement);
                int bestCmp = candHard.CompareTo(bestHard);
                if (bestCmp < 0 || (bestCmp == 0 && candCost < bestScore))
                {
                    bestScore = candCost;
                    bestEntries = candidate;
                    bestBreakdown = newBreakdown;
                    accepted++;
                    UpdateLnsWeights(weights, axisOps, op, 2);
                    MarkAccepted(telemetry, op.Name, prevBest - candCost);
                }
                else
                {
                    UpdateLnsWeights(weights, axisOps, op, 1);
                    MarkAccepted(telemetry, op.Name, 0);
                }
                long ovfCount = newBreakdown.S10_Overflow / (long)SchedulerSentinels.OverflowPenalty;
                if (ovfCount > 0) forced = overflowOp;
                string ovf = ovfCount > 0 ? $" overflow={ovfCount}" : "";
                progress?.Report($"{kickPrefix} | принято score={currentCost}{ovf} (best {bestScore})");
            }
            else
            {
                UpdateLnsWeights(weights, axisOps, op, 0);
                MarkRejected(telemetry, op.Name);
                progress?.Report($"{kickPrefix} | отклонено score={candCost} Δ={candCost - currentCost} [{DeltaString(currentBreakdown, newBreakdown)}] (best {bestScore})");
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

    private const double ScoreNewBest = 10.0;
    private const double ScoreAccepted = 2.0;
    private const double Lambda = 0.15;
    private const double MinWeight = 0.05;

    private static void UpdateLnsWeights(Dictionary<string, double> weights, IDestroyOperator[] batch,
        IDestroyOperator chosen, int outcome)
    {
        var reward = outcome switch
        {
            2 => ScoreNewBest,      // New global best
            1 => ScoreAccepted,     // Accepted by LAHC but not a new global best
            _ => 0.0                // Rejected
        };

        double lambda = outcome == 0 ? Math.Min(0.9, Lambda * chosen.Factor) : Lambda;
        var target = weights[chosen.Name] * (1.0 - lambda) + reward * lambda;
        weights[chosen.Name] = Math.Max(MinWeight, target);

        foreach (var op in batch)
            if (op.Name != chosen.Name)
                weights[op.Name] = Math.Max(MinWeight, weights[op.Name] * 0.99);
    }

    private static (Dictionary<int, ScheduleEntry>, Dictionary<Guid, int>, List<ScheduleEntry>, HashSet<int>)
        MatchIncumbent(IReadOnlyList<SchedulerRequirement> reqs, IReadOnlyList<ScheduleEntry> entries)
    {
        // Natural key buckets - within each bucket, walk pairwise in stable order.
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

    private static void ReconcileParallelStreams(
        List<SchedulerRequirement> reqs,
        Dictionary<int, ScheduleEntry> entryByRi,
        Dictionary<Guid, int> riByEntryId,
        List<ScheduleEntry> unmatchedEntries,
        HashSet<int> unmatchedReqs)
    {
        if (unmatchedEntries.Count == 0 || unmatchedReqs.Count == 0) return;

        var reqsByRelaxed = new Dictionary<string, Queue<int>>();
        foreach (var ri in unmatchedReqs.OrderBy(x => x))
        {
            if (reqs[ri].ParallelKey is not int) continue;
            var key = RelaxedKey(reqs[ri]);
            if (!reqsByRelaxed.TryGetValue(key, out var q)) reqsByRelaxed[key] = q = new Queue<int>();
            q.Enqueue(ri);
        }
        if (reqsByRelaxed.Count == 0) return;

        var stillUnmatched = new List<ScheduleEntry>();
        foreach (var e in unmatchedEntries)
        {
            if (reqsByRelaxed.TryGetValue(RelaxedKey(e), out var q) && q.Count > 0)
            {
                int ri = q.Dequeue();
                reqs[ri] = reqs[ri] with { TeacherId = e.TeacherId };
                entryByRi[ri] = e;
                riByEntryId[e.Id] = ri;
                unmatchedReqs.Remove(ri);
            }
            else stillUnmatched.Add(e);
        }
        unmatchedEntries.Clear();
        unmatchedEntries.AddRange(stillUnmatched);
    }

    private static string RelaxedKey(SchedulerRequirement r) =>
        $"{r.SubjectId}|{r.LessonType}|{r.WeekType}|{string.Join(",", r.GroupIds.OrderBy(x => x))}|{r.SubgroupLabel ?? ""}|{(r.IsOnline ? 1 : 0)}";

    private static string RelaxedKey(ScheduleEntry e) =>
        $"{e.SubjectId}|{e.LessonType}|{e.WeekType}|{string.Join(",", e.StudentGroups.Select(sg => sg.StudentGroupId).OrderBy(x => x))}|{e.SubgroupLabel ?? ""}|{(e.IsOnline ? 1 : 0)}";

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
        IReadOnlyList<ScheduleEntry> carryOver)
    {
        var parallelGuids = new Dictionary<int, Guid>();
        var result = new List<ScheduleEntry>(assignments.Count + carryOver.Count);
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

        foreach (var e in carryOver)
        {
            var clone = new ScheduleEntry
            {
                ScheduleId = scheduleId,
                SubjectId = e.SubjectId,
                TeacherId = e.TeacherId,
                RoomId = e.RoomId,
                DayOfWeek = e.DayOfWeek,
                PairNumber = e.PairNumber,
                WeekType = e.WeekType,
                LessonType = e.LessonType,
                IsOnline = e.IsOnline,
                ParallelGroupId = e.ParallelGroupId,
                SubgroupLabel = e.SubgroupLabel
            };
            foreach (var sg in e.StudentGroups)
                clone.StudentGroups.Add(new ScheduleEntryStudentGroup { StudentGroupId = sg.StudentGroupId, ScheduleEntry = clone });
            result.Add(clone);
        }
        return result;
    }

    private static string DeltaString(ScoreBreakdown cur, ScoreBreakdown cand)
    {
        var parts = new List<string>();
        void Add(string name, int a, int b) { if (a != b) parts.Add($"{name} {(b - a > 0 ? "+" : "")}{b - a}"); }
        Add("Hard", cur.HardConflicts, cand.HardConflicts);
        if (cur.S10_Overflow != cand.S10_Overflow)
            parts.Add($"ovf {cur.S10_Overflow / (long)SchedulerSentinels.OverflowPenalty}->{cand.S10_Overflow / SchedulerSentinels.OverflowPenalty}");
        Add("S1win", cur.S1_StudentWindows, cand.S1_StudentWindows);
        Add("S2win", cur.S2_TeacherWindows, cand.S2_TeacherWindows);
        Add("S3day", cur.S3_ActiveDays, cand.S3_ActiveDays);
        Add("S4walk", cur.S4_Walking, cand.S4_Walking);
        Add("S5sp", cur.S5_SanPinOverload, cand.S5_SanPinOverload);
        Add("S6con", cur.S6_ConsecSameLesson, cand.S6_ConsecSameLesson);
        Add("S7tod", cur.S7_TimeOfDay, cand.S7_TimeOfDay);
        Add("S8sat", cur.S8_Saturday, cand.S8_Saturday);
        Add("S9dept", cur.S9_DeptMismatch, cand.S9_DeptMismatch);
        Add("S11rt", cur.S11_RoomTypeMismatch, cand.S11_RoomTypeMismatch);
        Add("S12blk", cur.S12_BlockedPlacement, cand.S12_BlockedPlacement);
        return parts.Count == 0 ? "no Δ" : string.Join(", ", parts);
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";

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
