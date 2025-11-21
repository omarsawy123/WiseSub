using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WiseSub.Infrastructure.Data;
using WiseSub.Infrastructure.Repositories;
using WiseSub.Infrastructure.Security;

namespace WiseSub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Entity Framework Core with SQLite
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=subscriptiontracker.db";
        
        services.AddDbContext<WiseSubDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            // Enable sensitive data logging in development
            if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
            }
        });

        // Register DbContext as DbContext for HealthService
        services.AddScoped<DbContext>(provider =>
            provider.GetRequiredService<WiseSubDbContext>());

        // Register encryption service for OAuth tokens
        services.AddSingleton<ITokenEncryptionService, TokenEncryptionService>();

        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailAccountRepository, EmailAccountRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IVendorMetadataRepository, VendorMetadataRepository>();

        return services;
    }
}
