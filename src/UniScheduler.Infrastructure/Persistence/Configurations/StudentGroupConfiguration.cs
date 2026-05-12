using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(50);
        builder.Property(g => g.Specialty).IsRequired();
        builder.HasOne(g => g.Faculty)
            .WithMany(f => f.Groups)
            .HasForeignKey(g => g.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
