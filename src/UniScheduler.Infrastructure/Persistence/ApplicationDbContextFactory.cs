using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Infrastructure.Persistence;

/// <summary>
/// Used by EF Core design-time tools (migrations) only.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../../src/UniScheduler.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(config.GetConnectionString("DefaultConnection"));

        return new ApplicationDbContext(optionsBuilder.Options, new NoopUniversityService());
    }

    private sealed class NoopUniversityService : ICurrentUniversityService
    {
        public Guid? UniversityId => null;
        public bool HasContext => false;
    }
}
