using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
                ResolveConnectionString(configuration),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<ISchedulerService, OrToolsSchedulerService>();
        services.AddSingleton<IConflictDetector, ConflictDetector>();
        services.AddSingleton<IJwtService>(sp =>
            new JwtService(configuration.GetSection("JwtSettings")));
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUniversityService, CurrentUniversityService>();
        services.AddSingleton<IAppUrls, AppUrls>();

        AddEmailSender(services, configuration);

        return services;
    }

    private static void AddEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>() ?? new EmailSettings();
        services.AddSingleton(settings);

        if (string.Equals(settings.Provider, "Resend", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<IEmailSender, ResendEmailSender>();
        else if (string.Equals(settings.Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, ConsoleEmailSender>();
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var includeErrorDetail = string.Equals(
            Environment.GetEnvironmentVariable("DB_ERROR_DETAIL"), "true", StringComparison.OrdinalIgnoreCase);

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl) &&
            (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
             databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);
            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
                Database = uri.AbsolutePath.TrimStart('/'),
                SslMode = SslMode.Prefer,
                IncludeErrorDetail = includeErrorDetail
            }.ConnectionString;
        }

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No database connection string. Set DATABASE_URL or ConnectionStrings__DefaultConnection.");
    }
}
