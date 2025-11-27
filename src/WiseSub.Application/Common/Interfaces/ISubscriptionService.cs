using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service interface for subscription management business logic
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Creates a new subscription or updates an existing duplicate (85% fuzzy match)
    /// </summary>
    Task<Result<Subscription>> CreateOrUpdateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all subscriptions for a user with optional filtering
    /// </summary>
    Task<Result<IEnumerable<Subscription>>> GetUserSubscriptionsAsync(
        string userId, 
        SubscriptionStatus? statusFilter = null,
        string? categoryFilter = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific subscription by ID
    /// </summary>
    Task<Result<Subscription>> GetByIdAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates subscription status with history tracking
    /// </summary>
    Task<Result> UpdateStatusAsync(string subscriptionId, SubscriptionStatus newStatus, string? sourceEmailId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates subscription price with history tracking
    /// </summary>
    Task<Result> UpdatePriceAsync(string subscriptionId, decimal newPrice, string? sourceEmailId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets upcoming subscription renewals within specified days
    /// </summary>
    Task<Result<IEnumerable<Subscription>>> GetUpcomingRenewalsAsync(string userId, int daysAhead = 7, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets subscriptions requiring user review
    /// </summary>
    Task<Result<IEnumerable<Subscription>>> GetPendingReviewAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Approves a subscription pending review
    /// </summary>
    Task<Result> ApproveSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rejects/archives a subscription pending review
    /// </summary>
    Task<Result> RejectSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets total monthly spending for a user (normalized from all billing cycles)
    /// </summary>
    Task<Result<decimal>> GetMonthlySpendingAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets spending breakdown by category
    /// </summary>
    Task<Result<Dictionary<string, decimal>>> GetSpendingByCategoryAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Archives all subscriptions for an email account (when account is disconnected)
    /// </summary>
    Task<Result> ArchiveByEmailAccountAsync(string emailAccountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculates the monthly normalized price for a subscription
    /// </summary>
    decimal NormalizeToMonthly(decimal price, BillingCycle billingCycle);

    /// <summary>
    /// Creates or updates a subscription from AI extraction result
    /// </summary>
    Task<Result<Subscription>> CreateOrUpdateFromExtractionAsync(
        string userId,
        ExtractionResult extraction,
        string sourceEmailId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request model for creating or updating a subscription
/// </summary>
public class CreateSubscriptionRequest
{
    public required string UserId { get; init; }
    public required string EmailAccountId { get; init; }
    public required string ServiceName { get; init; }
    public required decimal Price { get; init; }
    public string Currency { get; init; } = "USD";
    public required BillingCycle BillingCycle { get; init; }
    public DateTime? NextRenewalDate { get; init; }
    public string? Category { get; init; }
    public string? VendorId { get; init; }
    public string? CancellationLink { get; init; }
    public double ExtractionConfidence { get; init; } = 1.0;
    public string? SourceEmailId { get; init; }
}
