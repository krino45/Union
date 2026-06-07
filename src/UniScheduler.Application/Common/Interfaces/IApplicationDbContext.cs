using Microsoft.EntityFrameworkCore;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<University> Universities { get; }
    DbSet<UserUniversityAccess> UserUniversityAccesses { get; }
    DbSet<FloorPlanDraft> FloorPlanDrafts { get; }
    DbSet<FloorPlan> FloorPlans { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Building> Buildings { get; }
    DbSet<BuildingDistance> BuildingDistances { get; }
    DbSet<FloorPlanNode> FloorPlanNodes { get; }
    DbSet<FloorPlanEdge> FloorPlanEdges { get; }
    DbSet<EntranceConnection> EntranceConnections { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Department> Departments { get; }
    DbSet<GroupBlockedDay> GroupBlockedDays { get; }
    DbSet<Faculty> Faculties { get; }
    DbSet<Teacher> Teachers { get; }
    DbSet<TeacherSubject> TeacherSubjects { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<SubjectRoomBinding> SubjectRoomBindings { get; }
    DbSet<StudentGroup> StudentGroups { get; }
    DbSet<Schedule> Schedules { get; }
    DbSet<ScheduleEntry> ScheduleEntries { get; }
    DbSet<ScheduleEntryStudentGroup> ScheduleEntryStudentGroups { get; }
    DbSet<TeacherAvailability> TeacherAvailabilities { get; }
    DbSet<RescheduleRequest> RescheduleRequests { get; }
    DbSet<AppUser> AppUsers { get; }
    DbSet<PairTimeSlot> PairTimeSlots { get; }
    DbSet<StudyPlan> StudyPlans { get; }
    DbSet<StudyPlanGroup> StudyPlanGroups { get; }
    DbSet<StudyPlanEntry> StudyPlanEntries { get; }
    DbSet<CalendarPlan> CalendarPlans { get; }
    DbSet<CalendarWeek> CalendarWeeks { get; }
    DbSet<SolverSettings> SolverSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
