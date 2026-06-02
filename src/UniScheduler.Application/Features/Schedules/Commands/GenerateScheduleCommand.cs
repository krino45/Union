using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.Schedules.Internal;
using UniScheduler.Application.Features.Schedules.Lns;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Schedules.Commands;

public record GenerateScheduleCommand(
    Guid ScheduleId,
    int SolverTimeoutSeconds = 60,
    IProgress<string>? Progress = null,
    IReadOnlyList<Guid>? PlanIds = null,
    bool Polish = false
) : IRequest<GenerateScheduleResult>;

public class GenerateScheduleCommandHandler : IRequestHandler<GenerateScheduleCommand, GenerateScheduleResult>
{
    private readonly IApplicationDbContext db;
    private readonly ISchedulerService scheduler;
    private readonly ILnsOptimizerService lns;

    public GenerateScheduleCommandHandler(IApplicationDbContext db, ISchedulerService scheduler, ILnsOptimizerService lns)
    {
        this.db = db;
        this.scheduler = scheduler;
        this.lns = lns;
    }

    public async Task<GenerateScheduleResult> Handle(GenerateScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await db.Schedules
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), request.ScheduleId);

        var p = request.Progress;
        p?.Report("Загрузка данных...");

        var settingsEntity = await db.SolverSettings.FirstOrDefaultAsync(cancellationToken);
        var weights = settingsEntity == null ? new SolverWeights() : new SolverWeights(
            settingsEntity.StudentWindow, settingsEntity.TeacherWindow, settingsEntity.ActiveDay, settingsEntity.SanPinOverload,
            settingsEntity.ConsecLecture, settingsEntity.ConsecSeminar, settingsEntity.ConsecPractical, settingsEntity.ConsecLab,
            settingsEntity.EarlyPair, settingsEntity.MiddlePair, settingsEntity.LatePair, settingsEntity.ConsecRunScalar,
            settingsEntity.SaturdayPenalty, settingsEntity.DepartmentMismatchPenalty, settingsEntity.WalkingPenaltyMax,
            settingsEntity.StairFloorMeters);

        var shared = await ScheduleBuildContext.LoadSharedDataAsync(db, schedule, weights, cancellationToken);

        var groupToPlanId = new Dictionary<Guid, Guid>();
        foreach (var sp in shared.StudyPlans)
            foreach (var g in sp.Groups)
                groupToPlanId[g.StudentGroupId] = sp.Id;

        List<StudyPlan> plansToRun;
        if (request.PlanIds is { Count: > 0 })
        {
            var byId = shared.StudyPlans.ToDictionary(sp => sp.Id);
            plansToRun = request.PlanIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            if (plansToRun.Count == 0)
                return new GenerateScheduleResult(false, "Infeasible",
                    "Ни один из указанных учебных планов не найден для этого расписания.", 0);
        }
        else
        {
            var groupIdsWithEntries = await db.ScheduleEntryStudentGroups
                .Where(esg => esg.ScheduleEntry.ScheduleId == request.ScheduleId)
                .Select(esg => esg.StudentGroupId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var plansWithEntries = groupIdsWithEntries
                .Where(gid => groupToPlanId.ContainsKey(gid))
                .Select(gid => groupToPlanId[gid])
                .ToHashSet();
            plansToRun = shared.StudyPlans
                .Where(sp => !plansWithEntries.Contains(sp.Id))
                .OrderByDescending(sp => sp.Groups.Count)
                .ThenByDescending(sp => sp.Entries.Count)
                .ToList();
            if (plansToRun.Count == 0)
                return new GenerateScheduleResult(true, "Feasible",
                    "Все планы уже сгенерированы. Чтобы перегенерировать — выберите планы явно.", 0);
        }

        var plansToRunIds = plansToRun.Select(sp => sp.Id).ToHashSet();

        var existing = await db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(cancellationToken);

        var entriesByPlan = new Dictionary<Guid, List<ScheduleEntry>>();
        var keptEntries = new List<ScheduleEntry>();
        foreach (var e in existing)
        {
            var firstGroupId = e.StudentGroups.FirstOrDefault()?.StudentGroupId;
            Guid? planId = firstGroupId.HasValue && groupToPlanId.TryGetValue(firstGroupId.Value, out var pid)
                ? pid : (Guid?)null;
            if (planId.HasValue && plansToRunIds.Contains(planId.Value))
            {
                if (!entriesByPlan.TryGetValue(planId.Value, out var list))
                    entriesByPlan[planId.Value] = list = new List<ScheduleEntry>();
                list.Add(e);
            }
            else
            {
                keptEntries.Add(e);
            }
        }

        var roomBlocks = keptEntries
            .Where(e => e.RoomId.HasValue && !e.IsOnline)
            .Select(e => new SchedulerRoomBlock(e.RoomId!.Value, e.DayOfWeek, e.PairNumber, e.WeekType))
            .ToList();
        var dynamicTeacherBlocks = keptEntries
            .Select(e => new SchedulerBlock(e.TeacherId, e.DayOfWeek, e.PairNumber, e.WeekType))
            .ToList();

        int batchGroupTarget = int.TryParse(Environment.GetEnvironmentVariable("UNISCHEDULER_BATCH_GROUP_TARGET"), out var t) && t > 0 ? t : 5;
        var batches = BuildBatches(plansToRun, batchGroupTarget);

        int totalPlaced = 0;
        var perPlanMessages = new List<string>();
        int parallelSeq = 1;
        int idx = 0;

        for (int bi = 0; bi < batches.Count; bi++)
        {
            var batch = batches[bi];
            int batchGroups = batch.Sum(pl => pl.Groups.Count);
            var batchPrefix = $"Партия {bi + 1}/{batches.Count} ({batch.Count} планов, {batchGroups} групп)";
            p?.Report($"{batchPrefix} | подготовка...");

            var batchProgress = p == null ? null
                : (IProgress<string>)new Progress<string>(s => p.Report($"{batchPrefix} | {s}"));

            var requirements = new List<SchedulerRequirement>();
            foreach (var plan in batch)
                requirements.AddRange(ScheduleRequirementBuilder.BuildRequirementsForPlan(plan, shared, ref idx, ref parallelSeq));

            if (requirements.Count == 0)
            {
                foreach (var plan in batch)
                    perPlanMessages.Add($"{plan.Name ?? plan.Id.ToString()[..8]}: 0 занятий (пропущен)");
                continue;
            }

            var input = ScheduleBuildContext.BuildSchedulerInputForPlan(
                schedule.Id, shared, requirements, roomBlocks, dynamicTeacherBlocks,
                request.SolverTimeoutSeconds, weights);

            var output = await scheduler.SolveAsync(input, cancellationToken, batchProgress);

            if (output.Status == SolverStatus.Infeasible)
            {
                var labels = string.Join(", ", batch.Select(pl => pl.Name ?? pl.Id.ToString()[..8]));
                perPlanMessages.Add($"Партия [{labels}]: НЕРАЗРЕШИМО: {output.Message}");
                schedule.GenerationNotes = $"Партия {bi + 1} неразрешима. {string.Join(" | ", perPlanMessages)}";
                await db.SaveChangesAsync(cancellationToken);
                return new GenerateScheduleResult(false, "Infeasible",
                    $"Batch {bi + 1} ({labels}): {output.Message}", totalPlaced);
            }

            if (output.Status == SolverStatus.Unknown)
            {
                foreach (var plan in batch)
                    perPlanMessages.Add($"{plan.Name ?? plan.Id.ToString()[..8]}: ТАЙМАУТ (партия {bi + 1})");
                continue;
            }

            foreach (var plan in batch)
            {
                if (entriesByPlan.TryGetValue(plan.Id, out var oldForPlan) && oldForPlan.Count > 0)
                {
                    var oldIds = oldForPlan.Select(e => e.Id).ToList();
                    var relatedRequests = await db.RescheduleRequests
                        .Where(r => oldIds.Contains(r.OriginalEntryId))
                        .ToListAsync(cancellationToken);
                    db.RescheduleRequests.RemoveRange(relatedRequests);
                    db.ScheduleEntries.RemoveRange(oldForPlan);
                    entriesByPlan.Remove(plan.Id);
                }
            }

            var parallelGuids = new Dictionary<int, Guid>();
            var newEntries = new List<ScheduleEntry>();
            var countByPlan = new Dictionary<Guid, int>();
            foreach (var assignment in output.Assignments)
            {
                var req = input.Requirements[assignment.RequirementIndex];
                Guid? parallelGroupId = null;
                if (req.ParallelKey is int pk)
                {
                    if (!parallelGuids.TryGetValue(pk, out var g)) parallelGuids[pk] = g = Guid.NewGuid();
                    parallelGroupId = g;
                }
                var entry = new ScheduleEntry
                {
                    ScheduleId = request.ScheduleId,
                    SubjectId = req.SubjectId,
                    TeacherId = req.TeacherId,
                    RoomId = req.IsOnline ? null : assignment.RoomId,
                    DayOfWeek = assignment.Day,
                    PairNumber = assignment.PairNumber,
                    WeekType = assignment.WeekType,
                    LessonType = req.LessonType,
                    IsOnline = req.IsOnline,
                    ParallelGroupId = parallelGroupId,
                    SubgroupLabel = req.SubgroupLabel
                };
                db.ScheduleEntries.Add(entry);
                newEntries.Add(entry);
                foreach (var groupId in req.GroupIds)
                {
                    db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup
                    {
                        ScheduleEntry = entry,
                        StudentGroupId = groupId
                    });
                }

                var firstGroup = req.GroupIds.FirstOrDefault();
                if (groupToPlanId.TryGetValue(firstGroup, out var pid))
                    countByPlan[pid] = countByPlan.GetValueOrDefault(pid) + 1;
            }
            await db.SaveChangesAsync(cancellationToken);
            totalPlaced += newEntries.Count;
            foreach (var plan in batch)
            {
                var cnt = countByPlan.GetValueOrDefault(plan.Id, 0);
                perPlanMessages.Add($"{plan.Name ?? plan.Id.ToString()[..8]}: {cnt} занятий");
            }

            foreach (var e in newEntries)
            {
                if (e.RoomId.HasValue && !e.IsOnline)
                    roomBlocks.Add(new SchedulerRoomBlock(e.RoomId.Value, e.DayOfWeek, e.PairNumber, e.WeekType));
                dynamicTeacherBlocks.Add(new SchedulerBlock(e.TeacherId, e.DayOfWeek, e.PairNumber, e.WeekType));
            }
        }

        schedule.GeneratedAt = DateTime.UtcNow;
        schedule.GenerationNotes = string.Join(" | ", perPlanMessages);
        await db.SaveChangesAsync(cancellationToken);

        if (totalPlaced == 0)
        {
            return new GenerateScheduleResult(false, "Unknown",
                "Ни один учебный план не уложился в таймаут. Увеличьте таймаут или сузьте состав планов.",
                0);
        }

        p?.Report("Вычисление оценки...");
        var scoreEntries = await db.ScheduleEntries
            .Include(e => e.StudentGroups)
            .Where(e => e.ScheduleId == request.ScheduleId)
            .ToListAsync(cancellationToken);
        schedule.BaseScore = ScheduleScoreCalculator.Compute(scoreEntries, shared.ScoreCtx);
        await db.SaveChangesAsync(cancellationToken);

        // Optional polish phase: LNS-based improvement using the same CP-SAT model as repair.
        // Touches the entire schedule (not just the planned plans), so it runs once at the very
        // end. Score-gated — we only commit if the polish improved the schedule overall.
        if (request.Polish)
        {
            try
            {
                p?.Report("Полировка (LNS)...");
                int budgetMin = int.TryParse(Environment.GetEnvironmentVariable("UNISCHEDULER_LNS_BUDGET_MIN"), out var bm) && bm > 0 ? bm : 5;
                int kickSec = int.TryParse(Environment.GetEnvironmentVariable("UNISCHEDULER_LNS_KICK_SEC"), out var ks) && ks > 0 ? ks : 10;
                var opts = new LnsOptions(
                    TotalBudget: TimeSpan.FromMinutes(budgetMin),
                    KickTimeoutSeconds: kickSec,
                    MaxIterations: 200,
                    Seed: 12345);
                var result = await lns.OptimizeAsync(request.ScheduleId, scoreEntries, shared, opts, p, cancellationToken);

                if (result.AcceptedAny && result.AfterBreakdown.Total < result.BeforeBreakdown.Total)
                {
                    p?.Report($"LNS: применение результата ({result.BeforeBreakdown.Total} → {result.AfterBreakdown.Total})...");
                    await ReplaceAllEntriesAsync(request.ScheduleId, result.Entries, cancellationToken);
                    schedule.BaseScore = result.AfterBreakdown.Total;
                    schedule.GenerationNotes = (schedule.GenerationNotes ?? "") +
                        $" | LNS: {result.BeforeBreakdown.Total} → {result.AfterBreakdown.Total} ({result.AcceptedKicks}/{result.TotalKicks} kicks)";
                    await db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    p?.Report($"LNS: улучшений не найдено ({result.TotalKicks} kicks)");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                schedule.GenerationNotes = (schedule.GenerationNotes ?? "") + $" | LNS error: {ex.Message}";
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return new GenerateScheduleResult(true, "Feasible",
            string.Join(" | ", perPlanMessages), totalPlaced);
    }

    // Whole-schedule swap used by the polish phase. We mirror the per-batch delete (cascade
    // reschedule requests), then re-add fresh entries with new IDs and group rows.
    private async Task ReplaceAllEntriesAsync(Guid scheduleId, IReadOnlyList<ScheduleEntry> newEntries, CancellationToken ct)
    {
        var existing = await db.ScheduleEntries
            .Where(e => e.ScheduleId == scheduleId)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            var oldIds = existing.Select(e => e.Id).ToList();
            var relatedRequests = await db.RescheduleRequests
                .Where(r => oldIds.Contains(r.OriginalEntryId))
                .ToListAsync(ct);
            if (relatedRequests.Count > 0) db.RescheduleRequests.RemoveRange(relatedRequests);
            db.ScheduleEntries.RemoveRange(existing);
            await db.SaveChangesAsync(ct);
        }

        foreach (var e in newEntries)
        {
            e.Id = Guid.Empty; // ensure EF treats it as a fresh insert
            e.ScheduleId = scheduleId;
            db.ScheduleEntries.Add(e);
            foreach (var sg in e.StudentGroups)
            {
                sg.ScheduleEntry = e;
                db.ScheduleEntryStudentGroups.Add(sg);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    // Greedy bin-packing: walk plans in the caller-given order (priority order from the UI, or
    // default size-desc), accumulating into a batch until adding the next plan would exceed the
    // target group count. A plan whose own group count >= target ships as its own batch.
    private static List<List<StudyPlan>> BuildBatches(IReadOnlyList<StudyPlan> plans, int target)
    {
        var batches = new List<List<StudyPlan>>();
        var current = new List<StudyPlan>();
        int currentGroups = 0;
        foreach (var plan in plans)
        {
            int planGroups = plan.Groups.Count;
            if (current.Count > 0 && currentGroups + planGroups > target)
            {
                batches.Add(current);
                current = new List<StudyPlan>();
                currentGroups = 0;
            }
            current.Add(plan);
            currentGroups += planGroups;
            if (currentGroups >= target)
            {
                batches.Add(current);
                current = new List<StudyPlan>();
                currentGroups = 0;
            }
        }
        if (current.Count > 0) batches.Add(current);
        return batches;
    }
}
