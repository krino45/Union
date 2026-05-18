using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class FloorPlanNodeConfiguration : IEntityTypeConfiguration<FloorPlanNode>
{
    public void Configure(EntityTypeBuilder<FloorPlanNode> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Floor).IsRequired();
        builder.Property(n => n.X).IsRequired();
        builder.Property(n => n.Y).IsRequired();
        builder.Property(n => n.NodeType).IsRequired();
        builder.Property(n => n.Label).HasMaxLength(100);

        builder.HasOne(n => n.Building)
            .WithMany(b => b.FloorPlanNodes)
            .HasForeignKey(n => n.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Room)
            .WithMany()
            .HasForeignKey(n => n.RoomId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class FloorPlanEdgeConfiguration : IEntityTypeConfiguration<FloorPlanEdge>
{
    public void Configure(EntityTypeBuilder<FloorPlanEdge> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DistanceMeters).IsRequired();

        builder.HasOne(e => e.Building)
            .WithMany(b => b.FloorPlanEdges)
            .HasForeignKey(e => e.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.FromNode)
            .WithMany(n => n.EdgesFrom)
            .HasForeignKey(e => e.FromNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ToNode)
            .WithMany(n => n.EdgesTo)
            .HasForeignKey(e => e.ToNodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
