using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class CalendarPlansEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private readonly TestWebApplicationFactory _factory;
    public CalendarPlansEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/calendar-plans")]
    [InlineData("GET", "/api/calendar-plans/" + Id)]
    [InlineData("POST", "/api/calendar-plans")]
    [InlineData("PUT", "/api/calendar-plans/" + Id)]
    [InlineData("DELETE", "/api/calendar-plans/" + Id)]
    public async Task Endpoint_Unauthenticated_Returns401(string method, string url)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/calendar-plans");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
