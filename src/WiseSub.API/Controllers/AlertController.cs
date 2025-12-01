using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for alert management, preferences, and notification settings
/// </summary>
[ApiController]
[Route("api/alerts")]
[Authorize]
[Produces("application/json")]
public class AlertController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertController> _logger;

    public AlertController(
        IAlertService alertService,
        ILogger<AlertController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all alerts for the current user with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertResponse>>> GetAlerts(
        [FromQuery] string? status,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        AlertStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AlertStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        AlertType? typeFilter = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<AlertType>(type, true, out var parsedType))
        {
            typeFilter = parsedType;
        }

        var result = await _alertService.GetUserAlertsAsync(
            userId, statusFilter, typeFilter, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value.Select(MapToResponse));
    }

    /// <summary>
    /// Gets today's alerts for the current user (for daily digest)
    /// </summary>
    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<AlertResponse>>> GetTodaysAlerts(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _alertService.GetTodaysAlertsAsync(userId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value.Select(MapToResponse));
    }

    /// <summary>
    /// Snoozes an alert for the specified number of hours
    /// </summary>
    [HttpPost("{id}/snooze")]
    public async Task<IActionResult> SnoozeAlert(
        string id,
        [FromBody] SnoozeAlertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Validate snooze hours
        var snoozeHours = request.Hours ?? 24;
        if (snoozeHours < 1 || snoozeHours > 168) // Max 1 week
        {
            return BadRequest(new { error = "Snooze hours must be between 1 and 168" });
        }

        var result = await _alertService.SnoozeAlertAsync(id, snoozeHours, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Alert {AlertId} snoozed for {Hours} hours by user {UserId}", 
            id, snoozeHours, userId);

        return Ok(new { message = $"Alert snoozed for {snoozeHours} hours" });
    }

    /// <summary>
    /// Dismisses an alert
    /// </summary>
    [HttpPost("{id}/dismiss")]
    public async Task<IActionResult> DismissAlert(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _alertService.DismissAlertAsync(id, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Alert {AlertId} dismissed by user {UserId}", id, userId);

        return Ok(new { message = "Alert dismissed" });
    }

    /// <summary>
    /// Gets the current user's alert preferences
    /// </summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<AlertPreferencesResponse>> GetPreferences(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _alertService.GetUserPreferencesAsync(userId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        var prefs = result.Value;
        return Ok(new AlertPreferencesResponse
        {
            EnableRenewalAlerts = prefs.EnableRenewalAlerts,
            EnablePriceChangeAlerts = prefs.EnablePriceChangeAlerts,
            EnableTrialEndingAlerts = prefs.EnableTrialEndingAlerts,
            EnableUnusedSubscriptionAlerts = prefs.EnableUnusedSubscriptionAlerts,
            UseDailyDigest = prefs.UseDailyDigest,
            TimeZone = prefs.TimeZone,
            PreferredCurrency = prefs.PreferredCurrency
        });
    }

    /// <summary>
    /// Updates the current user's alert preferences
    /// </summary>
    [HttpPut("preferences")]
    public async Task<ActionResult<AlertPreferencesResponse>> UpdatePreferences(
        [FromBody] UpdateAlertPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var preferences = new UserPreferences
        {
            EnableRenewalAlerts = request.EnableRenewalAlerts ?? true,
            EnablePriceChangeAlerts = request.EnablePriceChangeAlerts ?? true,
            EnableTrialEndingAlerts = request.EnableTrialEndingAlerts ?? true,
            EnableUnusedSubscriptionAlerts = request.EnableUnusedSubscriptionAlerts ?? true,
            UseDailyDigest = request.UseDailyDigest ?? false,
            TimeZone = request.TimeZone ?? "UTC",
            PreferredCurrency = request.PreferredCurrency ?? "USD"
        };

        var result = await _alertService.UpdateUserPreferencesAsync(userId, preferences, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Alert preferences updated for user {UserId}", userId);

        return Ok(new AlertPreferencesResponse
        {
            EnableRenewalAlerts = preferences.EnableRenewalAlerts,
            EnablePriceChangeAlerts = preferences.EnablePriceChangeAlerts,
            EnableTrialEndingAlerts = preferences.EnableTrialEndingAlerts,
            EnableUnusedSubscriptionAlerts = preferences.EnableUnusedSubscriptionAlerts,
            UseDailyDigest = preferences.UseDailyDigest,
            TimeZone = preferences.TimeZone,
            PreferredCurrency = preferences.PreferredCurrency
        });
    }

    /// <summary>
    /// Manually triggers alert generation for the current user
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<AlertGenerationResponse>> GenerateAlerts(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _alertService.GenerateAllAlertsAsync(userId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        var summary = result.Value;
        _logger.LogInformation("Generated {Count} alerts for user {UserId}", 
            summary.TotalAlertsGenerated, userId);

        return Ok(new AlertGenerationResponse
        {
            RenewalAlertsGenerated = summary.RenewalAlertsGenerated,
            PriceChangeAlertsGenerated = summary.PriceChangeAlertsGenerated,
            TrialEndingAlertsGenerated = summary.TrialEndingAlertsGenerated,
            UnusedSubscriptionAlertsGenerated = summary.UnusedSubscriptionAlertsGenerated,
            TotalAlertsGenerated = summary.TotalAlertsGenerated,
            SkippedDueToDuplicates = summary.SkippedDueToDuplicates,
            SkippedDueToPreferences = summary.SkippedDueToPreferences
        });
    }

    private string? GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    private static AlertResponse MapToResponse(Alert alert)
    {
        return new AlertResponse
        {
            Id = alert.Id,
            SubscriptionId = alert.SubscriptionId,
            Type = alert.Type.ToString(),
            Message = alert.Message,
            ScheduledFor = alert.ScheduledFor,
            SentAt = alert.SentAt,
            Status = alert.Status.ToString()
        };
    }
}

#region Request/Response DTOs

public class SnoozeAlertRequest
{
    [Range(1, 168, ErrorMessage = "Snooze hours must be between 1 and 168")]
    public int? Hours { get; set; }
}

public class UpdateAlertPreferencesRequest
{
    public bool? EnableRenewalAlerts { get; set; }
    public bool? EnablePriceChangeAlerts { get; set; }
    public bool? EnableTrialEndingAlerts { get; set; }
    public bool? EnableUnusedSubscriptionAlerts { get; set; }
    public bool? UseDailyDigest { get; set; }
    
    [StringLength(50, ErrorMessage = "TimeZone must be at most 50 characters")]
    public string? TimeZone { get; set; }
    
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter code")]
    public string? PreferredCurrency { get; set; }
}

public class AlertResponse
{
    public string Id { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AlertPreferencesResponse
{
    public bool EnableRenewalAlerts { get; set; }
    public bool EnablePriceChangeAlerts { get; set; }
    public bool EnableTrialEndingAlerts { get; set; }
    public bool EnableUnusedSubscriptionAlerts { get; set; }
    public bool UseDailyDigest { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string PreferredCurrency { get; set; } = "USD";
}

public class AlertGenerationResponse
{
    public int RenewalAlertsGenerated { get; set; }
    public int PriceChangeAlertsGenerated { get; set; }
    public int TrialEndingAlertsGenerated { get; set; }
    public int UnusedSubscriptionAlertsGenerated { get; set; }
    public int TotalAlertsGenerated { get; set; }
    public int SkippedDueToDuplicates { get; set; }
    public int SkippedDueToPreferences { get; set; }
}

#endregion
