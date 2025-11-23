using System.Text.Json;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
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

    public async Task<Result<User>> CreateUserAsync(string email, string name, string oauthProvider, string oauthSubjectId)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<User>(UserErrors.InvalidEmail);

        var existingUser = await _userRepository.GetByEmailAsync(email);
        if (existingUser != null)
            return Result.Failure<User>(UserErrors.AlreadyExists);

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

        var createdUser = await _userRepository.AddAsync(user);
        return Result.Success(createdUser);
    }

    public async Task<Result<User>> GetUserByIdAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return Result.Failure<User>(UserErrors.NotFound);

        return Result.Success(user);
    }

    public async Task<Result<User>> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<User>(UserErrors.InvalidEmail);

        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return Result.Failure<User>(UserErrors.NotFound);

        return Result.Success(user);
    }

    public async Task<Result<User>> GetUserByOAuthSubjectIdAsync(string oauthProvider, string oauthSubjectId)
    {
        var user = await _userRepository.GetByOAuthAsync(oauthProvider, oauthSubjectId);
        if (user == null)
            return Result.Failure<User>(UserErrors.NotFound);

        return Result.Success(user);
    }

    public async Task<Result<User>> UpdateUserAsync(User user)
    {
        if (user == null)
            return Result.Failure<User>(UserErrors.NotFound);

        var existingUser = await _userRepository.GetByIdAsync(user.Id);
        if (existingUser == null)
            return Result.Failure<User>(UserErrors.NotFound);

        await _userRepository.UpdateAsync(user);
        return Result.Success(user);
    }

    public async Task<Result> UpdateLastLoginAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return Result.Failure(UserErrors.NotFound);

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        return Result.Success();
    }

    public async Task<Result<byte[]>> ExportUserDataAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return Result.Failure<byte[]>(UserErrors.NotFound);

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

        return Result.Success(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public async Task<Result> DeleteUserDataAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return Result.Failure(UserErrors.NotFound);

        // Use the repository's cascading delete method
        await _userRepository.DeleteUserDataAsync(userId);
        return Result.Success();
    }
}
