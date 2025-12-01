using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Enums;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for subscription management operations including CRUD, filtering, and approval workflows
/// </summary>
[ApiController]
[Route("api/subscriptions")]
[Authorize]
[Produces("application/json")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITierService _tierService;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        ITierService tierService,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionService = subscriptionService;
        _tierService = tierService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all subscriptions for the current user with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SubscriptionResponse>>> GetSubscriptions(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? sortBy,
        [FromQuery] bool descending = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        SubscriptionStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubscriptionStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var result = await _subscriptionService.GetUserSubscriptionsAsync(
            userId, statusFilter, category, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        var subscriptions = result.Value.Select(MapToResponse);

        // Apply sorting
        subscriptions = sortBy?.ToLower() switch
        {
            "name" => descending 
                ? subscriptions.OrderByDescending(s => s.ServiceName)
                : subscriptions.OrderBy(s => s.ServiceName),
            "price" => descending 
                ? subscriptions.OrderByDescending(s => s.MonthlyPrice)
                : subscriptions.OrderBy(s => s.MonthlyPrice),
            "renewal" => descending 
                ? subscriptions.OrderByDescending(s => s.NextRenewalDate)
                : subscriptions.OrderBy(s => s.NextRenewalDate),
            _ => subscriptions.OrderBy(s => s.ServiceName)
        };

        return Ok(subscriptions);
    }

    /// <summary>
    /// Gets a specific subscription by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SubscriptionDetailResponse>> GetSubscription(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _subscriptionService.GetByIdAsync(id, cancellationToken);

        if (result.IsFailure)
            return NotFound(new { error = result.ErrorMessage });

        var subscription = result.Value;
        if (subscription.UserId != userId)
            return Forbid();

        return Ok(MapToDetailResponse(subscription));
    }

    /// <summary>
    /// Manually adds a new subscription
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SubscriptionResponse>> CreateSubscription(
        [FromBody] CreateSubscriptionApiRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check tier limits
        var canAddResult = await _tierService.CanAddSubscriptionAsync(userId, cancellationToken);
        if (canAddResult.IsFailure)
            return BadRequest(new { error = canAddResult.ErrorMessage });

        if (!canAddResult.Value)
        {
            return BadRequest(new { 
                error = "Subscription limit reached for your tier",
                code = "SUBSCRIPTION_LIMIT_REACHED",
                suggestedAction = "Upgrade to a higher tier for more subscriptions"
            });
        }

        if (!Enum.TryParse<BillingCycle>(request.BillingCycle, true, out var billingCycle))
        {
            return BadRequest(new { error = "Invalid billing cycle. Supported: Weekly, Monthly, Quarterly, Annual" });
        }

        var createRequest = new CreateSubscriptionRequest
        {
            UserId = userId,
            EmailAccountId = request.EmailAccountId ?? string.Empty,
            ServiceName = request.ServiceName,
            Price = request.Price,
            Currency = request.Currency ?? "USD",
            BillingCycle = billingCycle,
            NextRenewalDate = request.NextRenewalDate,
            Category = request.Category,
            CancellationLink = request.CancellationLink,
            ExtractionConfidence = 1.0 // Manual entry = full confidence
        };

        var result = await _subscriptionService.CreateOrUpdateAsync(createRequest, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Subscription {ServiceName} created manually for user {UserId}", 
            request.ServiceName, userId);

        return CreatedAtAction(
            nameof(GetSubscription),
            new { id = result.Value.Id },
            MapToResponse(result.Value));
    }

    /// <summary>
    /// Updates an existing subscription
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<SubscriptionResponse>> UpdateSubscription(
        string id,
        [FromBody] UpdateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var existingResult = await _subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existingResult.IsFailure)
            return NotFound(new { error = existingResult.ErrorMessage });

        var subscription = existingResult.Value;
        if (subscription.UserId != userId)
            return Forbid();

        // Update price if changed
        if (request.Price.HasValue && request.Price.Value != subscription.Price)
        {
            var priceResult = await _subscriptionService.UpdatePriceAsync(
                id, request.Price.Value, null, cancellationToken);
            if (priceResult.IsFailure)
                return BadRequest(new { error = priceResult.ErrorMessage });
        }

        // Update status if changed
        if (!string.IsNullOrEmpty(request.Status) && 
            Enum.TryParse<SubscriptionStatus>(request.Status, true, out var newStatus) &&
            newStatus != subscription.Status)
        {
            var statusResult = await _subscriptionService.UpdateStatusAsync(
                id, newStatus, null, cancellationToken);
            if (statusResult.IsFailure)
                return BadRequest(new { error = statusResult.ErrorMessage });
        }

        // Fetch updated subscription
        var updatedResult = await _subscriptionService.GetByIdAsync(id, cancellationToken);
        if (updatedResult.IsFailure)
            return BadRequest(new { error = updatedResult.ErrorMessage });

        _logger.LogInformation("Subscription {SubscriptionId} updated for user {UserId}", id, userId);

        return Ok(MapToResponse(updatedResult.Value));
    }

    /// <summary>
    /// Deletes (archives) a subscription
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubscription(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var existingResult = await _subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existingResult.IsFailure)
            return NotFound(new { error = existingResult.ErrorMessage });

        if (existingResult.Value.UserId != userId)
            return Forbid();

        var result = await _subscriptionService.UpdateStatusAsync(
            id, SubscriptionStatus.Archived, null, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Subscription {SubscriptionId} archived for user {UserId}", id, userId);

        return NoContent();
    }

    /// <summary>
    /// Gets subscriptions pending user review
    /// </summary>
    [HttpGet("pending-review")]
    public async Task<ActionResult<IEnumerable<SubscriptionResponse>>> GetPendingReview(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _subscriptionService.GetPendingReviewAsync(userId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value.Select(MapToResponse));
    }

    /// <summary>
    /// Approves a subscription pending review
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveSubscription(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var existingResult = await _subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existingResult.IsFailure)
            return NotFound(new { error = existingResult.ErrorMessage });

        if (existingResult.Value.UserId != userId)
            return Forbid();

        var result = await _subscriptionService.ApproveSubscriptionAsync(id, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Subscription approved" });
    }

    /// <summary>
    /// Rejects a subscription pending review
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectSubscription(
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var existingResult = await _subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existingResult.IsFailure)
            return NotFound(new { error = existingResult.ErrorMessage });

        if (existingResult.Value.UserId != userId)
            return Forbid();

        var result = await _subscriptionService.RejectSubscriptionAsync(id, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Subscription rejected" });
    }

    /// <summary>
    /// Gets upcoming renewals for the current user
    /// </summary>
    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<SubscriptionResponse>>> GetUpcomingRenewals(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _subscriptionService.GetUpcomingRenewalsAsync(userId, days, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value.Select(MapToResponse));
    }

    private string? GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    private static SubscriptionResponse MapToResponse(Domain.Entities.Subscription subscription)
    {
        var monthlyPrice = NormalizeToMonthly(subscription.Price, subscription.BillingCycle);
        var daysUntilRenewal = subscription.NextRenewalDate.HasValue
            ? (int)(subscription.NextRenewalDate.Value - DateTime.UtcNow).TotalDays
            : (int?)null;

        return new SubscriptionResponse
        {
            Id = subscription.Id,
            ServiceName = subscription.ServiceName,
            Price = subscription.Price,
            MonthlyPrice = monthlyPrice,
            Currency = subscription.Currency,
            BillingCycle = subscription.BillingCycle.ToString(),
            Category = subscription.Category,
            Status = subscription.Status.ToString(),
            NextRenewalDate = subscription.NextRenewalDate,
            DaysUntilRenewal = daysUntilRenewal,
            IsInDangerZone = daysUntilRenewal.HasValue && daysUntilRenewal <= 7,
            RequiresReview = subscription.RequiresUserReview,
            EmailAccountId = subscription.EmailAccountId,
            VendorLogoUrl = subscription.Vendor?.LogoUrl,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt
        };
    }

    private static SubscriptionDetailResponse MapToDetailResponse(Domain.Entities.Subscription subscription)
    {
        var response = MapToResponse(subscription);
        return new SubscriptionDetailResponse
        {
            Id = response.Id,
            ServiceName = response.ServiceName,
            Price = response.Price,
            MonthlyPrice = response.MonthlyPrice,
            Currency = response.Currency,
            BillingCycle = response.BillingCycle,
            Category = response.Category,
            Status = response.Status,
            NextRenewalDate = response.NextRenewalDate,
            DaysUntilRenewal = response.DaysUntilRenewal,
            IsInDangerZone = response.IsInDangerZone,
            RequiresReview = response.RequiresReview,
            EmailAccountId = response.EmailAccountId,
            VendorLogoUrl = response.VendorLogoUrl,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
            CancellationLink = subscription.CancellationLink,
            ExtractionConfidence = subscription.ExtractionConfidence,
            CancelledAt = subscription.CancelledAt,
            LastActivityEmailAt = subscription.LastActivityEmailAt,
            VendorWebsiteUrl = subscription.Vendor?.WebsiteUrl
        };
    }

    private static decimal NormalizeToMonthly(decimal price, BillingCycle billingCycle)
    {
        return billingCycle switch
        {
            BillingCycle.Weekly => price * 4.33m,
            BillingCycle.Monthly => price,
            BillingCycle.Quarterly => price / 3m,
            BillingCycle.Annual => price / 12m,
            _ => price
        };
    }
}

#region Request/Response DTOs

public class CreateSubscriptionApiRequest
{
    [Required(ErrorMessage = "Service name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Service name must be between 1 and 200 characters")]
    public string ServiceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(0, 100000, ErrorMessage = "Price must be between 0 and 100,000")]
    public decimal Price { get; set; }

    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter code")]
    public string? Currency { get; set; }

    [Required(ErrorMessage = "Billing cycle is required")]
    public string BillingCycle { get; set; } = string.Empty;

    public DateTime? NextRenewalDate { get; set; }

    [StringLength(100, ErrorMessage = "Category must be at most 100 characters")]
    public string? Category { get; set; }

    public string? EmailAccountId { get; set; }

    [Url(ErrorMessage = "Invalid cancellation link URL")]
    public string? CancellationLink { get; set; }
}

public class UpdateSubscriptionRequest
{
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Service name must be between 1 and 200 characters")]
    public string? ServiceName { get; set; }

    [Range(0, 100000, ErrorMessage = "Price must be between 0 and 100,000")]
    public decimal? Price { get; set; }

    public string? BillingCycle { get; set; }

    public DateTime? NextRenewalDate { get; set; }

    [StringLength(100, ErrorMessage = "Category must be at most 100 characters")]
    public string? Category { get; set; }

    public string? Status { get; set; }

    [Url(ErrorMessage = "Invalid cancellation link URL")]
    public string? CancellationLink { get; set; }
}

public class SubscriptionResponse
{
    public string Id { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string BillingCycle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? NextRenewalDate { get; set; }
    public int? DaysUntilRenewal { get; set; }
    public bool IsInDangerZone { get; set; }
    public bool RequiresReview { get; set; }
    public string EmailAccountId { get; set; } = string.Empty;
    public string? VendorLogoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SubscriptionDetailResponse : SubscriptionResponse
{
    public string? CancellationLink { get; set; }
    public double ExtractionConfidence { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? LastActivityEmailAt { get; set; }
    public string? VendorWebsiteUrl { get; set; }
}

#endregion
