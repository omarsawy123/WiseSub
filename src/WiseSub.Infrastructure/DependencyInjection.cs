using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SendGrid;
using WiseSub.Application.Common.Configuration;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Infrastructure.AI;
using WiseSub.Infrastructure.Authentication;
using WiseSub.Infrastructure.Data;
using WiseSub.Infrastructure.Email;
using WiseSub.Infrastructure.Payments;
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

        // Configure Entity Framework Core with SQL Server
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        
        services.AddDbContext<WiseSubDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
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

        // Register email notification services (SendGrid)
        services.Configure<EmailNotificationConfiguration>(
            configuration.GetSection(EmailNotificationConfiguration.SectionName));
        
        var sendGridApiKey = configuration.GetSection("SendGrid:ApiKey").Value ?? "";
        services.AddSingleton<ISendGridClient>(_ => new SendGridClient(sendGridApiKey));
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();

        // Register AI services
        services.AddSingleton<IOpenAIClient, OpenAIClient>();
        services.AddScoped<IAIExtractionService, AIExtractionService>();

        // Register Stripe payment services
        services.Configure<StripeConfiguration>(
            configuration.GetSection("Stripe"));
        services.AddScoped<IStripeService, StripeService>();

        return services;
    }
}
