using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class CalendarPlanConfiguration : IEntityTypeConfiguration<CalendarPlan>
{
    public void Configure(EntityTypeBuilder<CalendarPlan> builder)
    {
        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Name).IsRequired().HasMaxLength(200);
        builder.Property(cp => cp.AcademicYear).IsRequired();
        builder.Property(cp => cp.Term).IsRequired();

        builder.HasMany(cp => cp.Weeks)
               .WithOne(w => w.CalendarPlan)
               .HasForeignKey(w => w.CalendarPlanId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CalendarWeekConfiguration : IEntityTypeConfiguration<CalendarWeek>
{
    public void Configure(EntityTypeBuilder<CalendarWeek> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.StartDate).IsRequired();
        builder.Property(w => w.Kind).IsRequired();
        builder.Property(w => w.Note).HasMaxLength(200);

        builder.HasIndex(w => new { w.CalendarPlanId, w.StartDate }).IsUnique();
    }
}
