using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Number).IsRequired().HasMaxLength(20);
        builder.Property(r => r.RoomType).IsRequired();
        builder.Property(r => r.Capacity).IsRequired();
        builder.Property(r => r.Floor).IsRequired().HasDefaultValue(1);
        builder.Property(r => r.DistanceFromStairsMeters).IsRequired().HasDefaultValue(0);

        builder.HasOne(r => r.Building)
            .WithMany(b => b.Rooms)
            .HasForeignKey(r => r.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
