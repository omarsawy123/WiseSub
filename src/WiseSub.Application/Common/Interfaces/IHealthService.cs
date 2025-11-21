using WiseSub.Domain.Common;

namespace WiseSub.Application.Common.Interfaces;

public interface IHealthService
{
    Task<Result<HealthCheckResponse>> CheckHealthAsync();
    Task<Result<DatabaseHealthResponse>> CheckDatabaseHealthAsync();
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Service { get; set; } = string.Empty;
}

public class DatabaseHealthResponse
{
    public string Status { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public bool CanConnect { get; set; }
    public DateTime Timestamp { get; set; }
}
