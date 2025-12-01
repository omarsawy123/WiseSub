using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

/// <summary>
/// Service for aggregating dashboard data, spending insights, and renewal timeline
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailAccountRepository _emailAccountRepository;
    
    private const int DangerZoneDays = 7;
    private const int UpcomingRenewalDays = 30;
    private const double HighConcentrationThreshold = 0.4; // 40%

    public DashboardService(
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        IEmailAccountRepository emailAccountRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _emailAccountRepository = emailAccountRepository;
    }

    public async Task<Result<DashboardData>> GetDashboardDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<DashboardData>(UserErrors.NotFound);
        }

        // Get all subscriptions and email accounts
        var allSubscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        var emailAccounts = await _emailAccountRepository.GetByUserIdAsync(userId, cancellationToken);
        var emailAccountMap = emailAccounts.ToDictionary(e => e.Id, e => e.EmailAddress);
        
        var subscriptionList = allSubscriptions.ToList();
        var activeSubscriptions = subscriptionList.Where(s => s.Status == SubscriptionStatus.Active).ToList();
        
        // Get upcoming renewals (next 30 days)
        var upcomingRenewals = activeSubscriptions
            .Where(s => s.NextRenewalDate.HasValue && 
                        s.NextRenewalDate.Value <= DateTime.UtcNow.AddDays(UpcomingRenewalDays) &&
                        s.NextRenewalDate.Value >= DateTime.UtcNow)
            .OrderBy(s => s.NextRenewalDate)
            .ToList();
        
        // Get subscriptions requiring review
        var requiresReview = subscriptionList
            .Where(s => s.RequiresUserReview || s.Status == SubscriptionStatus.PendingReview)
            .ToList();
        
        // Calculate spending
        var totalMonthlySpend = CalculateTotalMonthlySpend(activeSubscriptions);
        var spendingByCategory = CalculateSpendingByCategory(activeSubscriptions);
        
        var dashboardData = new DashboardData
        {
            ActiveSubscriptions = activeSubscriptions.Select(s => MapToSummary(s, emailAccountMap)),
            TotalMonthlySpend = totalMonthlySpend,
            SpendingByCategory = spendingByCategory,
            UpcomingRenewals = upcomingRenewals.Select(s => MapToSummary(s, emailAccountMap)),
            TotalSubscriptionCount = subscriptionList.Count,
            ActiveSubscriptionCount = activeSubscriptions.Count,
            RequiringReview = requiresReview.Select(s => MapToSummary(s, emailAccountMap))
        };
        
        return Result.Success(dashboardData);
    }

    public async Task<Result<SpendingInsights>> GetSpendingInsightsAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<SpendingInsights>(UserErrors.NotFound);
        }

        var activeSubscriptions = (await _subscriptionRepository.GetByUserIdAndStatusAsync(
            userId, SubscriptionStatus.Active, cancellationToken)).ToList();

        if (!activeSubscriptions.Any())
        {
            return Result.Success(new SpendingInsights
            {
                TotalMonthlySpend = 0,
                ProjectedYearlySpend = 0,
                SpendingByCategory = Enumerable.Empty<CategorySpending>(),
                WeeklyRenewalConcentration = Enumerable.Empty<WeeklySpending>(),
                ActiveSubscriptionCount = 0
            });
        }

        var totalMonthlySpend = CalculateTotalMonthlySpend(activeSubscriptions);
        var categorySpending = CalculateCategorySpending(activeSubscriptions, totalMonthlySpend);
        var weeklyConcentration = CalculateWeeklyConcentration(activeSubscriptions);
        
        // Find most expensive subscription
        var mostExpensive = activeSubscriptions
            .Select(s => new { s.ServiceName, MonthlyPrice = NormalizeToMonthly(s.Price, s.BillingCycle) })
            .OrderByDescending(s => s.MonthlyPrice)
            .FirstOrDefault();

        var insights = new SpendingInsights
        {
            TotalMonthlySpend = totalMonthlySpend,
            ProjectedYearlySpend = totalMonthlySpend * 12,
            SpendingByCategory = categorySpending,
            WeeklyRenewalConcentration = weeklyConcentration,
            MostExpensiveSubscription = mostExpensive?.ServiceName,
            MostExpensiveAmount = mostExpensive?.MonthlyPrice ?? 0,
            AverageMonthlyPerSubscription = activeSubscriptions.Count > 0 
                ? totalMonthlySpend / activeSubscriptions.Count 
                : 0,
            ActiveSubscriptionCount = activeSubscriptions.Count
        };

        return Result.Success(insights);
    }

    public async Task<Result<RenewalTimeline>> GetRenewalTimelineAsync(string userId, int months = 12, CancellationToken cancellationToken = default)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<RenewalTimeline>(UserErrors.NotFound);
        }

        if (months < 1 || months > 24)
        {
            months = 12; // Default to 12 months
        }

        var activeSubscriptions = (await _subscriptionRepository.GetByUserIdAndStatusAsync(
            userId, SubscriptionStatus.Active, cancellationToken)).ToList();

        var endDate = DateTime.UtcNow.AddMonths(months);
        var events = new List<RenewalEvent>();
        
        // Calculate the high-value threshold (top 20%)
        var allMonthlyPrices = activeSubscriptions
            .Select(s => NormalizeToMonthly(s.Price, s.BillingCycle))
            .OrderByDescending(p => p)
            .ToList();
        
        var highValueThreshold = allMonthlyPrices.Count > 0 
            ? allMonthlyPrices.Take(Math.Max(1, allMonthlyPrices.Count / 5)).LastOrDefault() 
            : 0;

        foreach (var subscription in activeSubscriptions.Where(s => s.NextRenewalDate.HasValue))
        {
            var renewalDate = subscription.NextRenewalDate!.Value;
            
            // Generate renewal events for the timeline period
            while (renewalDate <= endDate)
            {
                if (renewalDate >= DateTime.UtcNow)
                {
                    var monthlyPrice = NormalizeToMonthly(subscription.Price, subscription.BillingCycle);
                    
                    events.Add(new RenewalEvent
                    {
                        SubscriptionId = subscription.Id,
                        ServiceName = subscription.ServiceName,
                        RenewalDate = renewalDate,
                        Amount = subscription.Price,
                        Currency = subscription.Currency,
                        BillingCycle = subscription.BillingCycle.ToString(),
                        Category = subscription.Category ?? "Uncategorized",
                        VendorLogoUrl = subscription.Vendor?.LogoUrl,
                        IsInDangerZone = renewalDate <= DateTime.UtcNow.AddDays(DangerZoneDays),
                        IsHighValue = monthlyPrice >= highValueThreshold,
                        MonthGroup = renewalDate.ToString("MMMM yyyy")
                    });
                }
                
                // Advance to next renewal based on billing cycle
                renewalDate = AdvanceRenewalDate(renewalDate, subscription.BillingCycle);
            }
        }

        // Sort events chronologically
        var sortedEvents = events.OrderBy(e => e.RenewalDate).ToList();
        
        // Calculate monthly summaries
        var monthlySummaries = CalculateMonthlySummaries(sortedEvents, months);

        var timeline = new RenewalTimeline
        {
            Events = sortedEvents,
            MonthlySummaries = monthlySummaries,
            TotalAmount = sortedEvents.Sum(e => e.Amount),
            MonthsCovered = months
        };

        return Result.Success(timeline);
    }

    #region Private Helper Methods

    private static SubscriptionSummary MapToSummary(Subscription subscription, Dictionary<string, string> emailAccountMap)
    {
        var daysUntilRenewal = subscription.NextRenewalDate.HasValue
            ? (int)(subscription.NextRenewalDate.Value - DateTime.UtcNow).TotalDays
            : (int?)null;

        return new SubscriptionSummary
        {
            Id = subscription.Id,
            ServiceName = subscription.ServiceName,
            Price = subscription.Price,
            MonthlyPrice = NormalizeToMonthly(subscription.Price, subscription.BillingCycle),
            Currency = subscription.Currency,
            BillingCycle = subscription.BillingCycle.ToString(),
            Category = subscription.Category ?? "Uncategorized",
            Status = subscription.Status.ToString(),
            NextRenewalDate = subscription.NextRenewalDate,
            DaysUntilRenewal = daysUntilRenewal,
            IsInDangerZone = daysUntilRenewal.HasValue && daysUntilRenewal.Value <= DangerZoneDays && daysUntilRenewal.Value >= 0,
            VendorLogoUrl = subscription.Vendor?.LogoUrl,
            VendorWebsiteUrl = subscription.Vendor?.WebsiteUrl,
            EmailAccountId = subscription.EmailAccountId,
            EmailAddress = emailAccountMap.TryGetValue(subscription.EmailAccountId, out var email) ? email : string.Empty
        };
    }

    private static decimal CalculateTotalMonthlySpend(IEnumerable<Subscription> subscriptions)
    {
        return subscriptions.Sum(s => NormalizeToMonthly(s.Price, s.BillingCycle));
    }

    private static Dictionary<string, decimal> CalculateSpendingByCategory(IEnumerable<Subscription> subscriptions)
    {
        return subscriptions
            .GroupBy(s => s.Category ?? "Uncategorized")
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => NormalizeToMonthly(s.Price, s.BillingCycle))
            );
    }

    private static IEnumerable<CategorySpending> CalculateCategorySpending(
        IEnumerable<Subscription> subscriptions, 
        decimal totalMonthlySpend)
    {
        return subscriptions
            .GroupBy(s => s.Category ?? "Uncategorized")
            .Select(g =>
            {
                var monthlyAmount = g.Sum(s => NormalizeToMonthly(s.Price, s.BillingCycle));
                return new CategorySpending
                {
                    Category = g.Key,
                    MonthlyAmount = monthlyAmount,
                    Percentage = totalMonthlySpend > 0 
                        ? Math.Round(monthlyAmount / totalMonthlySpend * 100, 2) 
                        : 0,
                    SubscriptionCount = g.Count()
                };
            })
            .OrderByDescending(c => c.MonthlyAmount)
            .ToList();
    }

    private static IEnumerable<WeeklySpending> CalculateWeeklyConcentration(IEnumerable<Subscription> subscriptions)
    {
        var subscriptionsWithRenewal = subscriptions
            .Where(s => s.NextRenewalDate.HasValue)
            .ToList();

        if (!subscriptionsWithRenewal.Any())
        {
            return Enumerable.Range(1, 5).Select(w => new WeeklySpending
            {
                WeekOfMonth = w,
                RenewalCount = 0,
                TotalAmount = 0,
                IsHighConcentration = false
            });
        }

        var totalRenewals = subscriptionsWithRenewal.Count;
        
        var weeklyData = subscriptionsWithRenewal
            .GroupBy(s => GetWeekOfMonth(s.NextRenewalDate!.Value))
            .ToDictionary(
                g => g.Key,
                g => new { Count = g.Count(), Amount = g.Sum(s => s.Price) }
            );

        return Enumerable.Range(1, 5).Select(week =>
        {
            var data = weeklyData.TryGetValue(week, out var d) ? d : new { Count = 0, Amount = 0m };
            var concentrationRatio = totalRenewals > 0 ? (double)data.Count / totalRenewals : 0;
            
            return new WeeklySpending
            {
                WeekOfMonth = week,
                RenewalCount = data.Count,
                TotalAmount = data.Amount,
                IsHighConcentration = concentrationRatio > HighConcentrationThreshold
            };
        }).ToList();
    }

    private static IEnumerable<MonthlyRenewalSummary> CalculateMonthlySummaries(
        IEnumerable<RenewalEvent> events, 
        int monthsCount)
    {
        var groupedEvents = events
            .GroupBy(e => new { e.RenewalDate.Year, e.RenewalDate.Month })
            .ToDictionary(
                g => g.Key,
                g => new { Count = g.Count(), Total = g.Sum(e => e.Amount) }
            );

        var summaries = new List<MonthlyRenewalSummary>();
        var startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        for (int i = 0; i < monthsCount; i++)
        {
            var monthDate = startDate.AddMonths(i);
            var key = new { monthDate.Year, monthDate.Month };
            var data = groupedEvents.TryGetValue(key, out var d) ? d : new { Count = 0, Total = 0m };

            summaries.Add(new MonthlyRenewalSummary
            {
                Month = monthDate.ToString("MMMM yyyy"),
                YearMonth = monthDate.Year * 100 + monthDate.Month,
                RenewalCount = data.Count,
                TotalAmount = data.Total,
                IsHighSpend = false // Will be calculated below
            });
        }

        // Mark high-spend months (above average)
        var averageSpend = summaries.Count > 0 ? summaries.Average(s => s.TotalAmount) : 0;
        foreach (var summary in summaries.Where(s => s.TotalAmount > averageSpend))
        {
            summary.IsHighSpend = true;
        }

        return summaries;
    }

    private static int GetWeekOfMonth(DateTime date)
    {
        var day = date.Day;
        return day switch
        {
            <= 7 => 1,
            <= 14 => 2,
            <= 21 => 3,
            <= 28 => 4,
            _ => 5
        };
    }

    private static decimal NormalizeToMonthly(decimal price, BillingCycle billingCycle)
    {
        return billingCycle switch
        {
            BillingCycle.Annual => Math.Round(price / 12, 2),
            BillingCycle.Quarterly => Math.Round(price / 3, 2),
            BillingCycle.Weekly => Math.Round(price * 4.33m, 2),
            BillingCycle.Monthly => price,
            BillingCycle.Unknown => price, // Unknown defaults to monthly
            _ => price
        };
    }

    private static DateTime AdvanceRenewalDate(DateTime currentDate, BillingCycle billingCycle)
    {
        return billingCycle switch
        {
            BillingCycle.Weekly => currentDate.AddDays(7),
            BillingCycle.Monthly => currentDate.AddMonths(1),
            BillingCycle.Quarterly => currentDate.AddMonths(3),
            BillingCycle.Annual => currentDate.AddYears(1),
            BillingCycle.Unknown => currentDate.AddMonths(1), // Default to monthly for unknown
            _ => currentDate.AddMonths(1) // Default to monthly
        };
    }

    #endregion
}
