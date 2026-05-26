using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

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
        builder.Property(r => r.AllowedLessonTypes)
            .HasConversion(
                v => string.Join(',', v.Select(lt => lt.ToString())),
                v => string.IsNullOrEmpty(v)
                    ? new List<LessonType>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => Enum.Parse<LessonType>(s)).ToList())
            .HasColumnType("text")
            .HasDefaultValueSql("''");

        builder.Property(r => r.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasOne(r => r.Building)
            .WithMany(b => b.Rooms)
            .HasForeignKey(r => r.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Department)
            .WithMany(d => d.Rooms)
            .HasForeignKey(r => r.DepartmentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
