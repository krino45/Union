using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class StudyPlanEntryConfiguration : IEntityTypeConfiguration<StudyPlanEntry>
{
    public void Configure(EntityTypeBuilder<StudyPlanEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.StudyPlanId, e.SubjectId }).IsUnique();

        builder.HasOne(e => e.Subject)
               .WithMany()
               .HasForeignKey(e => e.SubjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.LectureHours).HasDefaultValue(0.0);
        builder.Property(e => e.PracticalHours).HasDefaultValue(0.0);
        builder.Property(e => e.LabHours).HasDefaultValue(0.0);
        builder.Property(e => e.SeminarHours).HasDefaultValue(0.0);
    }
}
