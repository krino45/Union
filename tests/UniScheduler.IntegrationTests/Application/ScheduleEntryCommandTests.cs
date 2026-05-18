using FluentAssertions;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using UniScheduler.Infrastructure.Scheduler;
using UniScheduler.IntegrationTests.Helpers;
using Xunit;

namespace UniScheduler.IntegrationTests.Application;

public class ScheduleEntryCommandTests
{
    // Seed the minimum set of entities required by most entry-level tests.
    private static async Task<(Schedule schedule, Teacher teacher, Subject subject,
        StudentGroup group, Room room)> SeedAsync(
        UniScheduler.Infrastructure.Persistence.ApplicationDbContext db,
        ScheduleStatus status = ScheduleStatus.Draft)
    {
        var faculty = new Faculty { Name = "ФИТ", ShortCode = "FIT" };
        var building = new Building { ShortCode = "A", Address = "Main St 1" };
        db.Faculties.Add(faculty);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var room = new Room { BuildingId = building.Id, Number = "101", RoomType = RoomType.LectureHall, Capacity = 40 };
        var teacher = new Teacher { FirstName = "Иван", LastName = "Иванов", MiddleName = "И.", Email = "i@uni.ru" };
        var subject = new Subject { Name = "Математика", ShortName = "МА", AcademicYear = 2026, Term = Term.First };
        var group = new StudentGroup { Name = "ФИТ-101", Year = 1, Specialty = "CS", StudentCount = 25, FacultyId = faculty.Id };
        var schedule = new Schedule
        {
            AcademicYear = 2026, Term = Term.First,
            StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 31),
            Status = status
        };
        db.Rooms.Add(room);
        db.Teachers.Add(teacher);
        db.Subjects.Add(subject);
        db.StudentGroups.Add(group);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();
        return (schedule, teacher, subject, group, room);
    }

    [Fact]
    public async Task CreateEntry_PersistsEntryWithGroups()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db);
        var handler = new CreateEntryCommandHandler(db, new ConflictDetector());

        var result = await handler.Handle(new CreateEntryCommand(
            schedule.Id, subject.Id, teacher.Id, room.Id, [group.Id],
            RussianDayOfWeek.Monday, 1, WeekType.Both, LessonType.Lecture, false),
            CancellationToken.None);

        result.ScheduleId.Should().Be(schedule.Id);
        result.StudentGroups.Should().ContainSingle(g => g.Id == group.Id);
        db.ScheduleEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateEntry_ConflictingRoom_ThrowsConflictException()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db);

        var teacher2 = new Teacher { FirstName = "Петр", LastName = "Петров", MiddleName = "", Email = "p@uni.ru" };
        db.Teachers.Add(teacher2);
        await db.SaveChangesAsync();

        var handler = new CreateEntryCommandHandler(db, new ConflictDetector());
        await handler.Handle(new CreateEntryCommand(
            schedule.Id, subject.Id, teacher.Id, room.Id, [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, LessonType.Lecture, false),
            CancellationToken.None);

        var act = async () => await handler.Handle(new CreateEntryCommand(
            schedule.Id, subject.Id, teacher2.Id, room.Id, [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, LessonType.Lecture, false),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateEntry_OnArchivedSchedule_ThrowsInvalidOperation()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db, ScheduleStatus.Archived);
        var handler = new CreateEntryCommandHandler(db, new ConflictDetector());

        var act = async () => await handler.Handle(new CreateEntryCommand(
            schedule.Id, subject.Id, teacher.Id, room.Id, [group.Id],
            RussianDayOfWeek.Monday, 1, WeekType.Both, LessonType.Lecture, false),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*archived*");
    }

    [Fact]
    public async Task CreateEntry_OnPublishedSchedule_RevertsToDraft()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db, ScheduleStatus.Published);
        var handler = new CreateEntryCommandHandler(db, new ConflictDetector());

        await handler.Handle(new CreateEntryCommand(
            schedule.Id, subject.Id, teacher.Id, room.Id, [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, LessonType.Lecture, false),
            CancellationToken.None);

        (await db.Schedules.FindAsync(schedule.Id))!.Status.Should().Be(ScheduleStatus.Draft);
    }

    [Fact]
    public async Task MoveEntry_UpdatesSlot()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db);
        var entry = AddEntry(db, schedule, teacher, subject, group, room, RussianDayOfWeek.Monday, 1);
        await db.SaveChangesAsync();

        var result = await new MoveEntryCommandHandler(db, new ConflictDetector())
            .Handle(new MoveEntryCommand(entry.Id, RussianDayOfWeek.Wednesday, 3, WeekType.Odd, room.Id),
                CancellationToken.None);

        result.DayOfWeek.Should().Be(RussianDayOfWeek.Wednesday);
        result.PairNumber.Should().Be(3);
        result.WeekType.Should().Be(WeekType.Odd);
    }

    [Fact]
    public async Task MoveEntry_ToConflictingSlot_ThrowsConflictException()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db);
        var entry1 = AddEntry(db, schedule, teacher, subject, group, room, RussianDayOfWeek.Monday, 1);
        var entry2 = AddEntry(db, schedule, teacher, subject, group, room, RussianDayOfWeek.Tuesday, 2);
        await db.SaveChangesAsync();

        var act = async () => await new MoveEntryCommandHandler(db, new ConflictDetector())
            .Handle(new MoveEntryCommand(entry1.Id, RussianDayOfWeek.Tuesday, 2, WeekType.Both, room.Id),
                CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task MoveEntry_OnPublishedSchedule_RevertsToDraft()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db, ScheduleStatus.Published);
        var entry = AddEntry(db, schedule, teacher, subject, group, room, RussianDayOfWeek.Monday, 1);
        await db.SaveChangesAsync();

        await new MoveEntryCommandHandler(db, new ConflictDetector())
            .Handle(new MoveEntryCommand(entry.Id, RussianDayOfWeek.Friday, 4, WeekType.Even, null),
                CancellationToken.None);

        (await db.Schedules.FindAsync(schedule.Id))!.Status.Should().Be(ScheduleStatus.Draft);
    }

    [Fact]
    public async Task MoveEntry_OnArchivedSchedule_ThrowsInvalidOperation()
    {
        using var db = DbContextFactory.Create();
        var (schedule, teacher, subject, group, room) = await SeedAsync(db, ScheduleStatus.Archived);
        var entry = AddEntry(db, schedule, teacher, subject, group, room, RussianDayOfWeek.Monday, 1);
        await db.SaveChangesAsync();

        var act = async () => await new MoveEntryCommandHandler(db, new ConflictDetector())
            .Handle(new MoveEntryCommand(entry.Id, RussianDayOfWeek.Friday, 4, WeekType.Both, null),
                CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*archived*");
    }

    [Fact]
    public async Task MoveEntry_Missing_ThrowsNotFoundException()
    {
        using var db = DbContextFactory.Create();
        var act = async () => await new MoveEntryCommandHandler(db, new ConflictDetector())
            .Handle(new MoveEntryCommand(Guid.NewGuid(), RussianDayOfWeek.Monday, 1, WeekType.Both, null),
                CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static ScheduleEntry AddEntry(
        UniScheduler.Infrastructure.Persistence.ApplicationDbContext db,
        Schedule schedule, Teacher teacher, Subject subject, StudentGroup group, Room room,
        RussianDayOfWeek day, int pair)
    {
        var entry = new ScheduleEntry
        {
            ScheduleId = schedule.Id, SubjectId = subject.Id, TeacherId = teacher.Id,
            RoomId = room.Id, DayOfWeek = day, PairNumber = pair,
            WeekType = WeekType.Both, LessonType = LessonType.Lecture
        };
        db.ScheduleEntries.Add(entry);
        db.ScheduleEntryStudentGroups.Add(new ScheduleEntryStudentGroup
        {
            ScheduleEntry = entry, StudentGroupId = group.Id
        });
        return entry;
    }
}
