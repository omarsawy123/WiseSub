using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for health check endpoints to monitor system status
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    /// <summary>
    /// Initializes a new instance of the HealthController
    /// </summary>
    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    /// <summary>
    /// Gets the overall health status of the API
    /// </summary>
    /// <returns>Health status information</returns>
    /// <response code="200">System is healthy</response>
    /// <response code="500">System is unhealthy</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get()
    {
        var result = await _healthService.CheckHealthAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>
    /// Checks the database connectivity and health
    /// </summary>
    /// <returns>Database health status</returns>
    /// <response code="200">Database is healthy</response>
    /// <response code="500">Database is unhealthy or unreachable</response>
    [HttpGet("db")]
    [ProducesResponseType(typeof(DatabaseHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckDatabase()
    {
        var result = await _healthService.CheckDatabaseHealthAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.ErrorMessage });

        return Ok(result.Value);
    }
}

/// <summary>
/// Response model for overall health check
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Overall health status (Healthy, Degraded, Unhealthy)
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp of the health check
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Individual component health statuses
    /// </summary>
    public Dictionary<string, string> Components { get; set; } = new();
}

/// <summary>
/// Response model for database health check
/// </summary>
public class DatabaseHealthResponse
{
    /// <summary>
    /// Database connection status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Database response time in milliseconds
    /// </summary>
    public long ResponseTimeMs { get; set; }
}
