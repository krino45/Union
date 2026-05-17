using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class StudyPlanConfiguration : IEntityTypeConfiguration<StudyPlan>
{
    public void Configure(EntityTypeBuilder<StudyPlan> builder)
    {
        builder.HasKey(sp => sp.Id);
        builder.Property(sp => sp.Name).IsRequired().HasMaxLength(200);
        builder.Property(sp => sp.AcademicYear).IsRequired();
        builder.Property(sp => sp.Term).IsRequired();

        builder.HasMany(sp => sp.Entries)
               .WithOne(e => e.StudyPlan)
               .HasForeignKey(e => e.StudyPlanId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(sp => sp.Groups)
               .WithOne(g => g.StudyPlan)
               .HasForeignKey(g => g.StudyPlanId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sp => sp.CalendarPlan)
               .WithMany(cp => cp.StudyPlans)
               .HasForeignKey(sp => sp.CalendarPlanId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public class StudyPlanGroupConfiguration : IEntityTypeConfiguration<StudyPlanGroup>
{
    public void Configure(EntityTypeBuilder<StudyPlanGroup> builder)
    {
        builder.HasKey(g => new { g.StudyPlanId, g.StudentGroupId });

        builder.HasOne(g => g.StudentGroup)
               .WithMany()
               .HasForeignKey(g => g.StudentGroupId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
