using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class AuthEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "WRONG" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "nobody", Password = "any" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Returns401ResponseWithProblemJson()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "bad" });

        response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Unauthorized");
    }

    private sealed record LoginBody(string Token, string Role, Guid UserId, Guid? TeacherId);
}
