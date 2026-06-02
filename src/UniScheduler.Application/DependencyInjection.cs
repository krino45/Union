using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UniScheduler.Application.Common.Behaviours;
using UniScheduler.Application.Features.Schedules.Lns;

namespace UniScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        services.AddScoped<ILnsOptimizerService, LnsOptimizerService>();

        return services;
    }
}
