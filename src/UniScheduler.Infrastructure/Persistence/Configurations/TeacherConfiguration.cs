using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.FirstName).IsRequired();
        builder.Property(t => t.LastName).IsRequired();
        builder.Property(t => t.MiddleName).IsRequired();
        builder.Property(t => t.Email).IsRequired();
        builder.Ignore(t => t.DisplayName);
        builder.Ignore(t => t.ShortName);

        // Email is unique per university (not globally)
        builder.HasIndex(t => new { t.UniversityId, t.Email }).IsUnique();

        builder.HasOne(t => t.University)
            .WithMany(u => u.Teachers)
            .HasForeignKey(t => t.UniversityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TeacherSubjectConfiguration : IEntityTypeConfiguration<TeacherSubject>
{
    public void Configure(EntityTypeBuilder<TeacherSubject> builder)
    {
        builder.HasKey(ts => new { ts.TeacherId, ts.SubjectId, ts.LessonType });

        builder.HasOne(ts => ts.Teacher)
            .WithMany(t => t.TeacherSubjects)
            .HasForeignKey(ts => ts.TeacherId);

        builder.HasOne(ts => ts.Subject)
            .WithMany(s => s.TeacherSubjects)
            .HasForeignKey(ts => ts.SubjectId);
    }
}
