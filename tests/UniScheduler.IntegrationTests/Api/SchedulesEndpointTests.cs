using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UniScheduler.Domain.Enums;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class SchedulesEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SchedulesEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetSchedules_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/schedules");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSchedules_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/schedules");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSchedules_Authenticated_ReturnsJsonArray()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var result = await client.GetFromJsonAsync<List<object>>("/api/schedules");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSchedule_Authenticated_Returns201()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/schedules", new
        {
            AcademicYear = 2026,
            Term = "First",
            StartDate = "2026-09-01",
            EndDate = "2027-01-31",
            FacultyId = (Guid?)null,
            AllowCrossFacultyLessons = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetScheduleById_MissingId_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"/api/schedules/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSchedule_ExistingDraft_Returns204()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Create a schedule
        var createResp = await client.PostAsJsonAsync("/api/schedules", new
        {
            AcademicYear = 2026,
            Term = "First",
            StartDate = "2026-09-01",
            EndDate = "2027-01-31",
            FacultyId = (Guid?)null,
            AllowCrossFacultyLessons = false
        });
        var created = await createResp.Content.ReadFromJsonAsync<ScheduleBody>();

        var deleteResp = await client.DeleteAsync($"/api/schedules/{created!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PublishSchedule_ExistingDraft_Returns204()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/schedules", new
        {
            AcademicYear = 2026,
            Term = "Second",
            StartDate = "2027-02-01",
            EndDate = "2027-06-30",
            FacultyId = (Guid?)null,
            AllowCrossFacultyLessons = false
        });
        var created = await createResp.Content.ReadFromJsonAsync<ScheduleBody>();

        var publishResp = await client.PostAsync($"/api/schedules/{created!.Id}/publish", null);
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteSchedule_MissingId_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"/api/schedules/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExceptionHandling_Returns404WithProblemJson_ForMissing()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"/api/schedules/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Not Found");
        body.Should().Contain("404");
    }

    private sealed record ScheduleBody(Guid Id, int AcademicYear, string Term,
        string StartDate, string EndDate, string Status);
}
