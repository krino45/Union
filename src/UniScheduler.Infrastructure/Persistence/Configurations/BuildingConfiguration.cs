using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.ShortCode).IsRequired().HasMaxLength(20);
        builder.Property(b => b.Address).IsRequired();
        builder.Property(b => b.StairsDistancePerFloor).IsRequired().HasDefaultValue(20);
    }
}

public class BuildingDistanceConfiguration : IEntityTypeConfiguration<BuildingDistance>
{
    public void Configure(EntityTypeBuilder<BuildingDistance> builder)
    {
        builder.HasKey(d => new { d.FromBuildingId, d.ToBuildingId });
        builder.Ignore(d => d.WalkingMinutes);
        builder.Ignore(d => d.ExceedsPairBreak);

        builder.HasOne(d => d.FromBuilding)
            .WithMany(b => b.DistancesFrom)
            .HasForeignKey(d => d.FromBuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ToBuilding)
            .WithMany(b => b.DistancesTo)
            .HasForeignKey(d => d.ToBuildingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
