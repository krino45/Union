using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Google.OrTools.Sat;
using UniScheduler.Application.Common.Config;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Scheduler;

public class OrToolsSchedulerService : ISchedulerService
{
    private const int NumDays = 6;
    private const double WalkSpeedMperMin = 80.0;

    public Task<SchedulerOutput> SolveAsync(SchedulerInput input, CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
        => Task.Run(() => Solve(input, progress, cancellationToken), cancellationToken);

    private SchedulerOutput Solve(SchedulerInput input, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        const int TotalStages = 28;
        int stage = 0;
        void Report(string msg) => progress?.Report($"[{++stage}/{TotalStages}] {msg}");
        void ReportSub(string msg) => progress?.Report($"[{stage}/{TotalStages}] {msg}");

        int numPairs = input.PairsPerDay;
        int[] breakMinutes = BuildBreakArray(input.BreakMinutesBetweenPairs, numPairs);
        var w = input.Weights ?? new SolverWeights();

        var model = new CpModel();
        // Sentinel "always 0" bool
        var Zero = model.NewBoolVar("ZERO");
        model.Add(Zero == 0);

        var reqs = input.Requirements.ToList();
        var rooms = input.Rooms.ToList();
        var groups = input.Groups.ToList();
        var teachers = input.Teachers.ToList();

        var distances = BuildDistanceMap(input.BuildingDistances);
        var roomDistances = BuildRoomDistanceMap(input.RoomDistances);
        var blocked = BuildBlockedSet(input.TeacherBlocks);
        var blockedRoomSlots = BuildBlockedRoomSlots(input.RoomBlocks);
        var groupBlockedDays = groups.ToDictionary(g => g.Id, g => (g.BlockedDays ?? Array.Empty<int>()).ToHashSet());

        var groupSizeById = groups.ToDictionary(g => g.Id, g => g.StudentCount);
        var reqGroupSize = reqs.Select(r => r.HeadcountOverride
                                            ?? r.GroupIds.Sum(gid =>
                                                groupSizeById.TryGetValue(gid, out var sz) ? sz : 0)).ToArray();

        // LNS pins (repair mode). Each pinned req emits exactly one BoolVar (the matching cell),
        // and H2's AddExactlyOne over that single var forces it to 1. We validate every pin upfront
        // so failure points have actionable messages instead of falling out as "no feasible slots".
        Dictionary<int, (int d, int p, int rmi)>? pinTarget = null;
        if (input.Pinnings is { Count: > 0 })
        {
            Report($"Закрепление: проверка {input.Pinnings.Count} закреплений...");
            var roomIdToIdx = new Dictionary<Guid, int>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++) roomIdToIdx[rooms[i].Id] = i;

            pinTarget = new Dictionary<int, (int, int, int)>(input.Pinnings.Count);
            foreach (var pin in input.Pinnings)
            {
                int ri = pin.RequirementIndex;
                if (ri < 0 || ri >= reqs.Count)
                    return new SchedulerOutput(SolverStatus.Infeasible,
                        $"Pin: requirement index {ri} out of range (0..{reqs.Count - 1})", []);
                if (pinTarget.ContainsKey(ri))
                    return new SchedulerOutput(SolverStatus.Infeasible,
                        $"Pin: requirement {ri} pinned more than once", []);
                var req = reqs[ri];
                if (req.WeekType != pin.WeekType)
                    return new SchedulerOutput(SolverStatus.Infeasible,
                        $"Pin ri={ri}: WeekType mismatch (req={req.WeekType}, pin={pin.WeekType})", []);
                if (!roomIdToIdx.TryGetValue(pin.RoomId, out int rmi))
                    return new SchedulerOutput(SolverStatus.Infeasible,
                        $"Pin ri={ri}: room {pin.RoomId} not in scheduler input", []);

                int d = (int)pin.Day - 1;
                int pp = pin.PairNumber - 1;
                if (d < 0 || d >= NumDays || pp < 0 || pp >= numPairs)
                    return new SchedulerOutput(SolverStatus.Infeasible,
                        $"Pin ri={ri}: day/pair {pin.Day}/p{pin.PairNumber} out of range", []);

                if (!IsCompatible(req, rooms[rmi], reqGroupSize[ri]))
                    return new SchedulerOutput(SolverStatus.Infeasible,
                        $"Pin ri={ri}: room {pin.RoomId} incompatible (type/capacity/equipment/online)", []);
                if (req.GroupIds.Any(gId => groupBlockedDays.TryGetValue(gId, out var bd) && bd.Contains(d)))
                    return new SchedulerOutput(SolverStatus.Infeasible,
                        $"Pin ri={ri}: day {pin.Day} is blocked for one of the groups", []);

                int[] calendarWis = req.WeekType == WeekType.Both
                    ? new[] { 0, 1 }
                    : new[] { VarWeekIndex(req.WeekType) };
                foreach (int awi in calendarWis)
                {
                    if (blocked.Contains((req.TeacherId, d, pp, awi)))
                        return new SchedulerOutput(SolverStatus.Infeasible,
                            $"Pin ri={ri}: teacher slot ({pin.Day}, p{pin.PairNumber}, week {awi}) is blocked", []);
                    if (blockedRoomSlots.Contains((pin.RoomId, d, pp, awi)))
                        return new SchedulerOutput(SolverStatus.Infeasible,
                            $"Pin ri={ri}: room {pin.RoomId} is blocked at ({pin.Day}, p{pin.PairNumber}, week {awi})", []);
                }

                pinTarget[ri] = (d, pp, rmi);
            }
        }

        Report($"Совместимость аудиторий ({reqs.Count} занятий × {rooms.Count} аудиторий)...");
        var compatibleRooms = new int[reqs.Count][];
        {
            int done = 0;
            int reportEvery = Math.Max(1, reqs.Count / 20);
            Parallel.For(0, reqs.Count, ri =>
            {
                var list = new List<int>(rooms.Count);
                for (int rmi = 0; rmi < rooms.Count; rmi++)
                    if (IsCompatible(reqs[ri], rooms[rmi], reqGroupSize[ri]))
                        list.Add(rmi);
                compatibleRooms[ri] = list.ToArray();
                int d = Interlocked.Increment(ref done);
                if (d % reportEvery == 0)
                    ReportSub($"Совместимость: {d}/{reqs.Count} ({100 * d / Math.Max(1, reqs.Count)}%)");
            });
        }

        //   varsByReqWi:   (ri, varWi) -> list<v>                  — used by H2 + feasibility
        //   varsByReqCell: (ri, d, p, varWi) -> list<(rmi, v)>     — used everywhere else
        //   byCellRoom:    (rmi, d, p, calendarWi) -> list<(ri, tId, v, size)>  — used by H4 only
        //   byCellTeacher: (tId, d, p, calendarWi) -> list<(ri, rmi, v)>        — used by H5 only
        // The byCell* indexes use calendar week (Both-week vars appear in BOTH wi=0 and wi=1).
        var varsByReqWi = new Dictionary<(int ri, int wi), List<BoolVar>>();
        var varsByReqCell = new Dictionary<(int ri, int d, int p, int wi), List<(int rmi, BoolVar v)>>();
        var byCellRoom = new Dictionary<(int rmi, int d, int p, int wi), List<(int ri, Guid tId, BoolVar v, long size)>>();
        var byCellTeacher = new Dictionary<(Guid tId, int d, int p, int wi), List<(int ri, int rmi, BoolVar v)>>();
        long totalVarCount = 0;

        Report($"Создание переменных ({reqs.Count} занятий)...");
        {
            int reportEvery = Math.Max(1, reqs.Count / 20);
            for (int ri = 0; ri < reqs.Count; ri++)
            {
                if (ri % reportEvery == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReportSub(
                        $"Переменные: занятие {ri}/{reqs.Count} ({100 * ri / Math.Max(1, reqs.Count)}%), создано {totalVarCount} перем.");
                }
                var req = reqs[ri];
                int varWi = VarWeekIndex(req.WeekType);
                var compatRmis = compatibleRooms[ri];
                if (compatRmis.Length == 0) continue;
                Guid tId = req.TeacherId;
                long size = reqGroupSize[ri];
                int[] calendarWis = req.WeekType == WeekType.Both ? new[] { 0, 1 } : new[] { varWi };

                bool isPinned = false;
                int pinD = 0, pinP = 0, pinRmi = 0;
                if (pinTarget != null && pinTarget.TryGetValue(ri, out var pt))
                {
                    isPinned = true;
                    pinD = pt.d; pinP = pt.p; pinRmi = pt.rmi;
                }

                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                {
                    if (isPinned && (d != pinD || p != pinP)) continue;

                    bool dayBlockedForGroup = req.GroupIds.Any(gId =>
                        groupBlockedDays.TryGetValue(gId, out var bd) && bd.Contains(d));
                    if (dayBlockedForGroup) continue;

                    bool slotBlocked = req.WeekType == WeekType.Both
                        ? blocked.Contains((req.TeacherId, d, p, 0)) || blocked.Contains((req.TeacherId, d, p, 1))
                        : blocked.Contains((req.TeacherId, d, p, varWi));
                    if (slotBlocked) continue;

                    foreach (int rmi in compatRmis)
                    {
                        if (isPinned && rmi != pinRmi) continue;

                        if (blockedRoomSlots.Count > 0 && !rooms[rmi].IsDistributed)
                        {
                            var roomId = rooms[rmi].Id;
                            bool roomTaken = false;
                            foreach (int awi in calendarWis)
                            {
                                if (blockedRoomSlots.Contains((roomId, d, p, awi))) { roomTaken = true; break; }
                            }
                            if (roomTaken) continue;
                        }

                        var v = model.NewBoolVar($"a_{ri}_{d}_{p}_{varWi}_{rmi}");
                        totalVarCount++;

                        var k1 = (ri, varWi);
                        if (!varsByReqWi.TryGetValue(k1, out var l1)) varsByReqWi[k1] = l1 = new List<BoolVar>();
                        l1.Add(v);

                        var k2 = (ri, d, p, varWi);
                        if (!varsByReqCell.TryGetValue(k2, out var l2))
                            varsByReqCell[k2] = l2 = new List<(int, BoolVar)>();
                        l2.Add((rmi, v));

                        foreach (int awi in calendarWis)
                        {
                            var kCR = (rmi, d, p, awi);
                            if (!byCellRoom.TryGetValue(kCR, out var lCR))
                                byCellRoom[kCR] = lCR = new List<(int, Guid, BoolVar, long)>();
                            lCR.Add((ri, tId, v, size));

                            var kCT = (tId, d, p, awi);
                            if (!byCellTeacher.TryGetValue(kCT, out var lCT))
                                byCellTeacher[kCT] = lCT = new List<(int, int, BoolVar)>();
                            lCT.Add((ri, rmi, v));
                        }
                    }
                }
            }
        }

        Report($"Проверка осуществимости ({totalVarCount} переменных)...");
        var noRoomLines = new List<string>();
        var noSlotLines = new List<string>();
        for (int ri = 0; ri < reqs.Count; ri++)
        {
            var req = reqs[ri];
            if (compatibleRooms[ri].Length == 0)
            {
                noRoomLines.Add(FormatReqLabel(req));
                continue;
            }

            int varWi = VarWeekIndex(req.WeekType);
            if (!varsByReqWi.ContainsKey((ri, varWi)))
            {
                int teacherBlocked = 0;
                int groupBlocked = 0;
                var freeCells = new List<string>();
                int[] calendarWis = req.WeekType == WeekType.Both ? new[] { 0, 1 } : new[] { varWi };
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                {
                    bool dayBlockedForGroup = req.GroupIds.Any(gId =>
                        groupBlockedDays.TryGetValue(gId, out var bd) && bd.Contains(d));
                    bool slotBlockedTeacher = req.WeekType == WeekType.Both
                        ? blocked.Contains((req.TeacherId, d, p, 0)) || blocked.Contains((req.TeacherId, d, p, 1))
                        : blocked.Contains((req.TeacherId, d, p, varWi));
                    if (dayBlockedForGroup) groupBlocked++;
                    if (slotBlockedTeacher) teacherBlocked++;
                    if (!dayBlockedForGroup && !slotBlockedTeacher && freeCells.Count < 3)
                        freeCells.Add($"д{d + 1}п{p + 1}");
                }
                int totalCells = NumDays * numPairs;
                string detail = $"teach={teacherBlocked}/{totalCells}, group-days={groupBlocked}/{totalCells}";
                if (freeCells.Count > 0) detail += $", free=[{string.Join(",", freeCells)}]";
                noSlotLines.Add($"{FormatReqLabel(req)} [{detail}]");
            }
        }

        if (noRoomLines.Count > 0 || noSlotLines.Count > 0)
        {
            var parts = new List<string>();
            if (noRoomLines.Count > 0)
                parts.Add(
                    $"{noRoomLines.Count} requirement(s) have no compatible rooms — check AllowedLessonTypes, room type, and capacity: {string.Join("; ", noRoomLines.Take(5))}");
            if (noSlotLines.Count > 0)
                parts.Add(
                    $"{noSlotLines.Count} requirement(s) have no feasible (day, pair) cell: {string.Join(" || ", noSlotLines.Take(5))}");
            return new SchedulerOutput(SolverStatus.Infeasible, string.Join(" | ", parts),
                Array.Empty<SchedulerAssignment>());
        }

        // Feasibility done — varsByReqWi is only used by H2 (below, one pass) and the feasibility
        // check above. Drop it right after H2 emits its constraints. compatibleRooms isn't read
        // again — drop it now.
        compatibleRooms = null!;

        if (input.Hints is { Count: > 0 })
        {
            var roomIdToIdxH = new Dictionary<Guid, int>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++) roomIdToIdxH[rooms[i].Id] = i;
            int hintsApplied = 0;
            foreach (var hint in input.Hints)
            {
                int ri = hint.RequirementIndex;
                if (ri < 0 || ri >= reqs.Count) continue;
                if (!roomIdToIdxH.TryGetValue(hint.RoomId, out int rmi)) continue;
                var req = reqs[ri];
                if (req.WeekType != hint.WeekType) continue;
                int varWi = VarWeekIndex(req.WeekType);
                int d = (int)hint.Day - 1;
                int pp = hint.PairNumber - 1;
                if (d < 0 || d >= NumDays || pp < 0 || pp >= numPairs) continue;
                if (!varsByReqCell.TryGetValue((ri, d, pp, varWi), out var cell)) continue;
                foreach (var (cellRmi, v) in cell)
                {
                    if (cellRmi != rmi) continue;
                    model.AddHint(v, 1);
                    hintsApplied++;
                    break;
                }
            }
            ReportSub($"Подсказки CP-SAT: применено {hintsApplied}/{input.Hints.Count}");
        }

        Report("H1: один учитель на группу/предмет/тип занятия");
        {
            var gstTeachers = new Dictionary<(Guid grp, Guid subj, LessonType lt), HashSet<Guid>>();
            foreach (var req in reqs)
            {
                // Parallel sessions (language streams / lab subgroups) intentionally assign several
                // teachers to the same (group, subject, lesson type) — exempt from the one-teacher rule.
                if (req.ParallelKey.HasValue) continue;
                foreach (var gId in req.GroupIds)
                {
                    var key = (gId, req.SubjectId, req.LessonType);
                    if (!gstTeachers.TryGetValue(key, out var ts)) gstTeachers[key] = ts = new HashSet<Guid>();
                    ts.Add(req.TeacherId);
                }
            }

            var teacherConflicts = gstTeachers
                .Where(kv => kv.Value.Count > 1)
                .Select(kv =>
                    $"group …{kv.Key.grp.ToString()[..8]}: {kv.Key.lt} of subj …{kv.Key.subj.ToString()[..8]}")
                .ToList();
            if (teacherConflicts.Count > 0)
                return new SchedulerOutput(SolverStatus.Infeasible,
                    $"Multiple teachers assigned to the same (group, subject, lesson type) — only one teacher per combination is allowed. " +
                    $"{teacherConflicts.Count} conflict(s): {string.Join("; ", teacherConflicts.Take(5))}",
                    Array.Empty<SchedulerAssignment>());
        }

        Report("H2: каждое занятие ровно один раз");
        //  H2: Each requirement scheduled exactly once
        for (int ri = 0; ri < reqs.Count; ri++)
        {
            int varWi = VarWeekIndex(reqs[ri].WeekType);
            if (varsByReqWi.TryGetValue((ri, varWi), out var slotVars))
                model.AddExactlyOne(slotVars);
        }

        // H2 done — varsByReqWi has no more consumers.
        varsByReqWi = null!;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        // teacherReqs is also used for isTeacherUsed auxiliary vars and soft objectives below.
        var teacherReqs = teachers.ToDictionary(t => t.Id, t =>
            reqs.Select((r, i) => (r, i)).Where(x => x.r.TeacherId == t.Id).Select(x => x.i).ToList());
        var teacherIdxMap = teachers.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);

        Report($"H4: один урок в аудитории за слот ({byCellRoom.Count} активных ячеек)...");
        // H4: at most one requirement (lesson) per room per slot.
        // H4-cap: total headcount of all concurrent requirements ≤ room capacity.
        {
            int h4Done = 0;
            int h4Total = byCellRoom.Count;
            int h4Report = Math.Max(1, h4Total / 20);
            foreach (var ((rmi, d, p, wi), entries) in byCellRoom)
            {
                if (h4Done % h4Report == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReportSub($"H4: ячейка {h4Done}/{h4Total} ({100 * h4Done / Math.Max(1, h4Total)}%)");
                }
                h4Done++;
                if (rooms[rmi].IsDistributed) continue;

                var byReq = new Dictionary<int, List<BoolVar>>();
                var capVars = new List<IntVar>();
                var capSizes = new List<long>();
                foreach (var (ri, _, v, size) in entries)
                {
                    if (!byReq.TryGetValue(ri, out var rv)) byReq[ri] = rv = new List<BoolVar>();
                    rv.Add(v);
                    capVars.Add(v);
                    capSizes.Add(size);
                }

                var reqPresences = new List<BoolVar>();
                foreach (var (ri, rv) in byReq)
                {
                    BoolVar pres;
                    if (rv.Count == 1) pres = rv[0];
                    else
                    {
                        pres = model.NewBoolVar($"rp4_{ri}_{rmi}_{d}_{p}_{wi}");
                        model.AddMaxEquality(pres, rv.Select(x => (IntVar)x).ToArray());
                    }
                    reqPresences.Add(pres);
                }

                // SportsHall is a multi-occupancy venue (many PE groups at once, separated by
                // teacher/zone). Drop the AddAtMostOne but keep the headcount-vs-capacity check.
                bool isSportsHall = rooms[rmi].RoomType == RoomType.SportsHall;
                if (!isSportsHall && reqPresences.Count > 1) model.AddAtMostOne(reqPresences);
                if (capVars.Count > 0 && rooms[rmi].Capacity > 0)
                    model.Add(LinearExpr.WeightedSum(capVars.ToArray(), capSizes.ToArray()) <= rooms[rmi].Capacity);
            }
        }
        byCellRoom = null!;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        Report($"H5: преподаватель ведёт не более одного занятия за слот ({byCellTeacher.Count} активных ячеек)...");
        // H5: each teacher takes at most one requirement per slot.
        {
            int h5Done = 0;
            int h5Total = byCellTeacher.Count;
            int h5Report = Math.Max(1, h5Total / 20);
            foreach (var ((tId, d, p, wi), entries) in byCellTeacher)
            {
                if (h5Done % h5Report == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReportSub($"H5: ячейка {h5Done}/{h5Total} ({100 * h5Done / Math.Max(1, h5Total)}%)");
                }
                h5Done++;

                var byReq = new Dictionary<int, List<BoolVar>>();
                foreach (var (ri, _, v) in entries)
                {
                    if (!byReq.TryGetValue(ri, out var rv)) byReq[ri] = rv = new List<BoolVar>();
                    rv.Add(v);
                }

                if (byReq.Count <= 1) continue; // only one req involves this teacher at this slot due to H2

                int ti = teacherIdxMap[tId];
                var reqPresences = new List<BoolVar>();
                foreach (var (ri, rv) in byReq)
                {
                    BoolVar pres;
                    if (rv.Count == 1) pres = rv[0];
                    else
                    {
                        pres = model.NewBoolVar($"rp5_{ri}_{ti}_{d}_{p}_{wi}");
                        model.AddMaxEquality(pres, rv.Select(x => (IntVar)x).ToArray());
                    }
                    reqPresences.Add(pres);
                }

                model.AddAtMostOne(reqPresences);
            }
        }
        byCellTeacher = null!;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        Report($"H6: конфликты групп ({groups.Count} групп)...");
        var groupReqs = groups.ToDictionary(g => g.Id, g =>
            reqs.Select((r, i) => (r, i)).Where(x => x.r.GroupIds.Contains(g.Id)).Select(x => x.i).ToList());

        {
            int h6done = 0, h6total = groupReqs.Count;
            int h6Report = Math.Max(1, h6total / 20);
            foreach (var (gKey, grIdxs) in groupReqs)
            {
                if (h6done % h6Report == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReportSub($"H6: группа {h6done}/{h6total} ({100 * h6done / Math.Max(1, h6total)}%)");
                }
                h6done++;
                if (grIdxs.Count <= 1) continue;
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                for (int wi = 0; wi < 2; wi++)
                {
                    // Parallel sessions of one logical class (same ParallelKey) all occupy this group's
                    // slot by design, so they count as a SINGLE occupant — collapse them via an OR.
                    var occupants = new List<BoolVar>();
                    var parallelCellVars = new Dictionary<int, List<BoolVar>>();
                    foreach (int ri in grIdxs)
                    {
                        if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                        int varWi = VarWeekIndex(reqs[ri].WeekType);
                        if (!varsByReqCell.TryGetValue((ri, d, p, varWi), out var cell)) continue;
                        if (reqs[ri].ParallelKey is int pk)
                        {
                            if (!parallelCellVars.TryGetValue(pk, out var lst))
                                parallelCellVars[pk] = lst = new List<BoolVar>();
                            foreach (var (_, v) in cell) lst.Add(v);
                        }
                        else
                        {
                            foreach (var (_, v) in cell) occupants.Add(v);
                        }
                    }

                    foreach (var (pk, lst) in parallelCellVars)
                    {
                        if (lst.Count == 1)
                        {
                            occupants.Add(lst[0]);
                            continue;
                        }

                        var pres = model.NewBoolVar($"gpar_{gKey}_{d}_{p}_{wi}_{pk}");
                        model.AddMaxEquality(pres, lst.Select(v => (IntVar)v).ToArray());
                        occupants.Add(pres);
                    }

                    if (occupants.Count > 1) model.AddAtMostOne(occupants);
                }
            }
        }

        Report("H_par: параллельные сессии в одном слоте...");
        // Co-schedule parallel siblings: every member of a ParallelKey group lands on the SAME
        // (day, pair) as the anchor, so a class taught in parallel streams happens at one time.
        foreach (var pg in reqs.Select((r, i) => (r, i))
                     .Where(x => x.r.ParallelKey.HasValue)
                     .GroupBy(x => x.r.ParallelKey!.Value)
                     .Where(g => g.Count() > 1))
        {
            var members = pg.Select(x => x.i).ToList();
            int anchor = members[0];
            int anchorWi = VarWeekIndex(reqs[anchor].WeekType);
            for (int mi = 1; mi < members.Count; mi++)
            {
                int m = members[mi];
                int mWi = VarWeekIndex(reqs[m].WeekType);
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                {
                    varsByReqCell.TryGetValue((anchor, d, p, anchorWi), out var anchorCell);
                    varsByReqCell.TryGetValue((m, d, p, mWi), out var memberCell);
                    if ((anchorCell == null || anchorCell.Count == 0) &&
                        (memberCell == null || memberCell.Count == 0)) continue;
                    var anchorVars = anchorCell == null
                        ? Array.Empty<IntVar>()
                        : anchorCell.Select(t => (IntVar)t.v).ToArray();
                    var memberVars = memberCell == null
                        ? Array.Empty<IntVar>()
                        : memberCell.Select(t => (IntVar)t.v).ToArray();
                    model.Add(LinearExpr.Sum(anchorVars) == LinearExpr.Sum(memberVars));
                }
            }
        }

        // Building index
        var allBuildingIds = rooms.Where(r => !r.IsDistributed).Select(r => r.BuildingId).Distinct().ToList();
        var buildingIdToIdx = allBuildingIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var roomToBuildingIdx = new int[rooms.Count];
        for (int i = 0; i < rooms.Count; i++)
            roomToBuildingIdx[i] = rooms[i].IsDistributed ? -1 : buildingIdToIdx[rooms[i].BuildingId];

        var zoneIdx = new Dictionary<(int bi, int floor), int>();
        var zones = new List<(int bi, int floor)>();
        var roomToZone = new int[rooms.Count];
        for (int rmi = 0; rmi < rooms.Count; rmi++)
        {
            if (rooms[rmi].IsDistributed)
            {
                roomToZone[rmi] = -1;
                continue;
            }

            var key = (roomToBuildingIdx[rmi], rooms[rmi].Floor);
            if (!zoneIdx.TryGetValue(key, out int zi))
            {
                zi = zones.Count;
                zoneIdx[key] = zi;
                zones.Add(key);
            }

            roomToZone[rmi] = zi;
        }

        var bldgGuidByIdx = buildingIdToIdx.ToDictionary(kv => kv.Value, kv => kv.Key);

        var zoneEntryMeters = new Dictionary<(int bi, int floor), int>();
        if (input.ZoneEntryDistances != null)
        {
            foreach (var z in input.ZoneEntryDistances)
            {
                if (buildingIdToIdx.TryGetValue(z.BuildingId, out int bi))
                    zoneEntryMeters[(bi, z.Floor)] = z.EntryDistanceMeters;
            }
        }
        int ZoneEntryWalk(int bi, int floor) =>
            zoneEntryMeters.TryGetValue((bi, floor), out int e)
                ? e
                : Math.Max(0, floor - 1) * w.StairFloorMeters;

        Report($"Расстояния ({zones.Count} * {zones.Count} зон * {numPairs - 1} перемен)...");
        // Zone-pair travel: cross-building only (intra-building handled per-room via gru).
        // Walk metres = (f1 - 1) * stair + bd(b1, b2) + (f2 - 1) * stair.
        // No direct bd → unreachable (transitively closed bd would have populated it).
        var zoneImpByBreak = new Dictionary<int, HashSet<int>>[numPairs - 1];
        var zonePenByBreak = new Dictionary<int, Dictionary<int, long>>[numPairs - 1];
        var tooFarByBreak = new Dictionary<int, HashSet<int>>[numPairs - 1];
        var walkPenByBreak = new Dictionary<int, Dictionary<int, long>>[numPairs - 1];
        long zoneImpPairCount = 0, zonePenPairCount = 0, tooFarPairCount = 0, walkPenPairCount = 0;
        Parallel.For(0, numPairs - 1, p =>
        {
            double allowedTravelMin = breakMinutes[p];
            long localZoneImp = 0, localZonePen = 0, localTooFar = 0, localWalkPen = 0;

            var zImp = new Dictionary<int, HashSet<int>>();
            var zPen = new Dictionary<int, Dictionary<int, long>>();
            for (int z1 = 0; z1 < zones.Count; z1++)
            for (int z2 = 0; z2 < zones.Count; z2++)
            {
                if (z1 == z2) continue;
                var (b1, f1) = zones[z1];
                var (b2, f2) = zones[z2];
                if (b1 == b2) continue; // intra-building
                if (!distances.TryGetValue((bldgGuidByIdx[b1], bldgGuidByIdx[b2]), out int bd))
                {
                    if (!zImp.TryGetValue(z1, out var s)) zImp[z1] = s = new HashSet<int>();
                    s.Add(z2);
                    localZoneImp++;
                    continue;
                }

                int walkMeters = ZoneEntryWalk(b1, f1) + bd + ZoneEntryWalk(b2, f2);
                double walkMins = walkMeters / WalkSpeedMperMin;
                if (walkMins > allowedTravelMin)
                {
                    if (!zImp.TryGetValue(z1, out var s)) zImp[z1] = s = new HashSet<int>();
                    s.Add(z2);
                    localZoneImp++;
                }
                else if (walkMeters > 0)
                {
                    long pen = Math.Max(1L, (long)(walkMins / allowedTravelMin * w.WalkingPenaltyMax));
                    if (!zPen.TryGetValue(z1, out var m)) zPen[z1] = m = new Dictionary<int, long>();
                    m[z2] = pen;
                    localZonePen++;
                }
            }

            zoneImpByBreak[p] = zImp;
            zonePenByBreak[p] = zPen;

            // Intra-building room-level only.
            var tooFar = new Dictionary<int, HashSet<int>>();
            var walkPen = new Dictionary<int, Dictionary<int, long>>();
            for (int rmi1 = 0; rmi1 < rooms.Count; rmi1++)
            {
                if (rooms[rmi1].IsDistributed) continue;
                int bi1 = roomToBuildingIdx[rmi1];
                for (int rmi2 = 0; rmi2 < rooms.Count; rmi2++)
                {
                    if (rmi1 == rmi2) continue;
                    if (rooms[rmi2].IsDistributed) continue;
                    if (roomToBuildingIdx[rmi2] != bi1) continue;
                    int dist = TravelDistanceMeters(rooms[rmi1], rooms[rmi2], distances, roomDistances);
                    if (dist <= 0) continue;
                    double walkMins = dist / WalkSpeedMperMin;
                    if (walkMins > allowedTravelMin)
                    {
                        if (!tooFar.TryGetValue(rmi1, out var s)) tooFar[rmi1] = s = new HashSet<int>();
                        s.Add(rmi2);
                        localTooFar++;
                    }
                    else
                    {
                        if (!walkPen.TryGetValue(rmi1, out var m)) walkPen[rmi1] = m = new Dictionary<int, long>();
                        m[rmi2] = Math.Max(1L, (long)(walkMins / allowedTravelMin * w.WalkingPenaltyMax));
                        localWalkPen++;
                    }
                }
            }

            tooFarByBreak[p] = tooFar;
            walkPenByBreak[p] = walkPen;

            Interlocked.Add(ref zoneImpPairCount, localZoneImp);
            Interlocked.Add(ref zonePenPairCount, localZonePen);
            Interlocked.Add(ref tooFarPairCount, localTooFar);
            Interlocked.Add(ref walkPenPairCount, localWalkPen);
        });
        ReportSub(
            $"Расстояния: зоны {zoneImpPairCount} запрещ. + {zonePenPairCount} штраф.; комнаты {tooFarPairCount} запрещ. + {walkPenPairCount} штраф.");

        bool needsZoneTravel = zoneImpPairCount > 0 || zonePenPairCount > 0;
        bool needsPerRoomTravel = tooFarPairCount > 0 || walkPenPairCount > 0;

        // gbfVars: per (group, day, pair, wi, zone) occupancy where zone = (building, floor).
        // OR over all req vars of the group landing in any room of that zone at that slot.
        // Drives the zone-level H_travel + S4 constraint families. Built only when there is
        // actual zone-level work; otherwise skipped to save memory.
        Report(needsZoneTravel
            ? $"Занятость зон по группам ({groups.Count} групп * {zones.Count} зон)..."
            : "Занятость зон по группам — пропуск");
        var gbfVars = new Dictionary<(int gi, int d, int p, int wi, int zi), BoolVar>();
        var gbfByCell = new Dictionary<(int gi, int d, int p, int wi), List<(int zi, BoolVar v)>>();
        if (needsZoneTravel)
        {
            int gbfReport = Math.Max(1, groups.Count / 20);
            var temp = new Dictionary<(int d, int p, int wi, int zi), List<BoolVar>>();
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % gbfReport == 0)
                    ReportSub(
                        $"Занятость зон: группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%), {gbfVars.Count} перем.");
                var grIdxs = groupReqs[groups[gi].Id];
                if (grIdxs.Count <= 1) continue;
                temp.Clear();
                foreach (int ri in grIdxs)
                {
                    int varWi = VarWeekIndex(reqs[ri].WeekType);
                    var wis = reqs[ri].WeekType == WeekType.Both ? new[] { 0, 1 } : new[] { varWi };
                    for (int d = 0; d < NumDays; d++)
                    for (int p = 0; p < numPairs; p++)
                    {
                        if (!varsByReqCell.TryGetValue((ri, d, p, varWi), out var cell)) continue;
                        foreach (int wi in wis)
                        foreach (var (rmi, v) in cell)
                        {
                            int zi = roomToZone[rmi];
                            if (zi < 0) continue; // distributed room
                            var key = (d, p, wi, zi);
                            if (!temp.TryGetValue(key, out var lst)) temp[key] = lst = new List<BoolVar>();
                            lst.Add(v);
                        }
                    }
                }

                foreach (var ((d, p, wi, zi), lst) in temp)
                {
                    BoolVar bv;
                    if (lst.Count == 1) bv = lst[0];
                    else
                    {
                        bv = model.NewBoolVar($"gbf_{gi}_{d}_{p}_{wi}_{zi}");
                        model.AddMaxEquality(bv, lst.Select(v => (IntVar)v).ToArray());
                    }

                    gbfVars[(gi, d, p, wi, zi)] = bv;
                    var ck = (gi, d, p, wi);
                    if (!gbfByCell.TryGetValue(ck, out var clist)) gbfByCell[ck] = clist = new List<(int, BoolVar)>();
                    clist.Add((zi, bv));
                }
            }

            ReportSub($"Занятость зон: создано {gbfVars.Count} переменных");
        }

        // gruVars: per (group, day, pair, wi, room) occupancy. Built only when there's residual
        // per-room travel work (intra-building too-far, partial cross-building too-far, or walking
        // penalties). With no distance data this stays empty.
        Report(needsPerRoomTravel
            ? $"Занятость аудиторий по группам (для H_travel/S4)..."
            : "Занятость аудиторий по группам — пропуск");
        var gruVars = new Dictionary<(int gi, int d, int p, int wi, int rmi), BoolVar>();
        var gruByCell = new Dictionary<(int gi, int d, int p, int wi), List<(int rmi, BoolVar v)>>();
        if (needsPerRoomTravel)
        {
            int gruReport = Math.Max(1, groups.Count / 20);
            var temp = new Dictionary<(int d, int p, int wi, int rmi), List<BoolVar>>();
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % gruReport == 0)
                    ReportSub(
                        $"Занятость по группам: {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%), {gruVars.Count} перем.");
                var grIdxs = groupReqs[groups[gi].Id];
                if (grIdxs.Count <= 1) continue;
                temp.Clear();
                foreach (int ri in grIdxs)
                {
                    int varWi = VarWeekIndex(reqs[ri].WeekType);
                    var wis = reqs[ri].WeekType == WeekType.Both ? new[] { 0, 1 } : new[] { varWi };
                    for (int d = 0; d < NumDays; d++)
                    for (int p = 0; p < numPairs; p++)
                    {
                        if (!varsByReqCell.TryGetValue((ri, d, p, varWi), out var cell)) continue;
                        foreach (int wi in wis)
                        foreach (var (rmi, v) in cell)
                        {
                            if (rooms[rmi].IsDistributed) continue;
                            var key = (d, p, wi, rmi);
                            if (!temp.TryGetValue(key, out var lst)) temp[key] = lst = new List<BoolVar>();
                            lst.Add(v);
                        }
                    }
                }

                foreach (var ((d, p, wi, rmi), lst) in temp)
                {
                    BoolVar bv;
                    if (lst.Count == 1) bv = lst[0];
                    else
                    {
                        bv = model.NewBoolVar($"gru_{gi}_{d}_{p}_{wi}_{rmi}");
                        model.AddMaxEquality(bv, lst.Select(v => (IntVar)v).ToArray());
                    }

                    gruVars[(gi, d, p, wi, rmi)] = bv;
                    var ck = (gi, d, p, wi);
                    if (!gruByCell.TryGetValue(ck, out var clist)) gruByCell[ck] = clist = new List<(int, BoolVar)>();
                    clist.Add((rmi, bv));
                }
            }

            ReportSub($"Занятость по группам: создано {gruVars.Count} переменных");
        }

        Report(zoneImpPairCount > 0
            ? $"H_travel (по зонам): {zoneImpPairCount} запрещ. пар зон, {groups.Count} групп..."
            : "H_travel (по зонам): пропуск");
        if (zoneImpPairCount > 0)
        {
            int htzReport = Math.Max(1, groups.Count / 20);
            long htzConstraints = 0;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % htzReport == 0)
                    ReportSub(
                        $"H_travel (зон.): группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%), {htzConstraints} огр.");
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs - 1; p++)
                {
                    var zImp = zoneImpByBreak[p];
                    if (zImp.Count == 0) continue;
                    for (int wi = 0; wi < 2; wi++)
                    {
                        if (!gbfByCell.TryGetValue((gi, d, p, wi), out var cellP)) continue;
                        if (!gbfByCell.TryGetValue((gi, d, p + 1, wi), out var cellP1)) continue;
                        foreach (var (zi1, bv1) in cellP)
                        {
                            if (!zImp.TryGetValue(zi1, out var z2Set)) continue;
                            foreach (var (zi2, bv2) in cellP1)
                            {
                                if (!z2Set.Contains(zi2)) continue;
                                model.Add(LinearExpr.Sum(new BoolVar[] { bv1, bv2 }) <= 1);
                                htzConstraints++;
                            }
                        }
                    }
                }
            }

            ReportSub($"H_travel (зон.): создано {htzConstraints} ограничений");
        }

        Report(tooFarPairCount > 0
            ? $"H_travel (по аудиториям): {tooFarPairCount} остаточных пар, {groups.Count} групп..."
            : "H_travel (по аудиториям): пропуск");
        if (tooFarPairCount > 0)
        {
            int htReport = Math.Max(1, groups.Count / 20);
            long htConstraints = 0;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % htReport == 0)
                    ReportSub(
                        $"H_travel (комн.): группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%), {htConstraints} огр.");
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs - 1; p++)
                {
                    var tooFar = tooFarByBreak[p];
                    if (tooFar.Count == 0) continue;
                    for (int wi = 0; wi < 2; wi++)
                    {
                        if (!gruByCell.TryGetValue((gi, d, p, wi), out var cellP)) continue;
                        if (!gruByCell.TryGetValue((gi, d, p + 1, wi), out var cellP1)) continue;
                        foreach (var (rmi1, bv1) in cellP)
                        {
                            if (!tooFar.TryGetValue(rmi1, out var rmi2Set)) continue;
                            foreach (var (rmi2, bv2) in cellP1)
                            {
                                if (!rmi2Set.Contains(rmi2)) continue;
                                model.Add(LinearExpr.Sum(new BoolVar[] { bv1, bv2 }) <= 1);
                                htConstraints++;
                            }
                        }
                    }
                }
            }

            ReportSub($"H_travel (комн.): создано {htConstraints} ограничений");
        }

        Report($"Вспомогательные переменные занятости ({groups.Count} групп, {teachers.Count} преп.)...");
        var isGroupUsed = new BoolVar[groups.Count, NumDays, numPairs, 2];
        {
            int gReport = Math.Max(1, groups.Count / 20);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % gReport == 0)
                    ReportSub($"isGroupUsed: группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%)");
                var grIdxs = groupReqs[groups[gi].Id];
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                for (int wi = 0; wi < 2; wi++)
                {
                    var slotVars = new List<BoolVar>();
                    foreach (int ri in grIdxs)
                    {
                        if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                        int varWi = VarWeekIndex(reqs[ri].WeekType);
                        if (!varsByReqCell.TryGetValue((ri, d, p, varWi), out var cell)) continue;
                        foreach (var (_, v) in cell) slotVars.Add(v);
                    }

                    BoolVar bv;
                    if (slotVars.Count == 0) bv = Zero;
                    else if (slotVars.Count == 1) bv = slotVars[0];
                    else
                    {
                        bv = model.NewBoolVar($"gu_{gi}_{d}_{p}_{wi}");
                        model.AddMaxEquality(bv, slotVars.Select(v => (IntVar)v).ToArray());
                    }

                    isGroupUsed[gi, d, p, wi] = bv;
                }
            }
        }

        var isTeacherUsed = new BoolVar[teachers.Count, NumDays, numPairs, 2];
        {
            int tReport = Math.Max(1, teachers.Count / 20);
            for (int ti = 0; ti < teachers.Count; ti++)
            {
                if (ti % tReport == 0)
                    ReportSub(
                        $"isTeacherUsed: преп. {ti}/{teachers.Count} ({100 * ti / Math.Max(1, teachers.Count)}%)");
                var trIdxs = teacherReqs[teachers[ti].Id];
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                for (int wi = 0; wi < 2; wi++)
                {
                    var slotVars = new List<BoolVar>();
                    foreach (int ri in trIdxs)
                    {
                        if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                        int varWi = VarWeekIndex(reqs[ri].WeekType);
                        if (!varsByReqCell.TryGetValue((ri, d, p, varWi), out var cell)) continue;
                        foreach (var (_, v) in cell) slotVars.Add(v);
                    }

                    BoolVar bv;
                    if (slotVars.Count == 0) bv = Zero;
                    else if (slotVars.Count == 1) bv = slotVars[0];
                    else
                    {
                        bv = model.NewBoolVar($"tu_{ti}_{d}_{p}_{wi}");
                        model.AddMaxEquality(bv, slotVars.Select(v => (IntVar)v).ToArray());
                    }

                    isTeacherUsed[ti, d, p, wi] = bv;
                }
            }
        }

        // Per-slot group occupancy split by campus vs online — drives the online-aware window penalty.
        var isGroupCampus = new BoolVar[groups.Count, NumDays, numPairs, 2];
        var isGroupOnline = new BoolVar[groups.Count, NumDays, numPairs, 2];
        {
            int gcReport = Math.Max(1, groups.Count / 20);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % gcReport == 0)
                    ReportSub(
                        $"isGroupCampus/Online: группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%)");
                var grIdxs = groupReqs[groups[gi].Id];
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs; p++)
                for (int wi = 0; wi < 2; wi++)
                {
                    var campusVars = new List<BoolVar>();
                    var onlineVars = new List<BoolVar>();
                    foreach (int ri in grIdxs)
                    {
                        if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                        int varWi = VarWeekIndex(reqs[ri].WeekType);
                        if (!varsByReqCell.TryGetValue((ri, d, p, varWi), out var cell)) continue;
                        var target = reqs[ri].IsOnline ? onlineVars : campusVars;
                        foreach (var (_, v) in cell) target.Add(v);
                    }

                    BoolVar bc;
                    if (campusVars.Count == 0) bc = Zero;
                    else if (campusVars.Count == 1) bc = campusVars[0];
                    else
                    {
                        bc = model.NewBoolVar($"gcamp_{gi}_{d}_{p}_{wi}");
                        model.AddMaxEquality(bc, campusVars.Select(v => (IntVar)v).ToArray());
                    }

                    isGroupCampus[gi, d, p, wi] = bc;

                    BoolVar bo;
                    if (onlineVars.Count == 0) bo = Zero;
                    else if (onlineVars.Count == 1) bo = onlineVars[0];
                    else
                    {
                        bo = model.NewBoolVar($"gonl_{gi}_{d}_{p}_{wi}");
                        model.AddMaxEquality(bo, onlineVars.Select(v => (IntVar)v).ToArray());
                    }

                    isGroupOnline[gi, d, p, wi] = bo;
                }
            }
        }
        groupReqs = null!;
        teacherReqs = null!;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        var objVars = new List<IntVar>();
        var objCoeffs = new List<long>();

        Report($"S1: окна у студентов ({groups.Count} групп)...");
        {
            int s1Report = Math.Max(1, groups.Count / 20);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % s1Report == 0)
                    ReportSub(
                        $"S1: группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%), {objVars.Count} слаг.");
                for (int d = 0; d < NumDays; d++)
                for (int wi = 0; wi < 2; wi++)
                {
                    for (int pm = 1; pm < numPairs - 1; pm++)
                    {
                        var before = model.NewBoolVar($"bef_{gi}_{d}_{wi}_{pm}");
                        var after = model.NewBoolVar($"aft_{gi}_{d}_{wi}_{pm}");
                        var beforeVars = Enumerable.Range(0, pm).Select(pp => (IntVar)isGroupCampus[gi, d, pp, wi])
                            .ToArray();
                        var afterVars = Enumerable.Range(pm + 1, numPairs - pm - 1)
                            .Select(pp => (IntVar)isGroupCampus[gi, d, pp, wi]).ToArray();

                        model.AddMaxEquality(before, beforeVars);
                        model.AddMaxEquality(after, afterVars);

                        var windowVar = model.NewBoolVar($"win_{gi}_{d}_{wi}_{pm}");
                        model.Add(
                            (LinearExpr)windowVar >=
                            LinearExpr.Sum(new LinearExpr[] { before, after }) -
                            (LinearExpr)isGroupUsed[gi, d, pm, wi] - 1);

                        objVars.Add(windowVar);
                        objCoeffs.Add(w.StudentWindow);
                    }

                    // Discourage a zero-gap online-to-campus transition (no time to move between home and campus)
                    for (int p = 0; p < numPairs - 1; p++)
                    {
                        var co = model.NewBoolVar($"trCO_{gi}_{d}_{wi}_{p}");
                        model.Add((LinearExpr)co >=
                                  LinearExpr.Sum(new[]
                                  {
                                      (IntVar)isGroupCampus[gi, d, p, wi], (IntVar)isGroupOnline[gi, d, p + 1, wi]
                                  }) - 1);
                        objVars.Add(co);
                        objCoeffs.Add(w.StudentWindow);

                        var oc = model.NewBoolVar($"trOC_{gi}_{d}_{wi}_{p}");
                        model.Add((LinearExpr)oc >=
                                  LinearExpr.Sum(new[]
                                  {
                                      (IntVar)isGroupOnline[gi, d, p, wi], (IntVar)isGroupCampus[gi, d, p + 1, wi]
                                  }) - 1);
                        objVars.Add(oc);
                        objCoeffs.Add(w.StudentWindow);
                    }
                }
            }
        }

        Report($"S2: окна у преподавателей ({teachers.Count} преп.)...");
        {
            int s2Report = Math.Max(1, teachers.Count / 20);
            for (int ti = 0; ti < teachers.Count; ti++)
            {
                if (ti % s2Report == 0)
                    ReportSub(
                        $"S2: преп. {ti}/{teachers.Count} ({100 * ti / Math.Max(1, teachers.Count)}%), {objVars.Count} слаг.");
                for (int d = 0; d < NumDays; d++)
                for (int wi = 0; wi < 2; wi++)
                {
                    for (int pm = 1; pm < numPairs - 1; pm++)
                    {
                        var before = model.NewBoolVar($"tbef_{ti}_{d}_{wi}_{pm}");
                        var after = model.NewBoolVar($"taft_{ti}_{d}_{wi}_{pm}");
                        var beforeVars = Enumerable.Range(0, pm).Select(pp => (IntVar)isTeacherUsed[ti, d, pp, wi])
                            .ToArray();
                        var afterVars = Enumerable.Range(pm + 1, numPairs - pm - 1)
                            .Select(pp => (IntVar)isTeacherUsed[ti, d, pp, wi]).ToArray();

                        model.AddMaxEquality(before, beforeVars);
                        model.AddMaxEquality(after, afterVars);

                        var windowVar = model.NewBoolVar($"twin_{ti}_{d}_{wi}_{pm}");
                        model.Add(
                            (LinearExpr)windowVar >=
                            LinearExpr.Sum(new LinearExpr[] { before, after }) -
                            (LinearExpr)isTeacherUsed[ti, d, pm, wi] - 1);

                        objVars.Add(windowVar);
                        objCoeffs.Add(w.TeacherWindow);
                    }
                }
            }
        }

        Report($"S3: штраф за активные дни группы ({groups.Count} групп)...");
        for (int gi = 0; gi < groups.Count; gi++)
        for (int d = 0; d < NumDays; d++)
        for (int wi = 0; wi < 2; wi++)
        {
            var dayHasClass = model.NewBoolVar($"gd_{gi}_{d}_{wi}");
            var daySlotVars = Enumerable.Range(0, numPairs).Select(p => (IntVar)isGroupUsed[gi, d, p, wi]).ToArray();
            model.AddMaxEquality(dayHasClass, daySlotVars);
            objVars.Add(dayHasClass);
            objCoeffs.Add(w.ActiveDay);
        }

        // S4 (по зонам): cross-building floor-aware walking penalty. One walkPen per
        // (group, transition, zone1, zone2) where zone = (building, floor). Penalty was
        // precomputed using (f1-1)*stair + bd + (f2-1)*stair.
        Report(zonePenPairCount > 0
            ? $"S4 (по зонам): {zonePenPairCount} пар * {groups.Count} групп..."
            : "S4 (по зонам): пропуск");
        if (zonePenPairCount > 0)
        {
            int s4zReport = Math.Max(1, groups.Count / 20);
            long s4zAdded = 0;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % s4zReport == 0)
                    ReportSub(
                        $"S4 (зон.): группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%), {s4zAdded} штраф. перем.");
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs - 1; p++)
                {
                    var penMap = zonePenByBreak[p];
                    if (penMap.Count == 0) continue;
                    for (int wi = 0; wi < 2; wi++)
                    {
                        if (!gbfByCell.TryGetValue((gi, d, p, wi), out var cellP)) continue;
                        if (!gbfByCell.TryGetValue((gi, d, p + 1, wi), out var cellP1)) continue;
                        foreach (var (zi1, bv1) in cellP)
                        {
                            if (!penMap.TryGetValue(zi1, out var innerPens)) continue;
                            foreach (var (zi2, bv2) in cellP1)
                            {
                                if (!innerPens.TryGetValue(zi2, out long penalty)) continue;
                                var walkPen = model.NewBoolVar($"walkZ_{gi}_{d}_{p}_{wi}_{zi1}_{zi2}");
                                model.Add((LinearExpr)walkPen >= LinearExpr.Sum(new BoolVar[] { bv1, bv2 }) - 1);
                                objVars.Add(walkPen);
                                objCoeffs.Add(penalty);
                                s4zAdded++;
                            }
                        }
                    }
                }
            }

            ReportSub($"S4 (зон.): создано {s4zAdded} штрафных переменных");
        }

        // H_travel (зон.) + S4 (зон.) done — drop zone vars/precomp.
        gbfVars = null!;
        gbfByCell = null!;
        zoneImpByBreak = null!;
        zonePenByBreak = null!;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        // S4 by room: intra-building only. Empty when no floor-plan room-distance data.
        Report(walkPenPairCount > 0
            ? $"S4 (по аудиториям, внутри здания): {walkPenPairCount} пар: {groups.Count} групп..."
            : "S4 (по аудиториям): пропуск");
        if (walkPenPairCount > 0)
        {
            int s4Report = Math.Max(1, groups.Count / 20);
            long s4Added = 0;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (gi % s4Report == 0)
                    ReportSub(
                        $"S4 (комн.): группа {gi}/{groups.Count} ({100 * gi / Math.Max(1, groups.Count)}%), {s4Added} штраф. перем.");
                for (int d = 0; d < NumDays; d++)
                for (int p = 0; p < numPairs - 1; p++)
                {
                    var penMap = walkPenByBreak[p];
                    if (penMap.Count == 0) continue;
                    for (int wi = 0; wi < 2; wi++)
                    {
                        if (!gruByCell.TryGetValue((gi, d, p, wi), out var cellP)) continue;
                        if (!gruByCell.TryGetValue((gi, d, p + 1, wi), out var cellP1)) continue;
                        foreach (var (rmi1, bv1) in cellP)
                        {
                            if (!penMap.TryGetValue(rmi1, out var innerPens)) continue;
                            foreach (var (rmi2, bv2) in cellP1)
                            {
                                if (!innerPens.TryGetValue(rmi2, out long penalty)) continue;
                                var walkPen = model.NewBoolVar($"walk_{gi}_{d}_{p}_{wi}_{rmi1}_{rmi2}");
                                model.Add((LinearExpr)walkPen >= LinearExpr.Sum(new BoolVar[] { bv1, bv2 }) - 1);
                                objVars.Add(walkPen);
                                objCoeffs.Add(penalty);
                                s4Added++;
                            }
                        }
                    }
                }
            }

            ReportSub($"S4 (комн.): создано {s4Added} штрафных переменных");
        }

        gruVars = null!;
        gruByCell = null!;
        tooFarByBreak = null!;
        walkPenByBreak = null!;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        // S5: SanPIN — penalize daily pairs > 4 per group (weight 500 per excess pair)
        Report($"S5: штраф СанПиН за >4 пар в день ({groups.Count} групп)...");
        const int sanPinMax = 4;
        for (var gi = 0; gi < groups.Count; gi++)
        for (var d = 0; d < NumDays; d++)
        for (var wi = 0; wi < 2; wi++)
        {
            var dayUsed = Enumerable.Range(0, numPairs).Select(p => (IntVar)isGroupUsed[gi, d, p, wi]).ToArray();
            var overload = model.NewIntVar(0, numPairs - sanPinMax, $"spov_{gi}_{d}_{wi}");
            model.Add(overload >= LinearExpr.Sum(dayUsed) - sanPinMax);
            objVars.Add(overload);
            objCoeffs.Add(w.SanPinOverload);
        }

        // S6: Penalize consecutive pairs of the same (subject, lessonType) for a group
        // S6+scalar: additional penalty for runs of 3+ (harsher scaling with ConsecRunScalar)
        {
            var stGroups = reqs
                .Select((r, i) => (r, i))
                .GroupBy(x => (x.r.SubjectId, x.r.LessonType))
                .ToList();

            Report($"S6: штраф за подряд одинаковых занятий ({stGroups.Count} комб. предмет-тип)...");

            // key: (sti, gi, d, p, wi) -> repPen BoolVar for "same type at p AND p+1"
            var repPenVars = new Dictionary<(int, int, int, int, int), BoolVar>();

            int s6Report = Math.Max(1, stGroups.Count / 20);
            for (int sti = 0; sti < stGroups.Count; sti++)
            {
                if (sti % s6Report == 0)
                    ReportSub(
                        $"S6: комб. {sti}/{stGroups.Count} ({100 * sti / Math.Max(1, stGroups.Count)}%), {objVars.Count} слаг.");
                var stg = stGroups[sti];
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var gId = groups[gi].Id;
                    var riSet = stg.Where(x => x.r.GroupIds.Contains(gId)).Select(x => x.i).ToList();
                    if (riSet.Count < 2) continue;

                    for (int d = 0; d < NumDays; d++)
                    for (int p = 0; p < numPairs - 1; p++)
                    for (int wi = 0; wi < 2; wi++)
                    {
                        var atP = new List<BoolVar>();
                        var atP1 = new List<BoolVar>();
                        foreach (int ri in riSet)
                        {
                            if (!AffectsWeekIndex(reqs[ri].WeekType, wi)) continue;
                            int varWi = VarWeekIndex(reqs[ri].WeekType);
                            if (varsByReqCell.TryGetValue((ri, d, p, varWi), out var cellP))
                                foreach (var (_, v) in cellP)
                                    atP.Add(v);
                            if (varsByReqCell.TryGetValue((ri, d, p + 1, varWi), out var cellP1))
                                foreach (var (_, v) in cellP1)
                                    atP1.Add(v);
                        }

                        if (atP.Count == 0 || atP1.Count == 0) continue;

                        var usedP = model.NewBoolVar($"rep_p_{sti}_{gi}_{d}_{p}_{wi}");
                        var usedP1 = model.NewBoolVar($"rep_p1_{sti}_{gi}_{d}_{p}_{wi}");
                        model.AddMaxEquality(usedP, atP.Select(v => (IntVar)v).ToArray());
                        model.AddMaxEquality(usedP1, atP1.Select(v => (IntVar)v).ToArray());

                        var repPen = model.NewBoolVar($"rep_{sti}_{gi}_{d}_{p}_{wi}");
                        model.Add(repPen <= usedP);
                        model.Add(repPen <= usedP1);
                        model.Add(LinearExpr.Sum(new BoolVar[] { usedP, usedP1 }) <= 1 + repPen);

                        objVars.Add(repPen);
                        objCoeffs.Add(GetLessonTypePenalty(stg.Key.LessonType, w));
                        repPenVars[(sti, gi, d, p, wi)] = repPen;
                    }
                }
            }

            // if repPen[p] and repPen[p+1], its a run of 3+
            if (w.ConsecRunScalar > 1)
            {
                foreach (var ((sti, gi, d, p, wi), rep1) in repPenVars)
                {
                    if (!repPenVars.TryGetValue((sti, gi, d, p + 1, wi), out var rep2)) continue;
                    var lt = stGroups[sti].Key.LessonType;
                    var pen3 = model.NewBoolVar($"rep3_{sti}_{gi}_{d}_{p}_{wi}");
                    model.Add(pen3 <= rep1);
                    model.Add(pen3 <= rep2);
                    model.Add(LinearExpr.Sum(new BoolVar[] { rep1, rep2 }) <= 1 + pen3);
                    objVars.Add(pen3);
                    objCoeffs.Add(GetLessonTypePenalty(lt, w) * (long)(w.ConsecRunScalar - 1));
                }
            }
        }

        // S7: Prefer pairs 2-3 (0-indexed) — penalize early/late slots per group
        Report("S7: предпочтительное время занятий...");
        for (int gi = 0; gi < groups.Count; gi++)
        for (int d = 0; d < NumDays; d++)
        for (int wi = 0; wi < 2; wi++)
        for (int p = 0; p < numPairs; p++)
        {
            long pen = PairPositionPenalty(p, w);
            if (pen <= 0) continue;
            objVars.Add(isGroupUsed[gi, d, p, wi]);
            objCoeffs.Add(pen);
        }

        // S8: Saturday discouragement — soft penalty per group slot on day 5 (Saturday)
        const int saturdayIdx = 5;
        Report(w.SaturdayPenalty > 0 ? "S8: штраф за субботу..." : "S8: пропуск (SaturdayPenalty=0)");
        if (w.SaturdayPenalty > 0)
        {
            for (int gi = 0; gi < groups.Count; gi++)
            for (int p = 0; p < numPairs; p++)
            for (int wi = 0; wi < 2; wi++)
            {
                objVars.Add(isGroupUsed[gi, saturdayIdx, p, wi]);
                objCoeffs.Add(w.SaturdayPenalty);
            }
        }

        Report(w.DepartmentMismatchPenalty > 0
            ? "S9: штраф за несоответствие кафедры..."
            : "S9: пропуск (DepartmentMismatchPenalty=0)");
        if (w.DepartmentMismatchPenalty > 0)
        {
            foreach (var ((ri, _, _, _), cell) in varsByReqCell)
            {
                var subjFacultyId = reqs[ri].SubjectFacultyId;
                if (!subjFacultyId.HasValue) continue;
                foreach (var (rmi, v) in cell)
                {
                    var roomFacultyId = rooms[rmi].DepartmentFacultyId;
                    if (roomFacultyId.HasValue && roomFacultyId != subjFacultyId)
                    {
                        objVars.Add(v);
                        objCoeffs.Add(w.DepartmentMismatchPenalty);
                    }
                }
            }
        }

        if (objVars.Count > 0)
            model.Minimize(LinearExpr.WeightedSum(objVars.ToArray(), objCoeffs.ToArray()));

        var extractList = new List<(int ri, int d, int p, int wi, int rmi, BoolVar v)>(objVars.Count);
        foreach (var ((ri, d, p, wi), cell) in varsByReqCell)
        foreach (var (rmi, v) in cell)
            extractList.Add((ri, d, p, wi, rmi, v));
        varsByReqCell = null!;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();

        Report($"Запуск решателя (таймаут {input.SolverTimeoutSeconds}с, {objVars.Count} слагаемых)...");
        var workers = int.TryParse(Environment.GetEnvironmentVariable(SchedulerEnv.SolverNumWorkers), out var nw) && nw > 0
            ? nw
            : Math.Max(2, Environment.ProcessorCount - 1);
        int linLevel = int.TryParse(Environment.GetEnvironmentVariable(SchedulerEnv.SolverLinearizationLevel), out var ll) &&
                       ll >= 0
            ? ll
            : 1;
        int probingLevel = int.TryParse(Environment.GetEnvironmentVariable(SchedulerEnv.SolverProbingLevel), out var pl) &&
                           pl >= 0
            ? pl
            : 1;
        var solver = new CpSolver();
        solver.StringParameters =
            $"max_time_in_seconds:{input.SolverTimeoutSeconds}," +
            $"num_search_workers:{workers}," +
            $"linearization_level:{linLevel}," +
            $"cp_model_probing_level:{probingLevel}," +
            "log_search_progress:false";

        CpSolverStatus status;
        using (cancellationToken.Register(() => solver.StopSearch()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            status = solver.Solve(model);
        }
        cancellationToken.ThrowIfCancellationRequested();

        switch (status)
        {
            case CpSolverStatus.Infeasible:
                return new SchedulerOutput(SolverStatus.Infeasible,
                    "No feasible schedule found. Check room availability, teacher blocks, and building travel times.",
                    []);
            case CpSolverStatus.Unknown:
                return new SchedulerOutput(SolverStatus.Unknown,
                    "Solver reached time limit without finding a feasible schedule.",
                    []);
        }

        Report($"Извлечение результатов (статус: {status})...");
        var assignments = new List<SchedulerAssignment>();
        foreach (var (ri, d, p, wi, rmi, v) in extractList)
        {
            if (!solver.BooleanValue(v)) continue;
            var weekType = reqs[ri].WeekType == WeekType.Both
                ? WeekType.Both
                : (wi == 0 ? WeekType.Odd : WeekType.Even);
            assignments.Add(new SchedulerAssignment(ri, (RussianDayOfWeek)(d + 1), p + 1, weekType, rooms[rmi].Id));
        }

        var solverStatus = status == CpSolverStatus.Optimal ? SolverStatus.Optimal : SolverStatus.Feasible;
        return new SchedulerOutput(solverStatus,
            $"Status: {status}, Objective: {(objVars.Count > 0 ? solver.ObjectiveValue.ToString("F0") : "N/A")}, Entries: {assignments.Count}",
            assignments);
    }

    /// <summary>
    /// The variable key wi used for a requirement. Both/Odd use wi=0; Even uses wi=1.
    /// </summary>
    private static int VarWeekIndex(WeekType wt) => wt == WeekType.Even ? 1 : 0;

    private static string FormatReqLabel(SchedulerRequirement req)
    {
        var grp = req.GroupIds.Count > 0 ? $"…{req.GroupIds[0].ToString("D")[..8]}" : "—";
        var sub = req.SubgroupLabel != null ? $" [{req.SubgroupLabel}]" : "";
        return $"{req.LessonType}{sub} teacher=…{req.TeacherId.ToString("D")[..8]} subj=…{req.SubjectId.ToString("D")[..8]} grp={grp} wk={req.WeekType}";
    }

    /// <summary>
    /// True if a requirement with this WeekType fires during calendar week wi (0=odd, 1=even).
    /// Both fires every week; Odd only on odd; Even only on even.
    /// </summary>
    private static bool AffectsWeekIndex(WeekType wt, int wi) => wt switch
    {
        WeekType.Both => true,
        WeekType.Odd => wi == 0,
        WeekType.Even => wi == 1,
        _ => true
    };

    private static int[] BuildBreakArray(IReadOnlyList<int>? breaks, int numPairs)
    {
        int gaps = numPairs - 1;
        if (breaks != null && breaks.Count >= gaps)
            return breaks.Take(gaps).ToArray();
        return Enumerable.Repeat(15, gaps).ToArray();
    }

    private static Dictionary<(Guid, Guid), int> BuildDistanceMap(IEnumerable<SchedulerBuildingDistance> distances)
    {
        var map = new Dictionary<(Guid, Guid), int>();
        foreach (var d in distances)
        {
            map[(d.FromId, d.ToId)] = d.DistanceMeters;
            map[(d.ToId, d.FromId)] = d.DistanceMeters;
        }

        return map;
    }

    private static Dictionary<(Guid, Guid), int> BuildRoomDistanceMap(IReadOnlyList<SchedulerRoomDistance>? distances)
    {
        var map = new Dictionary<(Guid, Guid), int>();
        if (distances == null) return map;
        foreach (var d in distances)
        {
            map[(d.FromRoomId, d.ToRoomId)] = d.DistanceMeters;
            map[(d.ToRoomId, d.FromRoomId)] = d.DistanceMeters;
        }

        return map;
    }

    private const int ImpossibleDistanceMeters = 10_000_000;

    private static int TravelDistanceMeters(SchedulerRoom r1, SchedulerRoom r2,
        Dictionary<(Guid, Guid), int> buildingDistanceMap,
        Dictionary<(Guid, Guid), int> roomDistanceMap)
    {
        if (r1.BuildingId == r2.BuildingId)
            return roomDistanceMap.TryGetValue((r1.Id, r2.Id), out int rd) ? rd : 0;

        if (!buildingDistanceMap.TryGetValue((r1.BuildingId, r2.BuildingId), out int interBuilding))
            return ImpossibleDistanceMeters;

        return r1.EntryDistanceMeters + interBuilding + r2.EntryDistanceMeters;
    }

    private static HashSet<(Guid, int, int, int)> BuildBlockedSet(IEnumerable<SchedulerBlock> blocks)
    {
        var set = new HashSet<(Guid, int, int, int)>();
        foreach (var b in blocks)
        {
            // Both-type blocks must cover both week indices
            foreach (int wi in b.WeekType == WeekType.Both ? new[] { 0, 1 } : new[] { VarWeekIndex(b.WeekType) })
                set.Add((b.TeacherId, (int)b.Day - 1, b.PairNumber - 1, wi));
        }

        return set;
    }

    private static HashSet<(Guid, int, int, int)> BuildBlockedRoomSlots(IEnumerable<SchedulerRoomBlock>? blocks)
    {
        var set = new HashSet<(Guid, int, int, int)>();
        if (blocks == null) return set;
        foreach (var b in blocks)
        {
            // Both-type blocks cover BOTH calendar weeks (the room is occupied every week).
            foreach (int wi in b.WeekType == WeekType.Both ? new[] { 0, 1 } : new[] { VarWeekIndex(b.WeekType) })
                set.Add((b.RoomId, (int)b.Day - 1, b.PairNumber - 1, wi));
        }
        return set;
    }

    private static bool IsCompatible(SchedulerRequirement req, SchedulerRoom room, int headcount)
    {
        // Distributed sentinel room: only no-fixed-location requirements (language streams) may use
        // it, and they may use nothing else. It has no capacity/equipment constraints.
        if (req.RequiresDistributedRoom) return room.IsDistributed;
        if (room.IsDistributed) return false;

        // Sports-hall routing: PE reqs go only to SportsHall rooms; no other req type may pick one.
        // No equipment / capacity / lesson-type heuristic — the venue type is the whole gate.
        if (req.RequiresSportsHall) return room.RoomType == RoomType.SportsHall;
        if (room.RoomType == RoomType.SportsHall) return false;

        if (req.IsOnline) return room.IsOnline;
        if (room.IsOnline) return false;
        if (req.NeedsProjector && !room.HasProjector) return false;
        if (req.NeedsComputers && !room.HasComputers) return false;
        if (req.NeedsLab && !room.HasLab) return false;
        if (room.AllowedLessonTypes is { Count: > 0 } allowed && !allowed.Contains(req.LessonType)) return false;

        if (room.Capacity < headcount) return false;

        // An explicit AllowedLessonTypes is the admin's deliberate opt-in and overrides the
        // RoomType heuristic below (we already returned false above if it excluded this type).
        if (room.AllowedLessonTypes is { Count: > 0 }) return true;

        return (req.LessonType, room.RoomType) switch
        {
            (LessonType.Lecture, RoomType.LectureHall) => true,
            (LessonType.Lecture, RoomType.RegularCabinet) => true,
            (LessonType.Practical, RoomType.RegularCabinet) => true,
            (LessonType.Practical, RoomType.LectureHall) => true,
            (LessonType.Practical, RoomType.ComputerLab) => true,
            (LessonType.Seminar, RoomType.RegularCabinet) => true,
            (LessonType.Lab, RoomType.Lab) => true,
            (LessonType.Lab, RoomType.ComputerLab) => req.NeedsComputers,
            _ => false
        };
    }

    private static long GetLessonTypePenalty(LessonType lt, SolverWeights w) => lt switch
    {
        LessonType.Lecture => w.ConsecLecture,
        LessonType.Practical => w.ConsecPractical,
        LessonType.Seminar => w.ConsecSeminar,
        LessonType.Lab => w.ConsecLab,
        _ => 0L
    };

    // Pairs 2-3 (0-indexed) preferred; middle pairs get flat penalty, early/late scale linearly.
    private static long PairPositionPenalty(int p, SolverWeights w)
    {
        if (p < 2) return w.EarlyPair * (long)(2 - p);
        if (p > 3) return w.LatePair * (long)(p - 3);
        return w.MiddlePair;
    }
}