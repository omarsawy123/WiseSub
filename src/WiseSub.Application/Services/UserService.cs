using System.Text.Json;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IAlertRepository _alertRepository;

    public UserService(
        IUserRepository userRepository,
        IEmailAccountRepository emailAccountRepository,
        ISubscriptionRepository subscriptionRepository,
        IAlertRepository alertRepository)
    {
        _userRepository = userRepository;
        _emailAccountRepository = emailAccountRepository;
        _subscriptionRepository = subscriptionRepository;
        _alertRepository = alertRepository;
    }

    public async Task<User> CreateUserAsync(string email, string name, string oauthProvider, string oauthSubjectId)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            Name = name,
            OAuthProvider = oauthProvider,
            OAuthSubjectId = oauthSubjectId,
            Tier = SubscriptionTier.Free,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            PreferencesJson = JsonSerializer.Serialize(new
            {
                EnableRenewalAlerts = true,
                EnablePriceChangeAlerts = true,
                EnableTrialEndingAlerts = true,
                EnableUnusedSubscriptionAlerts = true,
                UseDailyDigest = false,
                TimeZone = "UTC",
                PreferredCurrency = "USD"
            })
        };

        return await _userRepository.AddAsync(user);
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _userRepository.GetByEmailAsync(email);
    }

    public async Task<User?> GetUserByOAuthSubjectIdAsync(string oauthProvider, string oauthSubjectId)
    {
        return await _userRepository.GetByOAuthAsync(oauthProvider, oauthSubjectId);
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        await _userRepository.UpdateAsync(user);
        return user;
    }

    public async Task UpdateLastLoginAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }
    }

    public async Task<byte[]> ExportUserDataAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        // Load all related data
        var emailAccounts = await _emailAccountRepository.GetByUserIdAsync(userId);
        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId);
        var alerts = await _alertRepository.GetByUserIdAsync(userId);

        var exportData = new
        {
            User = new
            {
                user.Id,
                user.Email,
                user.Name,
                user.OAuthProvider,
                user.Tier,
                user.CreatedAt,
                user.LastLoginAt,
                user.PreferencesJson
            },
            EmailAccounts = emailAccounts.Select(ea => new
            {
                ea.Id,
                ea.EmailAddress,
                ea.Provider,
                ea.ConnectedAt,
                ea.LastScanAt,
                ea.IsActive
            }),
            Subscriptions = subscriptions.Select(s => new
            {
                s.Id,
                s.ServiceName,
                s.Price,
                s.Currency,
                s.BillingCycle,
                s.NextRenewalDate,
                s.Category,
                s.Status,
                s.CreatedAt,
                s.UpdatedAt
            }),
            Alerts = alerts.Select(a => new
            {
                a.Id,
                a.Type,
                a.Message,
                a.ScheduledFor,
                a.SentAt,
                a.Status
            }),
            ExportedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public async Task DeleteUserDataAsync(string userId)
    {
        // Use the repository's cascading delete method
        await _userRepository.DeleteUserDataAsync(userId);
    }
}
