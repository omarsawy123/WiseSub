using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for user profile, preferences, and GDPR data management
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAlertService _alertService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        IAlertService alertService,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user's profile
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _userService.GetUserByIdAsync(userId);

        if (result.IsFailure)
            return NotFound(new { error = result.ErrorMessage });

        var user = result.Value;
        var prefsResult = await _alertService.GetUserPreferencesAsync(userId, cancellationToken);
        var preferences = prefsResult.IsSuccess ? prefsResult.Value : new UserPreferences();

        return Ok(new UserProfileResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Tier = user.Tier.ToString(),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Preferences = new UserPreferencesResponse
            {
                EnableRenewalAlerts = preferences.EnableRenewalAlerts,
                EnablePriceChangeAlerts = preferences.EnablePriceChangeAlerts,
                EnableTrialEndingAlerts = preferences.EnableTrialEndingAlerts,
                EnableUnusedSubscriptionAlerts = preferences.EnableUnusedSubscriptionAlerts,
                UseDailyDigest = preferences.UseDailyDigest,
                TimeZone = preferences.TimeZone,
                PreferredCurrency = preferences.PreferredCurrency
            }
        });
    }

    /// <summary>
    /// Updates the current user's profile
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var userResult = await _userService.GetUserByIdAsync(userId);
        if (userResult.IsFailure)
            return NotFound(new { error = userResult.ErrorMessage });

        var user = userResult.Value;

        // Update user name if provided
        if (!string.IsNullOrEmpty(request.Name))
        {
            user.Name = request.Name;
            var updateResult = await _userService.UpdateUserAsync(user);
            if (updateResult.IsFailure)
                return BadRequest(new { error = updateResult.ErrorMessage });
        }

        // Update preferences if provided
        if (request.Preferences != null)
        {
            var preferences = new UserPreferences
            {
                EnableRenewalAlerts = request.Preferences.EnableRenewalAlerts ?? true,
                EnablePriceChangeAlerts = request.Preferences.EnablePriceChangeAlerts ?? true,
                EnableTrialEndingAlerts = request.Preferences.EnableTrialEndingAlerts ?? true,
                EnableUnusedSubscriptionAlerts = request.Preferences.EnableUnusedSubscriptionAlerts ?? true,
                UseDailyDigest = request.Preferences.UseDailyDigest ?? false,
                TimeZone = request.Preferences.TimeZone ?? "UTC",
                PreferredCurrency = request.Preferences.PreferredCurrency ?? "USD"
            };

            var prefsResult = await _alertService.UpdateUserPreferencesAsync(userId, preferences, cancellationToken);
            if (prefsResult.IsFailure)
                return BadRequest(new { error = prefsResult.ErrorMessage });
        }

        _logger.LogInformation("Profile updated for user {UserId}", userId);

        // Return updated profile
        return await GetProfile(cancellationToken);
    }

    /// <summary>
    /// Updates the current user's preferences
    /// </summary>
    [HttpPut("me/preferences")]
    public async Task<ActionResult<UserPreferencesResponse>> UpdatePreferences(
        [FromBody] UpdatePreferencesRequest request,
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

        _logger.LogInformation("Preferences updated for user {UserId}", userId);

        return Ok(new UserPreferencesResponse
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
    /// Exports all user data in JSON format (GDPR compliance)
    /// </summary>
    [HttpGet("me/export")]
    public async Task<IActionResult> ExportData(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _userService.ExportUserDataAsync(userId);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Data exported for user {UserId}", userId);

        return File(result.Value, "application/json", $"wisesub-export-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    /// <summary>
    /// Requests deletion of all user data (GDPR compliance)
    /// </summary>
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] DeleteAccountRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Require confirmation
        if (request?.Confirm != true)
        {
            return BadRequest(new { 
                error = "Account deletion requires confirmation",
                message = "Set 'confirm' to true to proceed with account deletion"
            });
        }

        var result = await _userService.DeleteUserDataAsync(userId);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Account deleted for user {UserId}", userId);

        return Ok(new { 
            message = "Account deletion initiated. All data will be permanently deleted within 24 hours.",
            deletedAt = DateTime.UtcNow
        });
    }

    private string? GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}

#region Request/Response DTOs

public class UpdateProfileRequest
{
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string? Name { get; set; }

    public UpdatePreferencesRequest? Preferences { get; set; }
}

public class UpdatePreferencesRequest
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

public class DeleteAccountRequest
{
    /// <summary>
    /// Must be true to confirm account deletion
    /// </summary>
    public bool Confirm { get; set; }
}

public class UserProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public UserPreferencesResponse Preferences { get; set; } = new();
}

public class UserPreferencesResponse
{
    public bool EnableRenewalAlerts { get; set; }
    public bool EnablePriceChangeAlerts { get; set; }
    public bool EnableTrialEndingAlerts { get; set; }
    public bool EnableUnusedSubscriptionAlerts { get; set; }
    public bool UseDailyDigest { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string PreferredCurrency { get; set; } = "USD";
}

#endregion
