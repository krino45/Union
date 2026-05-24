using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUniversityService _currentUniversity;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUniversityService currentUniversity) : base(options)
    {
        _currentUniversity = currentUniversity;
    }

    public DbSet<University> Universities => Set<University>();
    public DbSet<UserUniversityAccess> UserUniversityAccesses => Set<UserUniversityAccess>();
    public DbSet<FloorPlanDraft> FloorPlanDrafts => Set<FloorPlanDraft>();
    public DbSet<FloorPlan> FloorPlans => Set<FloorPlan>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingDistance> BuildingDistances => Set<BuildingDistance>();
    public DbSet<FloorPlanNode> FloorPlanNodes => Set<FloorPlanNode>();
    public DbSet<FloorPlanEdge> FloorPlanEdges => Set<FloorPlanEdge>();
    public DbSet<EntranceConnection> EntranceConnections => Set<EntranceConnection>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<GroupBlockedDay> GroupBlockedDays => Set<GroupBlockedDay>();
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
    public DbSet<SolverSettings> SolverSettings => Set<SolverSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global query filters: scope all root-level tenant entities to the current university.
        // SuperAdmin requests (no X-University-Id header) bypass filters via IgnoreQueryFilters().
        modelBuilder.Entity<Faculty>()
            .HasQueryFilter(e => !_currentUniversity.HasContext || e.UniversityId == _currentUniversity.UniversityId);
        modelBuilder.Entity<Building>()
            .HasQueryFilter(e => !_currentUniversity.HasContext || e.UniversityId == _currentUniversity.UniversityId);
        modelBuilder.Entity<Teacher>()
            .HasQueryFilter(e => !_currentUniversity.HasContext || e.UniversityId == _currentUniversity.UniversityId);
        modelBuilder.Entity<CalendarPlan>()
            .HasQueryFilter(e => !_currentUniversity.HasContext || e.UniversityId == _currentUniversity.UniversityId);
        modelBuilder.Entity<PairTimeSlot>()
            .HasQueryFilter(e => !_currentUniversity.HasContext || e.UniversityId == _currentUniversity.UniversityId);
        modelBuilder.Entity<Schedule>()
            .HasQueryFilter(e => !_currentUniversity.HasContext || e.UniversityId == _currentUniversity.UniversityId);
        modelBuilder.Entity<SolverSettings>()
            .HasQueryFilter(e => !_currentUniversity.HasContext || e.UniversityId == _currentUniversity.UniversityId);

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-populate UniversityId on new entities when university context is present
        if (_currentUniversity.HasContext)
        {
            var universityId = _currentUniversity.UniversityId!.Value;
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added) continue;
                var prop = entry.Metadata.FindProperty("UniversityId");
                if (prop == null) continue;
                var current = entry.CurrentValues[prop];
                if (current is Guid id && id == Guid.Empty)
                    entry.CurrentValues[prop] = universityId;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
