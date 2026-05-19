using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.ShortCode).IsRequired().HasMaxLength(20);

        // ShortCode is unique per university (not globally)
        builder.HasIndex(f => new { f.UniversityId, f.ShortCode }).IsUnique();

        builder.HasOne(f => f.University)
            .WithMany(u => u.Faculties)
            .HasForeignKey(f => f.UniversityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
