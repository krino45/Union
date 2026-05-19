using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class GroupBlockedDayConfiguration : IEntityTypeConfiguration<GroupBlockedDay>
{
    public void Configure(EntityTypeBuilder<GroupBlockedDay> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.DayOfWeek).IsRequired();
        builder.HasOne(g => g.Group)
            .WithMany(sg => sg.BlockedDays)
            .HasForeignKey(g => g.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(g => new { g.GroupId, g.DayOfWeek }).IsUnique();
    }
}
