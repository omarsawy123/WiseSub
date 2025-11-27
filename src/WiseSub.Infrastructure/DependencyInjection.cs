using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WiseSub.Application.Common.Configuration;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Infrastructure.AI;
using WiseSub.Infrastructure.Authentication;
using WiseSub.Infrastructure.Data;
using WiseSub.Infrastructure.Email;
using WiseSub.Infrastructure.Repositories;
using WiseSub.Infrastructure.Security;

namespace WiseSub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure email scan settings
        services.Configure<EmailScanConfiguration>(
            configuration.GetSection(EmailScanConfiguration.SectionName));

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
        services.AddScoped<IEmailMetadataRepository, EmailMetadataRepository>();

        // Register authentication services
        services.AddHttpClient();
        services.AddScoped<IAuthenticationService, GoogleAuthenticationService>();

        // Register email services
        services.AddScoped<IGmailClient, GmailClient>();
        services.AddScoped<IEmailProviderClient, GmailClient>(); // Register as provider client too
        services.AddSingleton<IEmailProviderFactory, EmailProviderFactory>();
        services.AddScoped<IEmailIngestionService, EmailIngestionService>();
        // Note: IEmailQueueService removed - using Hangfire for job processing instead

        // Register AI services
        services.AddSingleton<IOpenAIClient, OpenAIClient>();
        services.AddScoped<IAIExtractionService, AIExtractionService>();

        return services;
    }
}
