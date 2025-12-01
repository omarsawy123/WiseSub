using CsCheck;
using FluentAssertions;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Tests.Services;

/// <summary>
/// Property-based tests for multi-account subscription management
/// Task 16.1 - Property 32: Unified subscription aggregation
/// Task 16.2 - Property 36: Subscription archival on disconnect
/// </summary>
public class MultiAccountPropertyTests
{
    private readonly Mock<ISubscriptionRepository> _mockSubscriptionRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IEmailAccountRepository> _mockEmailAccountRepository;
    private readonly DashboardService _dashboardService;
    private readonly SubscriptionService _subscriptionService;

    // Generators for test data
    private static readonly Gen<string> ServiceNameGen = Gen.String[5, 30];
    private static readonly Gen<decimal> PriceGen = Gen.Decimal[1m, 500m];
    private static readonly Gen<BillingCycle> BillingCycleGen = Gen.OneOfConst(
        BillingCycle.Monthly, BillingCycle.Annual, BillingCycle.Quarterly, BillingCycle.Weekly);
    private static readonly Gen<string> CategoryGen = Gen.OneOfConst(
        "Entertainment", "Productivity", "Utilities", "Software", "Gaming", "Music", "Video");
    private static readonly Gen<string> CurrencyGen = Gen.OneOfConst("USD", "EUR", "GBP");

    public MultiAccountPropertyTests()
    {
        _mockSubscriptionRepository = new Mock<ISubscriptionRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockEmailAccountRepository = new Mock<IEmailAccountRepository>();

        _dashboardService = new DashboardService(
            _mockSubscriptionRepository.Object,
            _mockUserRepository.Object,
            _mockEmailAccountRepository.Object);

        _subscriptionService = new SubscriptionService(_mockSubscriptionRepository.Object);
    }

    #region Property 32: Unified subscription aggregation

    /// <summary>
    /// Feature: subscription-tracker, Property 32: Unified subscription aggregation
    /// *For any* user with multiple connected email accounts, the dashboard SHALL display subscriptions from all accounts
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void Property32_DashboardAggregatesSubscriptionsFromAllEmailAccounts()
    {
        // Feature: subscription-tracker, Property 32: Unified subscription aggregation
        // For any user with multiple connected email accounts, the dashboard SHALL display subscriptions from all accounts
        // Validates: Requirements 7.1

        // Generate 2-5 email accounts with 1-10 subscriptions each
        var emailAccountCountGen = Gen.Int[2, 5];
        var subscriptionsPerAccountGen = Gen.Int[1, 10];

        Gen.Select(emailAccountCountGen, subscriptionsPerAccountGen)
            .Sample(tuple =>
            {
                var (emailAccountCount, subscriptionsPerAccount) = tuple;
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Email = "test@example.com", Tier = SubscriptionTier.Pro };

                // Create email accounts
                var emailAccounts = Enumerable.Range(0, emailAccountCount)
                    .Select(i => new EmailAccount
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        EmailAddress = $"account{i}@example.com",
                        Provider = EmailProvider.Gmail,
                        IsActive = true
                    })
                    .ToList();

                // Create subscriptions for each email account
                var allSubscriptions = new List<Subscription>();
                foreach (var emailAccount in emailAccounts)
                {
                    for (int i = 0; i < subscriptionsPerAccount; i++)
                    {
                        allSubscriptions.Add(new Subscription
                        {
                            Id = Guid.NewGuid().ToString(),
                            UserId = userId,
                            EmailAccountId = emailAccount.Id,
                            ServiceName = $"Service_{emailAccount.Id}_{i}",
                            Price = 10m + i,
                            Currency = "USD",
                            BillingCycle = BillingCycle.Monthly,
                            Category = "Entertainment",
                            Status = SubscriptionStatus.Active,
                            NextRenewalDate = DateTime.UtcNow.AddDays(15),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Setup mocks
                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                _mockEmailAccountRepository
                    .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(emailAccounts);

                _mockSubscriptionRepository
                    .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(allSubscriptions);

                // Act
                var result = _dashboardService.GetDashboardDataAsync(userId).GetAwaiter().GetResult();

                // Assert
                result.IsSuccess.Should().BeTrue("Dashboard data retrieval should succeed");
                var dashboardData = result.Value;

                // Verify all subscriptions from all email accounts are included
                var expectedTotalCount = emailAccountCount * subscriptionsPerAccount;
                dashboardData.ActiveSubscriptionCount.Should().Be(expectedTotalCount,
                    $"Dashboard should display all {expectedTotalCount} subscriptions from {emailAccountCount} email accounts");

                // Verify subscriptions from each email account are present
                var subscriptionsByAccount = dashboardData.ActiveSubscriptions
                    .GroupBy(s => s.EmailAccountId)
                    .ToDictionary(g => g.Key, g => g.Count());

                subscriptionsByAccount.Count.Should().Be(emailAccountCount,
                    "Subscriptions should be present from all email accounts");

                foreach (var emailAccount in emailAccounts)
                {
                    subscriptionsByAccount.Should().ContainKey(emailAccount.Id,
                        $"Subscriptions from email account {emailAccount.EmailAddress} should be included");
                    subscriptionsByAccount[emailAccount.Id].Should().Be(subscriptionsPerAccount,
                        $"All {subscriptionsPerAccount} subscriptions from {emailAccount.EmailAddress} should be present");
                }
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 32: Unified subscription aggregation
    /// *For any* user with subscriptions across multiple accounts, the total spending SHALL include all accounts
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void Property32_TotalSpendingIncludesAllEmailAccounts()
    {
        // Feature: subscription-tracker, Property 32: Unified subscription aggregation
        // For any user with subscriptions across multiple accounts, the total spending SHALL include all accounts
        // Validates: Requirements 7.1

        var emailAccountCountGen = Gen.Int[2, 4];

        Gen.Select(emailAccountCountGen, PriceGen, PriceGen)
            .Sample(tuple =>
            {
                var (emailAccountCount, price1, price2) = tuple;
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Email = "test@example.com", Tier = SubscriptionTier.Pro };

                // Create email accounts
                var emailAccounts = Enumerable.Range(0, emailAccountCount)
                    .Select(i => new EmailAccount
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        EmailAddress = $"account{i}@example.com",
                        Provider = EmailProvider.Gmail,
                        IsActive = true
                    })
                    .ToList();

                // Create one subscription per email account with known prices
                var allSubscriptions = emailAccounts.Select((ea, i) => new Subscription
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    EmailAccountId = ea.Id,
                    ServiceName = $"Service_{i}",
                    Price = i == 0 ? price1 : price2,
                    Currency = "USD",
                    BillingCycle = BillingCycle.Monthly,
                    Category = "Entertainment",
                    Status = SubscriptionStatus.Active,
                    NextRenewalDate = DateTime.UtcNow.AddDays(15),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                // Setup mocks
                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                _mockEmailAccountRepository
                    .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(emailAccounts);

                _mockSubscriptionRepository
                    .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(allSubscriptions);

                // Act
                var result = _dashboardService.GetDashboardDataAsync(userId).GetAwaiter().GetResult();

                // Assert
                result.IsSuccess.Should().BeTrue();
                var dashboardData = result.Value;

                // Calculate expected total (all subscriptions are monthly, so no normalization needed)
                var expectedTotal = allSubscriptions.Sum(s => s.Price);
                dashboardData.TotalMonthlySpend.Should().Be(expectedTotal,
                    "Total monthly spend should include subscriptions from all email accounts");
            }, iter: 100);
    }

    #endregion

    #region Property 36: Subscription archival on disconnect

    /// <summary>
    /// Feature: subscription-tracker, Property 36: Subscription archival on disconnect
    /// *For any* disconnected email account, all associated subscriptions SHALL have Status = Archived
    /// Validates: Requirements 7.5
    /// </summary>
    [Fact]
    public void Property36_DisconnectedAccountSubscriptionsAreArchived()
    {
        // Feature: subscription-tracker, Property 36: Subscription archival on disconnect
        // For any disconnected email account, all associated subscriptions SHALL have Status = Archived
        // Validates: Requirements 7.5

        var subscriptionCountGen = Gen.Int[1, 10];

        subscriptionCountGen.Sample(subscriptionCount =>
        {
            var userId = Guid.NewGuid().ToString();
            var emailAccountId = Guid.NewGuid().ToString();

            // Create subscriptions for the email account
            var subscriptions = Enumerable.Range(0, subscriptionCount)
                .Select(i => new Subscription
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    EmailAccountId = emailAccountId,
                    ServiceName = $"Service_{i}",
                    Price = 10m + i,
                    Currency = "USD",
                    BillingCycle = BillingCycle.Monthly,
                    Category = "Entertainment",
                    Status = SubscriptionStatus.Active,
                    NextRenewalDate = DateTime.UtcNow.AddDays(15),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            // Track archived subscriptions
            var archivedSubscriptions = new List<Subscription>();

            _mockSubscriptionRepository
                .Setup(r => r.ArchiveByEmailAccountAsync(emailAccountId, It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    // Simulate archiving all subscriptions
                    foreach (var sub in subscriptions)
                    {
                        sub.Status = SubscriptionStatus.Archived;
                        archivedSubscriptions.Add(sub);
                    }
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = _subscriptionService.ArchiveByEmailAccountAsync(emailAccountId).GetAwaiter().GetResult();

            // Assert
            result.IsSuccess.Should().BeTrue("Archiving subscriptions should succeed");

            // Verify all subscriptions were archived
            archivedSubscriptions.Count.Should().Be(subscriptionCount,
                $"All {subscriptionCount} subscriptions should be archived");

            foreach (var subscription in subscriptions)
            {
                subscription.Status.Should().Be(SubscriptionStatus.Archived,
                    $"Subscription {subscription.ServiceName} should be archived after email account disconnect");
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 36: Subscription archival on disconnect
    /// *For any* disconnected email account, historical data SHALL be retained
    /// Validates: Requirements 7.5
    /// </summary>
    [Fact]
    public void Property36_HistoricalDataRetainedAfterDisconnect()
    {
        // Feature: subscription-tracker, Property 36: Subscription archival on disconnect
        // For any disconnected email account, historical data SHALL be retained
        // Validates: Requirements 7.5

        var subscriptionCountGen = Gen.Int[1, 5];

        Gen.Select(subscriptionCountGen, ServiceNameGen, PriceGen, CategoryGen)
            .Sample(tuple =>
            {
                var (subscriptionCount, serviceName, price, category) = tuple;
                var userId = Guid.NewGuid().ToString();
                var emailAccountId = Guid.NewGuid().ToString();

                // Create subscriptions with history
                var subscriptions = Enumerable.Range(0, subscriptionCount)
                    .Select(i =>
                    {
                        var sub = new Subscription
                        {
                            Id = Guid.NewGuid().ToString(),
                            UserId = userId,
                            EmailAccountId = emailAccountId,
                            ServiceName = $"{serviceName}_{i}",
                            Price = price + i,
                            Currency = "USD",
                            BillingCycle = BillingCycle.Monthly,
                            Category = category,
                            Status = SubscriptionStatus.Active,
                            NextRenewalDate = DateTime.UtcNow.AddDays(15),
                            CreatedAt = DateTime.UtcNow.AddMonths(-3),
                            UpdatedAt = DateTime.UtcNow
                        };

                        // Add some history
                        sub.History.Add(new SubscriptionHistory
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = sub.Id,
                            ChangeType = "Created",
                            OldValue = string.Empty,
                            NewValue = $"Service: {sub.ServiceName}",
                            ChangedAt = sub.CreatedAt
                        });

                        return sub;
                    })
                    .ToList();

                // Store original data for comparison
                var originalData = subscriptions.Select(s => new
                {
                    s.Id,
                    s.ServiceName,
                    s.Price,
                    s.Category,
                    s.CreatedAt,
                    HistoryCount = s.History.Count
                }).ToList();

                _mockSubscriptionRepository
                    .Setup(r => r.ArchiveByEmailAccountAsync(emailAccountId, It.IsAny<CancellationToken>()))
                    .Callback(() =>
                    {
                        // Simulate archiving - only status changes, data is retained
                        foreach (var sub in subscriptions)
                        {
                            sub.Status = SubscriptionStatus.Archived;
                            sub.UpdatedAt = DateTime.UtcNow;
                        }
                    })
                    .Returns(Task.CompletedTask);

                // Act
                var result = _subscriptionService.ArchiveByEmailAccountAsync(emailAccountId).GetAwaiter().GetResult();

                // Assert
                result.IsSuccess.Should().BeTrue();

                // Verify historical data is retained
                for (int i = 0; i < subscriptions.Count; i++)
                {
                    var subscription = subscriptions[i];
                    var original = originalData[i];

                    subscription.Id.Should().Be(original.Id, "Subscription ID should be retained");
                    subscription.ServiceName.Should().Be(original.ServiceName, "Service name should be retained");
                    subscription.Price.Should().Be(original.Price, "Price should be retained");
                    subscription.Category.Should().Be(original.Category, "Category should be retained");
                    subscription.CreatedAt.Should().Be(original.CreatedAt, "Created date should be retained");
                    subscription.History.Count.Should().Be(original.HistoryCount, "History should be retained");
                    subscription.Status.Should().Be(SubscriptionStatus.Archived, "Status should be Archived");
                }
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 36: Subscription archival on disconnect
    /// *For any* user with multiple email accounts, disconnecting one SHALL NOT affect subscriptions from other accounts
    /// Validates: Requirements 7.5
    /// </summary>
    [Fact]
    public void Property36_DisconnectingOneAccountDoesNotAffectOthers()
    {
        // Feature: subscription-tracker, Property 36: Subscription archival on disconnect
        // For any user with multiple email accounts, disconnecting one SHALL NOT affect subscriptions from other accounts
        // Validates: Requirements 7.5

        var subscriptionsPerAccountGen = Gen.Int[1, 5];

        subscriptionsPerAccountGen.Sample(subscriptionsPerAccount =>
        {
            var userId = Guid.NewGuid().ToString();
            var emailAccount1Id = Guid.NewGuid().ToString();
            var emailAccount2Id = Guid.NewGuid().ToString();

            // Create subscriptions for account 1 (will be disconnected)
            var account1Subscriptions = Enumerable.Range(0, subscriptionsPerAccount)
                .Select(i => new Subscription
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    EmailAccountId = emailAccount1Id,
                    ServiceName = $"Account1_Service_{i}",
                    Price = 10m + i,
                    Currency = "USD",
                    BillingCycle = BillingCycle.Monthly,
                    Status = SubscriptionStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            // Create subscriptions for account 2 (should remain unaffected)
            var account2Subscriptions = Enumerable.Range(0, subscriptionsPerAccount)
                .Select(i => new Subscription
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    EmailAccountId = emailAccount2Id,
                    ServiceName = $"Account2_Service_{i}",
                    Price = 20m + i,
                    Currency = "USD",
                    BillingCycle = BillingCycle.Monthly,
                    Status = SubscriptionStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            // Store original status of account 2 subscriptions
            var account2OriginalStatuses = account2Subscriptions
                .ToDictionary(s => s.Id, s => s.Status);

            _mockSubscriptionRepository
                .Setup(r => r.ArchiveByEmailAccountAsync(emailAccount1Id, It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    // Only archive account 1 subscriptions
                    foreach (var sub in account1Subscriptions)
                    {
                        sub.Status = SubscriptionStatus.Archived;
                    }
                    // Account 2 subscriptions should NOT be modified
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = _subscriptionService.ArchiveByEmailAccountAsync(emailAccount1Id).GetAwaiter().GetResult();

            // Assert
            result.IsSuccess.Should().BeTrue();

            // Verify account 1 subscriptions are archived
            foreach (var subscription in account1Subscriptions)
            {
                subscription.Status.Should().Be(SubscriptionStatus.Archived,
                    $"Account 1 subscription {subscription.ServiceName} should be archived");
            }

            // Verify account 2 subscriptions are NOT affected
            foreach (var subscription in account2Subscriptions)
            {
                subscription.Status.Should().Be(account2OriginalStatuses[subscription.Id],
                    $"Account 2 subscription {subscription.ServiceName} should remain {account2OriginalStatuses[subscription.Id]}");
            }
        }, iter: 100);
    }

    #endregion
}
