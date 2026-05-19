using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class PairTimeSlotConfiguration : IEntityTypeConfiguration<PairTimeSlot>
{
    public void Configure(EntityTypeBuilder<PairTimeSlot> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PairNumber).IsRequired();
        builder.Property(p => p.StartTime).IsRequired();
        builder.Property(p => p.EndTime).IsRequired();

        builder.HasIndex(p => new { p.UniversityId, p.PairNumber }).IsUnique();

        builder.HasOne(p => p.University)
            .WithMany()
            .HasForeignKey(p => p.UniversityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
