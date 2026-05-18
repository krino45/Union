using FluentAssertions;
using UniScheduler.Application.Features.Schedules.Queries;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using UniScheduler.IntegrationTests.Helpers;
using Xunit;

namespace UniScheduler.IntegrationTests.Application;

public class GetSchedulesQueryTests
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2027, 1, 31);

    [Fact]
    public async Task GetSchedules_EmptyDb_ReturnsEmpty()
    {
        using var db = DbContextFactory.Create();
        var result = await new GetSchedulesQueryHandler(db)
            .Handle(new GetSchedulesQuery(), CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSchedules_NoFilter_ReturnsAll()
    {
        using var db = DbContextFactory.Create();
        db.Schedules.AddRange(
            MakeSchedule(Term.First, ScheduleStatus.Draft),
            MakeSchedule(Term.Second, ScheduleStatus.Published));
        await db.SaveChangesAsync();

        var result = await new GetSchedulesQueryHandler(db)
            .Handle(new GetSchedulesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSchedules_FilterByDraft_ReturnsOnlyDraft()
    {
        using var db = DbContextFactory.Create();
        db.Schedules.AddRange(
            MakeSchedule(Term.First, ScheduleStatus.Draft),
            MakeSchedule(Term.Second, ScheduleStatus.Published));
        await db.SaveChangesAsync();

        var result = await new GetSchedulesQueryHandler(db)
            .Handle(new GetSchedulesQuery(ScheduleStatus.Draft), CancellationToken.None);

        result.Should().ContainSingle().Which.Status.Should().Be(ScheduleStatus.Draft);
    }

    [Fact]
    public async Task GetSchedules_FilterByArchived_ReturnsOnlyArchived()
    {
        using var db = DbContextFactory.Create();
        db.Schedules.AddRange(
            MakeSchedule(Term.First, ScheduleStatus.Draft),
            MakeSchedule(Term.Second, ScheduleStatus.Archived));
        await db.SaveChangesAsync();

        var result = await new GetSchedulesQueryHandler(db)
            .Handle(new GetSchedulesQuery(ScheduleStatus.Archived), CancellationToken.None);

        result.Should().ContainSingle().Which.Status.Should().Be(ScheduleStatus.Archived);
    }

    [Fact]
    public async Task GetSchedules_OrderedByYearDesc()
    {
        using var db = DbContextFactory.Create();
        db.Schedules.AddRange(
            new Schedule { AcademicYear = 2025, Term = Term.First, StartDate = Start, EndDate = End, Status = ScheduleStatus.Archived },
            new Schedule { AcademicYear = 2026, Term = Term.First, StartDate = Start, EndDate = End, Status = ScheduleStatus.Draft });
        await db.SaveChangesAsync();

        var result = await new GetSchedulesQueryHandler(db)
            .Handle(new GetSchedulesQuery(), CancellationToken.None);

        result[0].AcademicYear.Should().Be(2026);
        result[1].AcademicYear.Should().Be(2025);
    }

    [Fact]
    public async Task GetSchedules_WithFaculty_IncludesFacultyName()
    {
        using var db = DbContextFactory.Create();
        var faculty = new Faculty { Name = "ФИТ", ShortCode = "FIT" };
        db.Faculties.Add(faculty);
        await db.SaveChangesAsync();

        db.Schedules.Add(new Schedule
        {
            AcademicYear = 2026, Term = Term.First, StartDate = Start, EndDate = End,
            Status = ScheduleStatus.Draft, FacultyId = faculty.Id
        });
        await db.SaveChangesAsync();

        var result = await new GetSchedulesQueryHandler(db)
            .Handle(new GetSchedulesQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.FacultyName.Should().Be("ФИТ");
    }

    private static Schedule MakeSchedule(Term term, ScheduleStatus status) => new()
    {
        AcademicYear = 2026, Term = term, StartDate = Start, EndDate = End, Status = status
    };
}
