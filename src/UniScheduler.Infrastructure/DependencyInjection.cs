using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Infrastructure.Auth;
using UniScheduler.Infrastructure.Email;
using UniScheduler.Infrastructure.Excel;
using UniScheduler.Infrastructure.Persistence;
using UniScheduler.Infrastructure.Scheduler;

namespace UniScheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<ISchedulerService, OrToolsSchedulerService>();
        services.AddSingleton<IConflictDetector, ConflictDetector>();
        services.AddSingleton<IJwtService>(sp =>
            new JwtService(configuration.GetSection("JwtSettings")));
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUniversityService, CurrentUniversityService>();
        services.AddScoped<IEmailSender, ConsoleEmailSender>();

        return services;
    }
}
