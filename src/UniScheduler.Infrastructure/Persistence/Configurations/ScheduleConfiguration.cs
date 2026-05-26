using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.AcademicYear).IsRequired();
        builder.Property(s => s.Term).IsRequired();
        builder.Property(s => s.StartDate).IsRequired();
        builder.Property(s => s.EndDate).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.IsOpenToAdmins).HasDefaultValue(false);

        builder.HasOne(s => s.Faculty)
            .WithMany()
            .HasForeignKey(s => s.FacultyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.University)
            .WithMany()
            .HasForeignKey(s => s.UniversityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Owner)
            .WithMany()
            .HasForeignKey(s => s.OwnerUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ScheduleEntryConfiguration : IEntityTypeConfiguration<ScheduleEntry>
{
    public void Configure(EntityTypeBuilder<ScheduleEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DayOfWeek).IsRequired();
        builder.Property(e => e.PairNumber).IsRequired();
        builder.Property(e => e.WeekType).IsRequired();
        builder.Property(e => e.LessonType).IsRequired();

        builder.HasIndex(e => new { e.ScheduleId, e.RoomId, e.DayOfWeek, e.PairNumber, e.WeekType })
            .HasFilter("\"RoomId\" IS NOT NULL AND \"ParallelGroupId\" IS NULL")
            .IsUnique();

        builder.HasIndex(e => new { e.ScheduleId, e.TeacherId, e.DayOfWeek, e.PairNumber, e.WeekType })
            .HasFilter("\"ParallelGroupId\" IS NULL")
            .IsUnique();

        builder.HasOne(e => e.Schedule)
            .WithMany(s => s.Entries)
            .HasForeignKey(e => e.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Teacher)
            .WithMany(t => t.ScheduleEntries)
            .HasForeignKey(e => e.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Subject)
            .WithMany(s => s.ScheduleEntries)
            .HasForeignKey(e => e.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Room)
            .WithMany(r => r.ScheduleEntries)
            .HasForeignKey(e => e.RoomId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}

public class ScheduleEntryStudentGroupConfiguration : IEntityTypeConfiguration<ScheduleEntryStudentGroup>
{
    public void Configure(EntityTypeBuilder<ScheduleEntryStudentGroup> builder)
    {
        builder.HasKey(sg => new { sg.ScheduleEntryId, sg.StudentGroupId });

        builder.HasOne(sg => sg.ScheduleEntry)
            .WithMany(e => e.StudentGroups)
            .HasForeignKey(sg => sg.ScheduleEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sg => sg.StudentGroup)
            .WithMany(g => g.ScheduleEntryGroups)
            .HasForeignKey(sg => sg.StudentGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
