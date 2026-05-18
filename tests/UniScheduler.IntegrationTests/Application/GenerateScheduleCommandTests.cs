using FluentAssertions;
using Moq;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Common.Models;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using UniScheduler.IntegrationTests.Helpers;
using Xunit;

namespace UniScheduler.IntegrationTests.Application;

public class GenerateScheduleCommandTests
{
    private static Schedule MakeSchedule() => new()
    {
        AcademicYear = 2026, Term = Term.First,
        StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 31),
        Status = ScheduleStatus.Draft
    };

    [Fact]
    public async Task Handle_MissingSchedule_ThrowsNotFoundException()
    {
        await using var db = DbContextFactory.Create();
        var handler = new GenerateScheduleCommandHandler(db, new Mock<ISchedulerService>().Object);

        var act = async () => await handler.Handle(
            new GenerateScheduleCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InfeasibleSolver_ReturnsFailed()
    {
        await using var db = DbContextFactory.Create();
        var schedule = MakeSchedule();
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var mockSolver = new Mock<ISchedulerService>();
        mockSolver.Setup(s => s.SolveAsync(It.IsAny<SchedulerInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulerOutput(SolverStatus.Infeasible, "No solution", []));

        var result = await new GenerateScheduleCommandHandler(db, mockSolver.Object)
            .Handle(new GenerateScheduleCommand(schedule.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Infeasible");
        result.EntriesCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ClearsExistingEntriesBeforeSolving()
    {
        using var db = DbContextFactory.Create();
        var schedule = MakeSchedule();
        var teacher = new Teacher { FirstName = "А", LastName = "Б", MiddleName = "", Email = "a@uni.ru" };
        var subject = new Subject { Name = "X", ShortName = "X", AcademicYear = 2026, Term = Term.First };
        db.Schedules.Add(schedule);
        db.Teachers.Add(teacher);
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        db.ScheduleEntries.Add(new ScheduleEntry
        {
            ScheduleId = schedule.Id, TeacherId = teacher.Id, SubjectId = subject.Id,
            DayOfWeek = RussianDayOfWeek.Monday, PairNumber = 1,
            WeekType = WeekType.Both, LessonType = LessonType.Lecture
        });
        await db.SaveChangesAsync();
        db.ScheduleEntries.Should().HaveCount(1);

        var mockSolver = new Mock<ISchedulerService>();
        mockSolver.Setup(s => s.SolveAsync(It.IsAny<SchedulerInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulerOutput(SolverStatus.Optimal, null, []));

        await new GenerateScheduleCommandHandler(db, mockSolver.Object)
            .Handle(new GenerateScheduleCommand(schedule.Id), CancellationToken.None);

        db.ScheduleEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OptimalResult_CreatesEntries()
    {
        using var db = DbContextFactory.Create();
        var faculty = new Faculty { Name = "ФИТ", ShortCode = "FIT" };
        var building = new Building { ShortCode = "A", Address = "Main" };
        db.Faculties.Add(faculty);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var room = new Room { BuildingId = building.Id, Number = "101", RoomType = RoomType.LectureHall, Capacity = 40 };
        var teacher = new Teacher { FirstName = "И", LastName = "И", MiddleName = "", Email = "i@uni.ru" };
        var subject = new Subject { Name = "Math", ShortName = "M", AcademicYear = 2026, Term = Term.First };
        var group = new StudentGroup { Name = "ФИТ-101", Year = 1, Specialty = "CS", StudentCount = 25, FacultyId = faculty.Id };
        var schedule = MakeSchedule();
        db.Rooms.Add(room);
        db.Teachers.Add(teacher);
        db.Subjects.Add(subject);
        db.StudentGroups.Add(group);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        // Fallback path requires teacher-subject link
        db.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherId = teacher.Id, SubjectId = subject.Id, LessonType = LessonType.Lecture
        });
        await db.SaveChangesAsync();

        var assignment = new SchedulerAssignment(0, RussianDayOfWeek.Monday, 1, WeekType.Both, room.Id);
        var mockSolver = new Mock<ISchedulerService>();
        mockSolver.Setup(s => s.SolveAsync(It.IsAny<SchedulerInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulerOutput(SolverStatus.Optimal, null, [assignment]));

        var result = await new GenerateScheduleCommandHandler(db, mockSolver.Object)
            .Handle(new GenerateScheduleCommand(schedule.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EntriesCreated.Should().Be(1);
        db.ScheduleEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_OptimalResult_SetsGeneratedAt()
    {
        using var db = DbContextFactory.Create();
        var schedule = MakeSchedule();
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var mockSolver = new Mock<ISchedulerService>();
        mockSolver.Setup(s => s.SolveAsync(It.IsAny<SchedulerInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulerOutput(SolverStatus.Optimal, null, []));

        await new GenerateScheduleCommandHandler(db, mockSolver.Object)
            .Handle(new GenerateScheduleCommand(schedule.Id), CancellationToken.None);

        (await db.Schedules.FindAsync(schedule.Id))!.GeneratedAt.Should().NotBeNull();
    }
}
