using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class TeacherAvailabilityEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private readonly TestWebApplicationFactory _factory;
    public TeacherAvailabilityEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/teacher-availability")]
    [InlineData("POST", "/api/teacher-availability")]
    [InlineData("PUT", "/api/teacher-availability/" + Id)]
    [InlineData("DELETE", "/api/teacher-availability/" + Id)]
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
        var response = await client.GetAsync("/api/teacher-availability");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
