using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class TeacherAvailabilityConfiguration : IEntityTypeConfiguration<TeacherAvailability>
{
    public void Configure(EntityTypeBuilder<TeacherAvailability> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasOne(a => a.Teacher)
            .WithMany(t => t.Availabilities)
            .HasForeignKey(a => a.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RescheduleRequestConfiguration : IEntityTypeConfiguration<RescheduleRequest>
{
    public void Configure(EntityTypeBuilder<RescheduleRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Reason).IsRequired();

        builder.HasOne(r => r.RequestedByTeacher)
            .WithMany()
            .HasForeignKey(r => r.RequestedByTeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.OriginalEntry)
            .WithMany()
            .HasForeignKey(r => r.OriginalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).IsRequired();

        builder.HasOne(u => u.Teacher)
            .WithMany()
            .HasForeignKey(u => u.TeacherId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
