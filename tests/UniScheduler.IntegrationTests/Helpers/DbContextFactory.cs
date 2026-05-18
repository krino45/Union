using Microsoft.EntityFrameworkCore;
using UniScheduler.Infrastructure.Persistence;

namespace UniScheduler.IntegrationTests.Helpers;

public static class DbContextFactory
{
    /// <summary>Creates an isolated in-memory ApplicationDbContext. Each call is backed by a fresh database.</summary>
    public static ApplicationDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
