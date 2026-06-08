using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.StudyPlans;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules.Internal;

public static class ScheduleRequirementBuilder
{
    // Per-plan emitter - used by the batched seed pass.
    public static List<SchedulerRequirement> BuildRequirementsForPlan(
        StudyPlan plan, SharedData shared, ref int idx, ref int parallelSeq,
        Dictionary<Guid, int>? teacherLoad = null)
    {
        var requirements = new List<SchedulerRequirement>();
        int studyWeeks = StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan);
        var planGroupIds = plan.Groups
            .Select(g => g.StudentGroupId)
            .Where(gid => shared.GroupIds.Contains(gid))
            .ToList();
        if (planGroupIds.Count == 0) return requirements;

        foreach (var entry in plan.Entries)
        {
            shared.SubjectFacultyIds.TryGetValue(entry.SubjectId, out var subjFacultyId);
            shared.SubjectsById.TryGetValue(entry.SubjectId, out var subj);

            AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lecture,
                entry.LectureHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: true, subjFacultyId,
                needsProjector: subj?.RequiresProjector ?? false);
            AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Practical,
                entry.PracticalHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: false, subjFacultyId);

            if (subj is { AllowsSubgroups: true } &&
                AddSubgroupLabRequirements(requirements, ref idx, ref parallelSeq, entry.SubjectId,
                    entry.LabHours, studyWeeks, planGroupIds, shared.TeacherSubjects,
                    subj.SubgroupCount, shared.GroupSizes, subjFacultyId))
            {
                // emitted as subgroups
            }
            else
            {
                AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Lab,
                    entry.LabHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: false, subjFacultyId);
            }

            AddRequirements(requirements, ref idx, entry.SubjectId, LessonType.Seminar,
                entry.SeminarHours, studyWeeks, planGroupIds, shared.TeacherSubjects, merged: false, subjFacultyId);

            AddLanguageRequirements(requirements, ref idx, ref parallelSeq, entry.SubjectId,
                entry.LanguageHours, studyWeeks, planGroupIds, shared.TeacherSubjects,
                shared.GroupSizes, subjFacultyId, shared.Weights.LanguagePerTeacherCap, teacherLoad);

            AddPhysicalEducationRequirements(requirements, ref idx, ref parallelSeq, entry.SubjectId,
                entry.PhysicalEducationHours, studyWeeks, planGroupIds, shared.TeacherSubjects,
                shared.GroupSizes, subjFacultyId, shared.Weights.PhysicalEducationPerTeacherCap, teacherLoad);
        }

        ApplyRoomBindings(requirements, shared.SubjectRoomBindings);
        return requirements;
    }

    // Builds the FULL requirement list for every plan in stable order. Used by LNS so that
    // requirement indices match between the seed pass and repair passes. Plans, entries within a
    // plan, and groups within a plan are all sorted by Id (deterministic across EF loads).
    public static (List<SchedulerRequirement> Reqs, Dictionary<int, Guid> RiToPlanId)
        BuildAllRequirementsStable(SharedData shared)
    {
        var reqs = new List<SchedulerRequirement>();
        var riToPlanId = new Dictionary<int, Guid>();
        int idx = 0;
        int parallelSeq = 1;

        foreach (var plan in shared.StudyPlans.OrderBy(p => p.Id))
        {
            var sortedEntries = plan.Entries.OrderBy(e => e.SubjectId).ThenBy(e => e.Id).ToList();
            var sortedGroupIds = plan.Groups
                .Select(g => g.StudentGroupId)
                .Where(gid => shared.GroupIds.Contains(gid))
                .OrderBy(gid => gid)
                .ToList();
            if (sortedGroupIds.Count == 0) continue;

            int before = idx;
            foreach (var entry in sortedEntries)
            {
                shared.SubjectFacultyIds.TryGetValue(entry.SubjectId, out var subjFacultyId);
                shared.SubjectsById.TryGetValue(entry.SubjectId, out var subj);

                AddRequirements(reqs, ref idx, entry.SubjectId, LessonType.Lecture,
                    entry.LectureHours, StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan), sortedGroupIds,
                    shared.TeacherSubjects, merged: true, subjFacultyId,
                    needsProjector: subj?.RequiresProjector ?? false);
                AddRequirements(reqs, ref idx, entry.SubjectId, LessonType.Practical,
                    entry.PracticalHours, StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan), sortedGroupIds,
                    shared.TeacherSubjects, merged: false, subjFacultyId);

                if (subj is { AllowsSubgroups: true } &&
                    AddSubgroupLabRequirements(reqs, ref idx, ref parallelSeq, entry.SubjectId,
                        entry.LabHours, StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan), sortedGroupIds,
                        shared.TeacherSubjects, subj.SubgroupCount, shared.GroupSizes, subjFacultyId))
                {
                    // emitted as subgroups
                }
                else
                {
                    AddRequirements(reqs, ref idx, entry.SubjectId, LessonType.Lab,
                        entry.LabHours, StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan), sortedGroupIds,
                        shared.TeacherSubjects, merged: false, subjFacultyId);
                }

                AddRequirements(reqs, ref idx, entry.SubjectId, LessonType.Seminar,
                    entry.SeminarHours, StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan), sortedGroupIds,
                    shared.TeacherSubjects, merged: false, subjFacultyId);

                AddLanguageRequirements(reqs, ref idx, ref parallelSeq, entry.SubjectId,
                    entry.LanguageHours, StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan), sortedGroupIds,
                    shared.TeacherSubjects, shared.GroupSizes, subjFacultyId, shared.Weights.LanguagePerTeacherCap);

                AddPhysicalEducationRequirements(reqs, ref idx, ref parallelSeq, entry.SubjectId,
                    entry.PhysicalEducationHours, StudyPlanQ.StudyWeeksFromPlan(plan.CalendarPlan), sortedGroupIds,
                    shared.TeacherSubjects, shared.GroupSizes, subjFacultyId, shared.Weights.PhysicalEducationPerTeacherCap);
            }
            for (int i = before; i < idx; i++) riToPlanId[i] = plan.Id;
        }

        ApplyRoomBindings(reqs, shared.SubjectRoomBindings);
        return (reqs, riToPlanId);
    }

    // Stamp the hard (subject, lessonType) -> allowed-room set onto matching requirements.
    private static void ApplyRoomBindings(
        List<SchedulerRequirement> reqs,
        IReadOnlyDictionary<(Guid SubjectId, LessonType LessonType), List<Guid>> bindings)
    {
        if (bindings == null || bindings.Count == 0) return;
        for (int i = 0; i < reqs.Count; i++)
            if (bindings.TryGetValue((reqs[i].SubjectId, reqs[i].LessonType), out var rooms) && rooms.Count > 0)
                reqs[i] = reqs[i] with { AllowedRoomIds = rooms };
    }

    private static void AddRequirements(
        List<SchedulerRequirement> requirements, ref int idx,
        Guid subjectId, LessonType lt, double totalHours, int studyWeeks,
        List<Guid> planGroupIds, List<TeacherSubject> teacherSubjects,
        bool merged, Guid? subjectFacultyId = null, bool needsProjector = false)
    {
        if (totalHours <= 0) return;
        var teachers = teacherSubjects
            .Where(ts => ts.SubjectId == subjectId && ts.LessonType == lt)
            .Select(ts => ts.TeacherId).OrderBy(g => g).ToList();
        if (teachers.Count == 0) return;

        foreach (var wt in HoursToWeekTypes(totalHours, studyWeeks))
        {
            if (merged)
            {
                var chunks = SplitRoundRobin(planGroupIds, teachers.Count);
                for (int i = 0; i < teachers.Count; i++)
                {
                    if (chunks[i].Count == 0) continue;
                    requirements.Add(new SchedulerRequirement(idx++, chunks[i], subjectId, lt, teachers[i], wt, false, needsProjector, false, subjectFacultyId));
                }
            }
            else
            {
                for (int gi = 0; gi < planGroupIds.Count; gi++)
                {
                    var teacherId = teachers[gi % teachers.Count];
                    requirements.Add(new SchedulerRequirement(idx++, [planGroupIds[gi]], subjectId, lt, teacherId, wt, false, false, false, subjectFacultyId));
                }
            }
        }
    }

    private static List<WeekType> HoursToWeekTypes(double totalHours, int studyWeeks)
    {
        if (studyWeeks <= 0 || totalHours <= 0) return [];
        double pairsPerWeek = totalHours / 2.0 / studyWeeks;
        var result = new List<WeekType>();
        int whole = (int)pairsPerWeek;
        for (int i = 0; i < whole; i++) result.Add(WeekType.Both);
        double frac = pairsPerWeek - whole;
        if (frac >= 0.25) result.Add(WeekType.Odd);
        return result;
    }

    private static List<List<Guid>> SplitRoundRobin(List<Guid> items, int buckets)
    {
        var result = Enumerable.Range(0, buckets).Select(_ => new List<Guid>()).ToList();
        for (int i = 0; i < items.Count; i++)
            result[i % buckets].Add(items[i]);
        return result;
    }

    private static void AddLanguageRequirements(
        List<SchedulerRequirement> requirements, ref int idx, ref int parallelSeq,
        Guid subjectId, double totalHours, int studyWeeks,
        List<Guid> planGroupIds, List<TeacherSubject> teacherSubjects,
        Dictionary<Guid, int> groupSizes, Guid? subjectFacultyId,
        int perTeacherCap,
        Dictionary<Guid, int>? teacherLoad = null)
    {
        if (totalHours <= 0) return;
        var teachers = teacherSubjects
            .Where(ts => ts.SubjectId == subjectId && ts.LessonType == LessonType.Language)
            .Select(ts => ts.TeacherId).Distinct().OrderBy(g => g).ToList();
        if (teachers.Count == 0) return;

        if (perTeacherCap <= 0) perTeacherCap = 15;

        foreach (var wt in HoursToWeekTypes(totalHours, studyWeeks))
        {
            foreach (var gId in planGroupIds)
            {
                int groupSize = groupSizes.TryGetValue(gId, out var sz) && sz > 0 ? sz : perTeacherCap;
                int needed = Math.Clamp((int)Math.Ceiling(groupSize / (double)perTeacherCap), 1, teachers.Count);
                int pkey = parallelSeq++;
                int perTeacherHeadcount = (int)Math.Ceiling(groupSize / (double)needed);
                var picks = PickLeastLoadedTeachers(teachers, needed, pkey, wt, teacherLoad);
                for (int i = 0; i < needed; i++)
                {
                    requirements.Add(new SchedulerRequirement(
                        idx++, new[] { gId }, subjectId, LessonType.Language, picks[i], wt,
                        IsOnline: false, NeedsProjector: false, NeedsComputers: false,
                        SubjectFacultyId: subjectFacultyId,
                        ParallelKey: pkey,
                        SubgroupLabel: needed > 1 ? $"Поток {i + 1}" : null,
                        HeadcountOverride: perTeacherHeadcount,
                        RequiresDistributedRoom: true));
                }
            }
        }
    }

    private static List<Guid> PickLeastLoadedTeachers(
        IReadOnlyList<Guid> teachers, int needed, int pkey, WeekType wt, Dictionary<Guid, int>? teacherLoad)
    {
        if (teacherLoad == null)
        {
            int offset = (pkey * needed) % teachers.Count;
            return Enumerable.Range(0, needed).Select(i => teachers[(offset + i) % teachers.Count]).ToList();
        }
        int n = Math.Min(needed, teachers.Count);
        var ordered = teachers
            .OrderBy(t => teacherLoad.GetValueOrDefault(t, 0))
            .ThenBy(t => t)
            .ToList();
        int off = pkey % ordered.Count;
        int cellsPerPick = wt == WeekType.Both ? 2 : 1;
        var picks = new List<Guid>(n);
        for (int i = 0; i < n; i++)
        {
            var t = ordered[(off + i) % ordered.Count];
            picks.Add(t);
            teacherLoad[t] = teacherLoad.GetValueOrDefault(t, 0) + cellsPerPick;
        }
        return picks;
    }

    private static bool AddSubgroupLabRequirements(
        List<SchedulerRequirement> requirements, ref int idx, ref int parallelSeq,
        Guid subjectId, double totalHours, int studyWeeks,
        List<Guid> planGroupIds, List<TeacherSubject> teacherSubjects,
        int subgroupCount, Dictionary<Guid, int> groupSizes, Guid? subjectFacultyId)
    {
        if (totalHours <= 0) return false;
        var teachers = teacherSubjects
            .Where(ts => ts.SubjectId == subjectId && ts.LessonType == LessonType.Lab)
            .Select(ts => ts.TeacherId).Distinct().OrderBy(g => g).ToList();
        int n = Math.Min(Math.Max(2, subgroupCount), teachers.Count);
        if (n < 2) return false;

        foreach (var wt in HoursToWeekTypes(totalHours, studyWeeks))
        {
            foreach (var gId in planGroupIds)
            {
                int pkey = parallelSeq++;
                int total = groupSizes.TryGetValue(gId, out var sz) ? sz : 0;
                int per = total > 0 ? (int)Math.Ceiling(total / (double)n) : 0;
                for (int s = 0; s < n; s++)
                {
                    requirements.Add(new SchedulerRequirement(
                        idx++, new[] { gId }, subjectId, LessonType.Lab, teachers[s], wt,
                        IsOnline: false, NeedsProjector: false, NeedsComputers: false,
                        SubjectFacultyId: subjectFacultyId,
                        ParallelKey: pkey,
                        SubgroupLabel: $"Подгр. {s + 1}",
                        HeadcountOverride: per > 0 ? per : (int?)null,
                        RequiresDistributedRoom: false));
                }
            }
        }
        return true;
    }

    // Same shape as AddLanguageRequirements but routed to the SportsHall pool.
    private static void AddPhysicalEducationRequirements(
        List<SchedulerRequirement> requirements, ref int idx, ref int parallelSeq,
        Guid subjectId, double totalHours, int studyWeeks,
        List<Guid> planGroupIds, List<TeacherSubject> teacherSubjects,
        Dictionary<Guid, int> groupSizes, Guid? subjectFacultyId,
        int perTeacherCap,
        Dictionary<Guid, int>? teacherLoad = null)
    {
        if (totalHours <= 0) return;
        var teachers = teacherSubjects
            .Where(ts => ts.SubjectId == subjectId && ts.LessonType == LessonType.PhysicalEducation)
            .Select(ts => ts.TeacherId).Distinct().OrderBy(g => g).ToList();
        if (teachers.Count == 0) return;

        if (perTeacherCap <= 0) perTeacherCap = 40;

        foreach (var wt in HoursToWeekTypes(totalHours, studyWeeks))
        {
            foreach (var gId in planGroupIds)
            {
                int groupSize = groupSizes.TryGetValue(gId, out var sz) && sz > 0 ? sz : perTeacherCap;
                int needed = Math.Clamp((int)Math.Ceiling(groupSize / (double)perTeacherCap), 1, teachers.Count);
                int pkey = parallelSeq++;
                int perTeacherHeadcount = (int)Math.Ceiling(groupSize / (double)needed);
                var picks = PickLeastLoadedTeachers(teachers, needed, pkey, wt, teacherLoad);
                for (int i = 0; i < needed; i++)
                {
                    requirements.Add(new SchedulerRequirement(
                        idx++, new[] { gId }, subjectId, LessonType.PhysicalEducation, picks[i], wt,
                        IsOnline: false, NeedsProjector: false, NeedsComputers: false,
                        SubjectFacultyId: subjectFacultyId,
                        ParallelKey: pkey,
                        SubgroupLabel: needed > 1 ? $"Группа {i + 1}" : null,
                        HeadcountOverride: perTeacherHeadcount,
                        RequiresDistributedRoom: false,
                        RequiresSportsHall: true));
                }
            }
        }
    }
}
