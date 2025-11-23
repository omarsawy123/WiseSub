using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;

namespace WiseSub.Application.Services;

public class HealthService : IHealthService
{
    private readonly DbContext _dbContext;

    public HealthService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Result<HealthCheckResponse>> CheckHealthAsync()
    {
        var response = new HealthCheckResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Service = "Subscription Tracker API"
        };

        return Task.FromResult(Result.Success(response));
    }

    public async Task<Result<DatabaseHealthResponse>> CheckDatabaseHealthAsync()
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();
            
            var response = new DatabaseHealthResponse
            {
                Status = canConnect ? "healthy" : "unhealthy",
                Database = "SQLite",
                CanConnect = canConnect,
                Timestamp = DateTime.UtcNow
            };

            if (!canConnect)
                return Result.Failure<DatabaseHealthResponse>(GeneralErrors.DatabaseError);

            return Result.Success(response);
        }
        catch (Exception)
        {
            var response = new DatabaseHealthResponse
            {
                Status = "unhealthy",
                Database = "SQLite",
                CanConnect = false,
                Timestamp = DateTime.UtcNow
            };
            return Result.Failure<DatabaseHealthResponse>(GeneralErrors.DatabaseError);
        }
    }
}
