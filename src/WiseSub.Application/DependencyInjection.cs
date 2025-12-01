using Microsoft.Extensions.DependencyInjection;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;

namespace WiseSub.Application;

/// <summary>
/// Dependency injection configuration for application layer
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register application services
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailMetadataService, EmailMetadataService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAlertService, AlertService>();

        return services;
    }
}
