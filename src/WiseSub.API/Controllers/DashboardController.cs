using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for dashboard data, spending insights, and renewal timeline
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IFeatureAccessService _featureAccessService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardService dashboardService,
        IFeatureAccessService featureAccessService,
        ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _featureAccessService = featureAccessService;
        _logger = logger;
    }

    /// <summary>
    /// Gets aggregated dashboard data including active subscriptions, spending summary, and upcoming renewals
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDataResponse>> GetDashboardData(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _dashboardService.GetDashboardDataAsync(userId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Value;
        return Ok(new DashboardDataResponse
        {
            ActiveSubscriptions = data.ActiveSubscriptions,
            TotalMonthlySpend = data.TotalMonthlySpend,
            SpendingByCategory = data.SpendingByCategory,
            UpcomingRenewals = data.UpcomingRenewals,
            TotalSubscriptionCount = data.TotalSubscriptionCount,
            ActiveSubscriptionCount = data.ActiveSubscriptionCount,
            RequiringReview = data.RequiringReview
        });
    }

    /// <summary>
    /// Gets detailed spending insights including category breakdown and weekly concentration
    /// </summary>
    [HttpGet("insights")]
    public async Task<ActionResult<SpendingInsightsResponse>> GetSpendingInsights(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check if user has access to spending insights (Pro+ feature)
        var accessResult = await _featureAccessService.CanUseSpendingByCategoryAsync(userId, cancellationToken);
        if (accessResult.IsFailure || !accessResult.Value)
        {
            return StatusCode(403, new { 
                error = "Spending insights require Pro or Premium tier",
                code = "FEATURE_NOT_AVAILABLE",
                suggestedAction = "Upgrade to Pro tier to access spending insights"
            });
        }

        var result = await _dashboardService.GetSpendingInsightsAsync(userId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        var insights = result.Value;
        return Ok(new SpendingInsightsResponse
        {
            TotalMonthlySpend = insights.TotalMonthlySpend,
            ProjectedYearlySpend = insights.ProjectedYearlySpend,
            SpendingByCategory = insights.SpendingByCategory,
            WeeklyRenewalConcentration = insights.WeeklyRenewalConcentration,
            MostExpensiveSubscription = insights.MostExpensiveSubscription,
            MostExpensiveAmount = insights.MostExpensiveAmount,
            AverageMonthlyPerSubscription = insights.AverageMonthlyPerSubscription,
            ActiveSubscriptionCount = insights.ActiveSubscriptionCount
        });
    }

    /// <summary>
    /// Gets renewal timeline showing upcoming renewals for the specified number of months
    /// </summary>
    [HttpGet("timeline")]
    public async Task<ActionResult<RenewalTimelineResponse>> GetRenewalTimeline(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check if user has access to renewal timeline (Pro+ feature)
        var accessResult = await _featureAccessService.CanUseRenewalTimelineAsync(userId, cancellationToken);
        if (accessResult.IsFailure || !accessResult.Value)
        {
            return StatusCode(403, new { 
                error = "Renewal timeline requires Pro or Premium tier",
                code = "FEATURE_NOT_AVAILABLE",
                suggestedAction = "Upgrade to Pro tier to access renewal timeline"
            });
        }

        // Limit months to reasonable range
        months = Math.Clamp(months, 1, 24);

        var result = await _dashboardService.GetRenewalTimelineAsync(userId, months, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        var timeline = result.Value;
        return Ok(new RenewalTimelineResponse
        {
            Events = timeline.Events,
            MonthlySummaries = timeline.MonthlySummaries,
            TotalAmount = timeline.TotalAmount,
            MonthsCovered = timeline.MonthsCovered
        });
    }

    /// <summary>
    /// Gets a summary of spending for quick overview
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<SpendingSummaryResponse>> GetSpendingSummary(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _dashboardService.GetDashboardDataAsync(userId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Value;
        return Ok(new SpendingSummaryResponse
        {
            TotalMonthlySpend = data.TotalMonthlySpend,
            ProjectedYearlySpend = data.TotalMonthlySpend * 12,
            ActiveSubscriptionCount = data.ActiveSubscriptionCount,
            UpcomingRenewalCount = data.UpcomingRenewals.Count(),
            PendingReviewCount = data.RequiringReview.Count()
        });
    }

    private string? GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}

#region Response DTOs

public class DashboardDataResponse
{
    public IEnumerable<SubscriptionSummary> ActiveSubscriptions { get; set; } = Enumerable.Empty<SubscriptionSummary>();
    public decimal TotalMonthlySpend { get; set; }
    public Dictionary<string, decimal> SpendingByCategory { get; set; } = new();
    public IEnumerable<SubscriptionSummary> UpcomingRenewals { get; set; } = Enumerable.Empty<SubscriptionSummary>();
    public int TotalSubscriptionCount { get; set; }
    public int ActiveSubscriptionCount { get; set; }
    public IEnumerable<SubscriptionSummary> RequiringReview { get; set; } = Enumerable.Empty<SubscriptionSummary>();
}

public class SpendingInsightsResponse
{
    public decimal TotalMonthlySpend { get; set; }
    public decimal ProjectedYearlySpend { get; set; }
    public IEnumerable<CategorySpending> SpendingByCategory { get; set; } = Enumerable.Empty<CategorySpending>();
    public IEnumerable<WeeklySpending> WeeklyRenewalConcentration { get; set; } = Enumerable.Empty<WeeklySpending>();
    public string? MostExpensiveSubscription { get; set; }
    public decimal MostExpensiveAmount { get; set; }
    public decimal AverageMonthlyPerSubscription { get; set; }
    public int ActiveSubscriptionCount { get; set; }
}

public class RenewalTimelineResponse
{
    public IEnumerable<RenewalEvent> Events { get; set; } = Enumerable.Empty<RenewalEvent>();
    public IEnumerable<MonthlyRenewalSummary> MonthlySummaries { get; set; } = Enumerable.Empty<MonthlyRenewalSummary>();
    public decimal TotalAmount { get; set; }
    public int MonthsCovered { get; set; }
}

public class SpendingSummaryResponse
{
    public decimal TotalMonthlySpend { get; set; }
    public decimal ProjectedYearlySpend { get; set; }
    public int ActiveSubscriptionCount { get; set; }
    public int UpcomingRenewalCount { get; set; }
    public int PendingReviewCount { get; set; }
}

#endregion
