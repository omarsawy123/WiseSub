using Microsoft.Extensions.DependencyInjection;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;

namespace WiseSub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register application services
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
