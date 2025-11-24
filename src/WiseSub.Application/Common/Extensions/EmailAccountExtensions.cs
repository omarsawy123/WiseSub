using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Extensions;

/// <summary>
/// Extension methods for EmailAccount entity
/// </summary>
public static class EmailAccountExtensions
{
    private const string GmailHistoryIdKey = "gmail_history_id";
    private const string OutlookDeltaTokenKey = "outlook_delta_token";

    /// <summary>
    /// Gets the provider-specific sync token for incremental syncing
    /// </summary>
    public static string? GetProviderSyncToken(this EmailAccount account)
    {
        return account.Provider switch
        {
            EmailProvider.Gmail => account.ProviderSyncMetadata.TryGetValue(GmailHistoryIdKey, out var token) 
                ? token 
                : null, // Fallback to legacy field
            EmailProvider.Outlook => account.ProviderSyncMetadata.TryGetValue(OutlookDeltaTokenKey, out var token) 
                ? token 
                : null,
            _ => null
        };
    }

    /// <summary>
    /// Sets the provider-specific sync token
    /// </summary>
    public static void SetProviderSyncToken(this EmailAccount account, string? token)
    {
        if (string.IsNullOrEmpty(token))
            return;

        var key = account.Provider switch
        {
            EmailProvider.Gmail => GmailHistoryIdKey,
            EmailProvider.Outlook => OutlookDeltaTokenKey,
            _ => null
        };

        if (key != null)
        {
            account.ProviderSyncMetadata[key] = token;
        }
    }

    /// <summary>
    /// Checks if the account supports incremental sync
    /// </summary>
    public static bool SupportsIncrementalSync(this EmailAccount account)
    {
        return account.LastScanAt > DateTime.MinValue 
            && !string.IsNullOrEmpty(account.GetProviderSyncToken());
    }
}
