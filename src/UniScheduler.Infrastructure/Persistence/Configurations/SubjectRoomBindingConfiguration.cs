using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class SubjectRoomBindingConfiguration : IEntityTypeConfiguration<SubjectRoomBinding>
{
    public void Configure(EntityTypeBuilder<SubjectRoomBinding> builder)
    {
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => new { b.SubjectId, b.LessonType, b.RoomId }).IsUnique();

        builder.HasOne(b => b.Subject)
            .WithMany()
            .HasForeignKey(b => b.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Room)
            .WithMany()
            .HasForeignKey(b => b.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
