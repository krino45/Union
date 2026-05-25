using FluentAssertions;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Features.Schedules.Commands;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using UniScheduler.IntegrationTests.Helpers;
using Xunit;

namespace UniScheduler.IntegrationTests.Application;

public class ScheduleCommandTests
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2027, 1, 31);

    [Fact]
    public async Task CreateSchedule_PersistsWithDraftStatus()
    {
        using var db = DbContextFactory.Create();
        var handler = new CreateScheduleCommandHandler(db, new FakeCurrentUser());

        var result = await handler.Handle(
            new CreateScheduleCommand(2026, Term.First, Start, End, null, false),
            CancellationToken.None);

        result.Status.Should().Be(ScheduleStatus.Draft);
        result.AcademicYear.Should().Be(2026);
        result.Term.Should().Be(Term.First);
        db.Schedules.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateSchedule_WithFaculty_SetsFacultyName()
    {
        using var db = DbContextFactory.Create();
        var faculty = new Faculty { Name = "ФИТ", ShortCode = "FIT" };
        db.Faculties.Add(faculty);
        await db.SaveChangesAsync();

        var handler = new CreateScheduleCommandHandler(db, new FakeCurrentUser());
        var result = await handler.Handle(
            new CreateScheduleCommand(2026, Term.First, Start, End, faculty.Id, false),
            CancellationToken.None);

        result.FacultyName.Should().Be("ФИТ");
        result.FacultyId.Should().Be(faculty.Id);
    }

    [Fact]
    public async Task PublishSchedule_SetsPublishedStatus()
    {
        using var db = DbContextFactory.Create();
        var schedule = MakeDraftSchedule();
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        await new PublishScheduleCommandHandler(db, new FakeCurrentUser())
            .Handle(new PublishScheduleCommand(schedule.Id), CancellationToken.None);

        (await db.Schedules.FindAsync(schedule.Id))!.Status.Should().Be(ScheduleStatus.Published);
    }

    [Fact]
    public async Task PublishSchedule_MissingSchedule_ThrowsNotFoundException()
    {
        using var db = DbContextFactory.Create();
        var act = async () => await new PublishScheduleCommandHandler(db, new FakeCurrentUser())
            .Handle(new PublishScheduleCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ArchiveSchedule_SetsArchivedStatus()
    {
        using var db = DbContextFactory.Create();
        var schedule = MakeDraftSchedule(status: ScheduleStatus.Published);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        await new ArchiveScheduleCommandHandler(db)
            .Handle(new ArchiveScheduleCommand(schedule.Id), CancellationToken.None);

        (await db.Schedules.FindAsync(schedule.Id))!.Status.Should().Be(ScheduleStatus.Archived);
    }

    [Fact]
    public async Task UnarchiveSchedule_RevertsToDraft()
    {
        using var db = DbContextFactory.Create();
        var schedule = MakeDraftSchedule(status: ScheduleStatus.Archived);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        await new UnarchiveScheduleCommandHandler(db)
            .Handle(new UnarchiveScheduleCommand(schedule.Id), CancellationToken.None);

        (await db.Schedules.FindAsync(schedule.Id))!.Status.Should().Be(ScheduleStatus.Draft);
    }

    [Fact]
    public async Task DeleteSchedule_RemovesRecord()
    {
        using var db = DbContextFactory.Create();
        var schedule = MakeDraftSchedule();
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        await new DeleteScheduleCommandHandler(db, new FakeCurrentUser())
            .Handle(new DeleteScheduleCommand(schedule.Id), CancellationToken.None);

        db.Schedules.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSchedule_WhenPublished_ThrowsInvalidOperation()
    {
        using var db = DbContextFactory.Create();
        var schedule = MakeDraftSchedule(status: ScheduleStatus.Published);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var act = async () => await new DeleteScheduleCommandHandler(db, new FakeCurrentUser())
            .Handle(new DeleteScheduleCommand(schedule.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*published*");
    }

    [Fact]
    public async Task DeleteSchedule_MissingSchedule_ThrowsNotFoundException()
    {
        using var db = DbContextFactory.Create();
        var act = async () => await new DeleteScheduleCommandHandler(db, new FakeCurrentUser())
            .Handle(new DeleteScheduleCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static Schedule MakeDraftSchedule(ScheduleStatus status = ScheduleStatus.Draft) => new()
    {
        AcademicYear = 2026, Term = Term.First, StartDate = Start, EndDate = End, Status = status
    };
}
