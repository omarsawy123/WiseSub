using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Enums;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for managing email account connections via OAuth
/// </summary>
[ApiController]
[Route("api/email-accounts")]
[Authorize]
[Produces("application/json")]
public class EmailAccountController : ControllerBase
{
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITierService _tierService;
    private readonly ILogger<EmailAccountController> _logger;

    public EmailAccountController(
        IEmailAccountRepository emailAccountRepository,
        ISubscriptionService subscriptionService,
        ITierService tierService,
        ILogger<EmailAccountController> logger)
    {
        _emailAccountRepository = emailAccountRepository;
        _subscriptionService = subscriptionService;
        _tierService = tierService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all email accounts for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmailAccountResponse>>> GetEmailAccounts(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var accounts = await _emailAccountRepository.GetByUserIdAsync(userId, cancellationToken);
        
        var response = accounts.Select(a => new EmailAccountResponse
        {
            Id = a.Id,
            EmailAddress = a.EmailAddress,
            Provider = a.Provider.ToString(),
            IsActive = a.IsActive,
            ConnectedAt = a.ConnectedAt,
            LastScanAt = a.LastScanAt
        });

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific email account by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<EmailAccountResponse>> GetEmailAccount(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var account = await _emailAccountRepository.GetByIdAsync(id, cancellationToken);
        
        if (account == null)
            return NotFound(new { error = "Email account not found" });

        if (account.UserId != userId)
            return Forbid();

        return Ok(new EmailAccountResponse
        {
            Id = account.Id,
            EmailAddress = account.EmailAddress,
            Provider = account.Provider.ToString(),
            IsActive = account.IsActive,
            ConnectedAt = account.ConnectedAt,
            LastScanAt = account.LastScanAt
        });
    }

    /// <summary>
    /// Connects a new email account via OAuth
    /// </summary>
    [HttpPost("connect")]
    public async Task<ActionResult<EmailAccountResponse>> ConnectEmailAccount(
        [FromBody] ConnectEmailAccountRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check tier limits
        var canAddResult = await _tierService.CanAddEmailAccountAsync(userId, cancellationToken);
        if (canAddResult.IsFailure)
            return BadRequest(new { error = canAddResult.ErrorMessage });

        if (!canAddResult.Value)
        {
            return BadRequest(new { 
                error = "Email account limit reached for your tier",
                code = "EMAIL_ACCOUNT_LIMIT_REACHED",
                suggestedAction = "Upgrade to a higher tier for more email accounts"
            });
        }

        // Check if email is already connected
        var existingAccount = await _emailAccountRepository.GetByEmailAddressAsync(
            request.EmailAddress, cancellationToken);
        
        if (existingAccount != null && existingAccount.UserId == userId && existingAccount.IsActive)
        {
            return Conflict(new { error = "This email account is already connected" });
        }

        // Parse provider
        if (!Enum.TryParse<EmailProvider>(request.Provider, true, out var provider))
        {
            return BadRequest(new { error = "Invalid email provider. Supported: Gmail, Outlook" });
        }

        // Create new email account
        var emailAccount = new Domain.Entities.EmailAccount
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            EmailAddress = request.EmailAddress,
            Provider = provider,
            EncryptedAccessToken = request.AccessToken, // Should be encrypted by service
            EncryptedRefreshToken = request.RefreshToken ?? string.Empty,
            TokenExpiresAt = request.TokenExpiresAt ?? DateTime.UtcNow.AddHours(1),
            ConnectedAt = DateTime.UtcNow,
            LastScanAt = DateTime.MinValue,
            IsActive = true
        };

        await _emailAccountRepository.AddAsync(emailAccount, cancellationToken);

        _logger.LogInformation("Email account {EmailAddress} connected for user {UserId}", 
            request.EmailAddress, userId);

        return CreatedAtAction(
            nameof(GetEmailAccount),
            new { id = emailAccount.Id },
            new EmailAccountResponse
            {
                Id = emailAccount.Id,
                EmailAddress = emailAccount.EmailAddress,
                Provider = emailAccount.Provider.ToString(),
                IsActive = emailAccount.IsActive,
                ConnectedAt = emailAccount.ConnectedAt,
                LastScanAt = emailAccount.LastScanAt
            });
    }

    /// <summary>
    /// Disconnects an email account and archives associated subscriptions
    /// </summary>
    [HttpPost("{id}/disconnect")]
    public async Task<IActionResult> DisconnectEmailAccount(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var account = await _emailAccountRepository.GetByIdAsync(id, cancellationToken);
        
        if (account == null)
            return NotFound(new { error = "Email account not found" });

        if (account.UserId != userId)
            return Forbid();

        // Revoke access (deletes tokens and marks as inactive)
        await _emailAccountRepository.RevokeAccessAsync(id, cancellationToken);

        // Archive associated subscriptions
        var archiveResult = await _subscriptionService.ArchiveByEmailAccountAsync(id, cancellationToken);
        if (archiveResult.IsFailure)
        {
            _logger.LogWarning("Failed to archive subscriptions for email account {AccountId}: {Error}",
                id, archiveResult.ErrorMessage);
        }

        _logger.LogInformation("Email account {AccountId} disconnected for user {UserId}", id, userId);

        return Ok(new { message = "Email account disconnected successfully" });
    }

    /// <summary>
    /// Triggers a manual scan for an email account
    /// </summary>
    [HttpPost("{id}/scan")]
    public async Task<IActionResult> TriggerScan(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var account = await _emailAccountRepository.GetByIdAsync(id, cancellationToken);
        
        if (account == null)
            return NotFound(new { error = "Email account not found" });

        if (account.UserId != userId)
            return Forbid();

        if (!account.IsActive)
            return BadRequest(new { error = "Email account is not active" });

        // Queue a background job for scanning
        // In a real implementation, this would use Hangfire
        _logger.LogInformation("Manual scan triggered for email account {AccountId}", id);

        return Accepted(new { 
            message = "Scan initiated",
            accountId = id
        });
    }

    private string? GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}

#region Request/Response DTOs

public class ConnectEmailAccountRequest
{
    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    public string EmailAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Provider is required")]
    public string Provider { get; set; } = string.Empty;

    [Required(ErrorMessage = "Access token is required")]
    public string AccessToken { get; set; } = string.Empty;

    public string? RefreshToken { get; set; }

    public DateTime? TokenExpiresAt { get; set; }
}

public class EmailAccountResponse
{
    public string Id { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastScanAt { get; set; }
}

#endregion
