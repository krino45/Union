using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingDistance> BuildingDistances => Set<BuildingDistance>();
    public DbSet<FloorPlanNode> FloorPlanNodes => Set<FloorPlanNode>();
    public DbSet<FloorPlanEdge> FloorPlanEdges => Set<FloorPlanEdge>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<ScheduleEntryStudentGroup> ScheduleEntryStudentGroups => Set<ScheduleEntryStudentGroup>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<RescheduleRequest> RescheduleRequests => Set<RescheduleRequest>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<PairTimeSlot> PairTimeSlots => Set<PairTimeSlot>();
    public DbSet<StudyPlan> StudyPlans => Set<StudyPlan>();
    public DbSet<StudyPlanGroup> StudyPlanGroups => Set<StudyPlanGroup>();
    public DbSet<StudyPlanEntry> StudyPlanEntries => Set<StudyPlanEntry>();
    public DbSet<CalendarPlan> CalendarPlans => Set<CalendarPlan>();
    public DbSet<CalendarWeek> CalendarWeeks => Set<CalendarWeek>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
