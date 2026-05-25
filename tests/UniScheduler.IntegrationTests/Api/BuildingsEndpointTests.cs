using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class BuildingsEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private readonly TestWebApplicationFactory _factory;
    public BuildingsEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/buildings")]
    [InlineData("POST", "/api/buildings")]
    [InlineData("PUT", "/api/buildings/" + Id)]
    [InlineData("DELETE", "/api/buildings/" + Id)]
    [InlineData("GET", "/api/buildings/distances")]
    [InlineData("PUT", "/api/buildings/distances")]
    [InlineData("GET", "/api/buildings/" + Id + "/floorplan")]
    [InlineData("PUT", "/api/buildings/" + Id + "/floorplan")]
    [InlineData("GET", "/api/buildings/" + Id + "/floorplans")]
    [InlineData("POST", "/api/buildings/" + Id + "/floorplans/from-draft")]
    [InlineData("PATCH", "/api/buildings/" + Id + "/floorplans/" + Id + "/activate")]
    [InlineData("DELETE", "/api/buildings/" + Id + "/floorplans/" + Id)]
    [InlineData("GET", "/api/buildings/" + Id + "/floorplan/drafts")]
    [InlineData("POST", "/api/buildings/" + Id + "/floorplan/drafts")]
    [InlineData("GET", "/api/buildings/" + Id + "/floorplan/drafts/" + Id)]
    [InlineData("PUT", "/api/buildings/" + Id + "/floorplan/drafts/" + Id)]
    [InlineData("PATCH", "/api/buildings/" + Id + "/floorplan/drafts/" + Id + "/access")]
    [InlineData("PATCH", "/api/buildings/" + Id + "/floorplan/drafts/" + Id + "/name")]
    [InlineData("DELETE", "/api/buildings/" + Id + "/floorplan/drafts/" + Id)]
    public async Task Endpoint_Unauthenticated_Returns401(string method, string url)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/buildings")]
    [InlineData("/api/buildings/distances")]
    public async Task Get_Authenticated_Returns200(string url)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
