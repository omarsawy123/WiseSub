using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WiseSub.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating DbContext instances during migrations
/// </summary>
public class WiseSubDbContextFactory : IDesignTimeDbContextFactory<WiseSubDbContext>
{
    public WiseSubDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Create DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<WiseSubDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=subscriptiontracker.db";
        
        optionsBuilder.UseSqlite(connectionString);

        return new WiseSubDbContext(optionsBuilder.Options);
    }
}
