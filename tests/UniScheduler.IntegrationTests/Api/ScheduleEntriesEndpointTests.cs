using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class ScheduleEntriesEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private readonly TestWebApplicationFactory _factory;
    public ScheduleEntriesEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("POST", "/api/schedule-entries")]
    [InlineData("POST", "/api/schedule-entries/" + Id + "/move")]
    [InlineData("POST", "/api/schedule-entries/" + Id + "/update")]
    [InlineData("POST", "/api/schedule-entries/" + Id + "/split-edit")]
    [InlineData("DELETE", "/api/schedule-entries/" + Id)]
    [InlineData("GET", "/api/schedule-entries/conflicts")]
    public async Task Endpoint_Unauthenticated_Returns401(string method, string url)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
