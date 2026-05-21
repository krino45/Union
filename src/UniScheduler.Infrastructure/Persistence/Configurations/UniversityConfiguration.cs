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
        builder.Property(u => u.City).HasMaxLength(200);
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
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.DraftJson).IsRequired();
        builder.Property(d => d.LastModified).IsRequired();
        builder.Property(d => d.IsOpenToAdmins).HasDefaultValue(false);

        builder.HasIndex(d => new { d.BuildingId, d.OwnerUserId });

        builder.HasOne(d => d.Building)
            .WithMany()
            .HasForeignKey(d => d.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Owner)
            .WithMany()
            .HasForeignKey(d => d.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FloorPlanConfiguration : IEntityTypeConfiguration<FloorPlan>
{
    public void Configure(EntityTypeBuilder<FloorPlan> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.FloorPlanJson).IsRequired();
        builder.Property(p => p.IsActive).HasDefaultValue(false);
        builder.Property(p => p.CreatedAt).IsRequired();

        // One active floor plan per building (PostgreSQL partial unique index)
        builder.HasIndex(p => p.BuildingId)
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");

        builder.HasOne(p => p.Building)
            .WithMany(b => b.FloorPlans)
            .HasForeignKey(p => p.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Email).IsRequired().HasMaxLength(320);
        builder.Property(i => i.OtpHash).IsRequired().HasMaxLength(128);
        builder.Property(i => i.SystemRole).IsRequired().HasMaxLength(30);
        builder.Property(i => i.UniversityRole).IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();

        builder.HasIndex(i => i.OtpHash).IsUnique();
        builder.HasIndex(i => i.Email);
        builder.HasIndex(i => i.UniversityId);

        builder.HasOne(i => i.University)
            .WithMany()
            .HasForeignKey(i => i.UniversityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.InvitedBy)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ConsumedBy)
            .WithMany()
            .HasForeignKey(i => i.ConsumedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.Teacher)
            .WithMany()
            .HasForeignKey(i => i.TeacherId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
