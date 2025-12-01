using FluentAssertions;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using Xunit;

namespace WiseSub.Application.Tests.Services;

/// <summary>
/// Unit tests for DashboardService
/// Tests cover Tasks 10.1-10.6 from tasks.md
/// </summary>
public class DashboardServiceTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IEmailAccountRepository> _emailAccountRepositoryMock;
    private readonly DashboardService _dashboardService;
    
    private readonly User _testUser;
    private readonly EmailAccount _testEmailAccount;

    public DashboardServiceTests()
    {
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _emailAccountRepositoryMock = new Mock<IEmailAccountRepository>();
        
        _dashboardService = new DashboardService(
            _subscriptionRepositoryMock.Object,
            _userRepositoryMock.Object,
            _emailAccountRepositoryMock.Object);

        _testUser = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            Name = "Test User",
            OAuthProvider = "Google",
            OAuthSubjectId = "oauth-123"
        };

        _testEmailAccount = new EmailAccount
        {
            Id = "email-123",
            UserId = _testUser.Id,
            EmailAddress = "test@gmail.com",
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = "encrypted",
            EncryptedRefreshToken = "encrypted"
        };
    }

    #region Task 10.1 - Active Subscription Display

    [Fact]
    public async Task GetDashboardDataAsync_WithActiveSubscriptions_ReturnsOnlyActiveSubscriptions()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Netflix", SubscriptionStatus.Active, 15.99m),
            CreateSubscription("Spotify", SubscriptionStatus.Active, 9.99m),
            CreateSubscription("Cancelled Service", SubscriptionStatus.Cancelled, 5.99m)
        };
        
        SetupMocks(subscriptions);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync(_testUser.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveSubscriptions.Should().HaveCount(2);
        result.Value.ActiveSubscriptions.Select(s => s.ServiceName)
            .Should().BeEquivalentTo(["Netflix", "Spotify"]);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsCorrectActiveSubscriptionCount()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Sub1", SubscriptionStatus.Active, 10m),
            CreateSubscription("Sub2", SubscriptionStatus.Active, 20m),
            CreateSubscription("Sub3", SubscriptionStatus.Cancelled, 15m),
            CreateSubscription("Sub4", SubscriptionStatus.Archived, 25m)
        };
        
        SetupMocks(subscriptions);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync(_testUser.Id);

        // Assert
        result.Value.ActiveSubscriptionCount.Should().Be(2);
        result.Value.TotalSubscriptionCount.Should().Be(4);
    }

    #endregion

    #region Task 10.2 - Category Grouping

    [Fact]
    public async Task GetDashboardDataAsync_GroupsSubscriptionsByCategory()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Netflix", SubscriptionStatus.Active, 15.99m, "Entertainment"),
            CreateSubscription("Spotify", SubscriptionStatus.Active, 9.99m, "Entertainment"),
            CreateSubscription("Dropbox", SubscriptionStatus.Active, 11.99m, "Productivity")
        };
        
        SetupMocks(subscriptions);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync(_testUser.Id);

        // Assert
        result.Value.SpendingByCategory.Should().ContainKey("Entertainment");
        result.Value.SpendingByCategory.Should().ContainKey("Productivity");
        result.Value.SpendingByCategory["Entertainment"].Should().Be(25.98m); // 15.99 + 9.99
        result.Value.SpendingByCategory["Productivity"].Should().Be(11.99m);
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_CalculatesCategoryPercentages()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Netflix", SubscriptionStatus.Active, 75m, "Entertainment"),
            CreateSubscription("Dropbox", SubscriptionStatus.Active, 25m, "Productivity")
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entertainment = result.Value.SpendingByCategory.First(c => c.Category == "Entertainment");
        var productivity = result.Value.SpendingByCategory.First(c => c.Category == "Productivity");
        
        entertainment.Percentage.Should().Be(75m);
        productivity.Percentage.Should().Be(25m);
    }

    [Fact]
    public async Task GetDashboardDataAsync_HandlesUncategorizedSubscriptions()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Mystery Service", SubscriptionStatus.Active, 10m, null)
        };
        
        SetupMocks(subscriptions);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync(_testUser.Id);

        // Assert
        result.Value.SpendingByCategory.Should().ContainKey("Uncategorized");
    }

    #endregion

    #region Task 10.3 - Upcoming Renewal Highlighting

    [Fact]
    public async Task GetDashboardDataAsync_HighlightsRenewalsWithin30Days()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Renews Soon", SubscriptionStatus.Active, 10m, nextRenewal: DateTime.UtcNow.AddDays(5)),
            CreateSubscription("Renews Later", SubscriptionStatus.Active, 10m, nextRenewal: DateTime.UtcNow.AddDays(45)),
            CreateSubscription("No Renewal Date", SubscriptionStatus.Active, 10m)
        };
        
        SetupMocks(subscriptions);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync(_testUser.Id);

        // Assert
        result.Value.UpcomingRenewals.Should().HaveCount(1);
        result.Value.UpcomingRenewals.First().ServiceName.Should().Be("Renews Soon");
    }

    [Fact]
    public async Task GetDashboardDataAsync_MarksSubscriptionsInDangerZone()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Urgent", SubscriptionStatus.Active, 10m, nextRenewal: DateTime.UtcNow.AddDays(3)),
            CreateSubscription("Not Urgent", SubscriptionStatus.Active, 10m, nextRenewal: DateTime.UtcNow.AddDays(15))
        };
        
        SetupMocks(subscriptions);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync(_testUser.Id);

        // Assert
        var urgent = result.Value.UpcomingRenewals.First(s => s.ServiceName == "Urgent");
        urgent.IsInDangerZone.Should().BeTrue();
        urgent.DaysUntilRenewal.Should().BeLessThanOrEqualTo(7);
    }

    #endregion

    #region Task 10.4 - Timeline Chronological Ordering

    [Fact]
    public async Task GetRenewalTimelineAsync_ReturnsEventsInChronologicalOrder()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Third", SubscriptionStatus.Active, 10m, nextRenewal: DateTime.UtcNow.AddDays(30)),
            CreateSubscription("First", SubscriptionStatus.Active, 10m, nextRenewal: DateTime.UtcNow.AddDays(5)),
            CreateSubscription("Second", SubscriptionStatus.Active, 10m, nextRenewal: DateTime.UtcNow.AddDays(15))
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetRenewalTimelineAsync(_testUser.Id, 12);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var events = result.Value.Events.Take(3).ToList();
        events[0].ServiceName.Should().Be("First");
        events[1].ServiceName.Should().Be("Second");
        events[2].ServiceName.Should().Be("Third");
    }

    [Fact]
    public async Task GetRenewalTimelineAsync_GeneratesRecurringRenewals()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Monthly", SubscriptionStatus.Active, 10m, 
                nextRenewal: DateTime.UtcNow.AddDays(5), 
                billingCycle: BillingCycle.Monthly)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetRenewalTimelineAsync(_testUser.Id, 3);

        // Assert
        // Should have at least 3 renewals for a monthly subscription over 3 months
        result.Value.Events.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    #endregion

    #region Task 10.5 - Total Monthly Cost Calculation

    [Fact]
    public async Task GetSpendingInsightsAsync_CalculatesTotalMonthlySpend()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Sub1", SubscriptionStatus.Active, 10m, billingCycle: BillingCycle.Monthly),
            CreateSubscription("Sub2", SubscriptionStatus.Active, 20m, billingCycle: BillingCycle.Monthly)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.Value.TotalMonthlySpend.Should().Be(30m);
        result.Value.ProjectedYearlySpend.Should().Be(360m);
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_NormalizesAnnualToMonthly()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Annual", SubscriptionStatus.Active, 120m, billingCycle: BillingCycle.Annual)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.Value.TotalMonthlySpend.Should().Be(10m); // 120 / 12
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_NormalizesQuarterlyToMonthly()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Quarterly", SubscriptionStatus.Active, 30m, billingCycle: BillingCycle.Quarterly)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.Value.TotalMonthlySpend.Should().Be(10m); // 30 / 3
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_NormalizesWeeklyToMonthly()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Weekly", SubscriptionStatus.Active, 10m, billingCycle: BillingCycle.Weekly)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.Value.TotalMonthlySpend.Should().Be(43.30m); // 10 * 4.33
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_HandlesUnknownBillingCycle()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Monthly", SubscriptionStatus.Active, 10m, billingCycle: BillingCycle.Monthly),
            CreateSubscription("Unknown", SubscriptionStatus.Active, 20m, billingCycle: BillingCycle.Unknown)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        // Unknown billing cycle defaults to monthly
        result.Value.TotalMonthlySpend.Should().Be(30m);
    }

    #endregion

    #region Task 10.6 - Category Percentage Accuracy

    [Fact]
    public async Task GetSpendingInsightsAsync_CategoryPercentagesSumTo100()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Entertainment1", SubscriptionStatus.Active, 50m, "Entertainment"),
            CreateSubscription("Productivity1", SubscriptionStatus.Active, 30m, "Productivity"),
            CreateSubscription("Other1", SubscriptionStatus.Active, 20m, "Other")
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        var totalPercentage = result.Value.SpendingByCategory.Sum(c => c.Percentage);
        totalPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_CountsSubscriptionsPerCategory()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("E1", SubscriptionStatus.Active, 10m, "Entertainment"),
            CreateSubscription("E2", SubscriptionStatus.Active, 10m, "Entertainment"),
            CreateSubscription("E3", SubscriptionStatus.Active, 10m, "Entertainment"),
            CreateSubscription("P1", SubscriptionStatus.Active, 10m, "Productivity")
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        var entertainment = result.Value.SpendingByCategory.First(c => c.Category == "Entertainment");
        var productivity = result.Value.SpendingByCategory.First(c => c.Category == "Productivity");
        
        entertainment.SubscriptionCount.Should().Be(3);
        productivity.SubscriptionCount.Should().Be(1);
    }

    #endregion

    #region Additional Coverage

    [Fact]
    public async Task GetDashboardDataAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync("nonexistent");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_NoSubscriptions_ReturnsEmptyInsights()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetByIdAsync(_testUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testUser);
        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAndStatusAsync(_testUser.Id, SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalMonthlySpend.Should().Be(0);
        result.Value.ActiveSubscriptionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_IdentifiesMostExpensiveSubscription()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Cheap", SubscriptionStatus.Active, 5m),
            CreateSubscription("Expensive", SubscriptionStatus.Active, 50m),
            CreateSubscription("Medium", SubscriptionStatus.Active, 25m)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.Value.MostExpensiveSubscription.Should().Be("Expensive");
        result.Value.MostExpensiveAmount.Should().Be(50m);
    }

    [Fact]
    public async Task GetSpendingInsightsAsync_CalculatesAveragePerSubscription()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Sub1", SubscriptionStatus.Active, 10m),
            CreateSubscription("Sub2", SubscriptionStatus.Active, 20m),
            CreateSubscription("Sub3", SubscriptionStatus.Active, 30m)
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetSpendingInsightsAsync(_testUser.Id);

        // Assert
        result.Value.AverageMonthlyPerSubscription.Should().Be(20m); // 60 / 3
    }

    [Fact]
    public async Task GetRenewalTimelineAsync_CalculatesMonthlySummaries()
    {
        // Arrange
        var nextMonth = DateTime.UtcNow.AddMonths(1);
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Sub1", SubscriptionStatus.Active, 10m, nextRenewal: nextMonth),
            CreateSubscription("Sub2", SubscriptionStatus.Active, 20m, nextRenewal: nextMonth.AddDays(5))
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetRenewalTimelineAsync(_testUser.Id, 3);

        // Assert
        result.Value.MonthlySummaries.Should().NotBeEmpty();
        result.Value.MonthsCovered.Should().Be(3);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsSubscriptionsRequiringReview()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Confident", SubscriptionStatus.Active, 10m),
            CreateSubscription("NeedsReview", SubscriptionStatus.PendingReview, 20m)
        };
        subscriptions[1].RequiresUserReview = true;
        
        SetupMocks(subscriptions);

        // Act
        var result = await _dashboardService.GetDashboardDataAsync(_testUser.Id);

        // Assert
        result.Value.RequiringReview.Should().HaveCount(1);
        result.Value.RequiringReview.First().ServiceName.Should().Be("NeedsReview");
    }

    [Fact]
    public async Task GetRenewalTimelineAsync_MarksHighValueRenewals()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("Expensive", SubscriptionStatus.Active, 100m, nextRenewal: DateTime.UtcNow.AddDays(10)),
            CreateSubscription("Cheap1", SubscriptionStatus.Active, 5m, nextRenewal: DateTime.UtcNow.AddDays(15)),
            CreateSubscription("Cheap2", SubscriptionStatus.Active, 5m, nextRenewal: DateTime.UtcNow.AddDays(20)),
            CreateSubscription("Cheap3", SubscriptionStatus.Active, 5m, nextRenewal: DateTime.UtcNow.AddDays(25)),
            CreateSubscription("Cheap4", SubscriptionStatus.Active, 5m, nextRenewal: DateTime.UtcNow.AddDays(30))
        };
        
        SetupMocksForInsights(subscriptions);

        // Act
        var result = await _dashboardService.GetRenewalTimelineAsync(_testUser.Id, 2);

        // Assert
        var expensiveEvent = result.Value.Events.FirstOrDefault(e => e.ServiceName == "Expensive");
        expensiveEvent.Should().NotBeNull();
        expensiveEvent!.IsHighValue.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private Subscription CreateSubscription(
        string name, 
        SubscriptionStatus status, 
        decimal price,
        string? category = null,
        DateTime? nextRenewal = null,
        BillingCycle billingCycle = BillingCycle.Monthly)
    {
        return new Subscription
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _testUser.Id,
            EmailAccountId = _testEmailAccount.Id,
            ServiceName = name,
            Price = price,
            Currency = "USD",
            BillingCycle = billingCycle,
            Status = status,
            Category = category,
            NextRenewalDate = nextRenewal
        };
    }

    private void SetupMocks(List<Subscription> subscriptions)
    {
        _userRepositoryMock.Setup(r => r.GetByIdAsync(_testUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testUser);
        
        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(_testUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);
        
        _emailAccountRepositoryMock.Setup(r => r.GetByUserIdAsync(_testUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailAccount> { _testEmailAccount });
    }

    private void SetupMocksForInsights(List<Subscription> subscriptions)
    {
        _userRepositoryMock.Setup(r => r.GetByIdAsync(_testUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testUser);
        
        var activeSubscriptions = subscriptions.Where(s => s.Status == SubscriptionStatus.Active).ToList();
        
        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAndStatusAsync(_testUser.Id, SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSubscriptions);
    }

    #endregion
}
