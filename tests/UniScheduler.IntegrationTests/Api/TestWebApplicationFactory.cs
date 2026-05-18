using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Infrastructure.Persistence;

namespace UniScheduler.IntegrationTests.Api;

/// <summary>
/// Factory that replaces PostgreSQL with an in-memory database so API integration
/// tests run without a real database server.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the registered Npgsql DbContext options
            var dbOpts = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbOpts != null) services.Remove(dbOpts);

            // Remove the ApplicationDbContext registration itself (if directly registered)
            var dbCtx = services.SingleOrDefault(d =>
                d.ServiceType == typeof(ApplicationDbContext));
            if (dbCtx != null) services.Remove(dbCtx);

            // Replace with a fresh in-memory database per factory instance
            var dbName = "TestApi_" + Guid.NewGuid().ToString("N");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // Re-register the scoped IApplicationDbContext alias
            services.AddScoped<IApplicationDbContext>(sp =>
                sp.GetRequiredService<ApplicationDbContext>());
        });
    }

    /// <summary>Gets an HTTP client that carries a valid Admin JWT token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        // Ensure the seeded admin user exists (DbSeeder ran during startup)
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123" });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private sealed record LoginResponse(string Token, string Role, Guid UserId, Guid? TeacherId);
}
