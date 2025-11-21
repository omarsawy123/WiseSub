using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;

namespace WiseSub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _healthService.CheckHealthAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("db")]
    public async Task<IActionResult> CheckDatabase()
    {
        var result = await _healthService.CheckDatabaseHealthAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error });

        return Ok(result.Value);
    }
}
