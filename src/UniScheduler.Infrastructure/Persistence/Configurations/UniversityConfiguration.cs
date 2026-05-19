using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(300);
        builder.Property(u => u.ShortName).IsRequired().HasMaxLength(50);
        builder.Property(u => u.LogoUrl).HasMaxLength(500);
        builder.HasIndex(u => u.ShortName).IsUnique();
    }
}

public class UserUniversityAccessConfiguration : IEntityTypeConfiguration<UserUniversityAccess>
{
    public void Configure(EntityTypeBuilder<UserUniversityAccess> builder)
    {
        builder.HasKey(a => new { a.UserId, a.UniversityId });
        builder.Property(a => a.Role).IsRequired();

        builder.HasOne(a => a.User)
            .WithMany(u => u.UniversityAccesses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.University)
            .WithMany(u => u.UserAccesses)
            .HasForeignKey(a => a.UniversityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FloorPlanDraftConfiguration : IEntityTypeConfiguration<FloorPlanDraft>
{
    public void Configure(EntityTypeBuilder<FloorPlanDraft> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DraftJson).IsRequired();
        builder.Property(d => d.LastModified).IsRequired();

        builder.HasIndex(d => d.BuildingId).IsUnique();

        builder.HasOne(d => d.Building)
            .WithMany()
            .HasForeignKey(d => d.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
