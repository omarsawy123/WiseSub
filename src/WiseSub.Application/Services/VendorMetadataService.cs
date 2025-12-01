using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

namespace WiseSub.Application.Services;

/// <summary>
/// Service for vendor metadata operations including matching, enrichment, and caching
/// </summary>
public partial class VendorMetadataService : IVendorMetadataService
{
    private readonly IVendorMetadataRepository _vendorRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VendorMetadataService> _logger;

    private const string AllVendorsCacheKey = "AllVendors";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    // Queue for vendors pending enrichment (in-memory for MVP)
    private static readonly HashSet<string> _enrichmentQueue = new();
    private static readonly object _queueLock = new();

    public VendorMetadataService(
        IVendorMetadataRepository vendorRepository,
        IMemoryCache cache,
        ILogger<VendorMetadataService> logger)
    {
        _vendorRepository = vendorRepository;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<VendorMetadata?>> MatchVendorAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Result.Success<VendorMetadata?>(null);
        }

        var normalizedName = NormalizeName(serviceName);
        
        _logger.LogDebug("Matching vendor for service name: {ServiceName} (normalized: {NormalizedName})",
            serviceName, normalizedName);

        // Try exact match first
        var vendor = await _vendorRepository.GetByNormalizedNameAsync(normalizedName, cancellationToken);
        if (vendor != null)
        {
            _logger.LogDebug("Found exact match for vendor: {VendorName}", vendor.Name);
            return Result.Success<VendorMetadata?>(vendor);
        }

        // Try fuzzy match using cached vendors
        var allVendors = await GetCachedVendorsAsync(cancellationToken);
        var bestMatch = FindBestMatch(normalizedName, allVendors);
        
        if (bestMatch != null)
        {
            _logger.LogDebug("Found fuzzy match for vendor: {VendorName} (similarity threshold met)", bestMatch.Name);
            return Result.Success<VendorMetadata?>(bestMatch);
        }

        _logger.LogDebug("No vendor match found for: {ServiceName}", serviceName);
        return Result.Success<VendorMetadata?>(null);
    }

    /// <inheritdoc />
    public async Task<Result<VendorMetadata>> GetByIdAsync(
        string vendorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorId))
        {
            return Result.Failure<VendorMetadata>(VendorErrors.NotFound);
        }

        var vendor = await _vendorRepository.GetByIdAsync(vendorId, cancellationToken);
        if (vendor == null)
        {
            return Result.Failure<VendorMetadata>(VendorErrors.NotFound);
        }

        return Result.Success(vendor);
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<VendorMetadata>>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Result.Success(Enumerable.Empty<VendorMetadata>());
        }

        var vendors = await _vendorRepository.GetByCategoryAsync(category, cancellationToken);
        return Result.Success(vendors);
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<VendorMetadata>>> SearchAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Result.Success(Enumerable.Empty<VendorMetadata>());
        }

        var vendors = await _vendorRepository.SearchByNameAsync(searchTerm, cancellationToken);
        return Result.Success(vendors);
    }

    /// <inheritdoc />
    public async Task<Result<VendorMetadata>> CreateVendorAsync(
        string name,
        string category,
        string? logoUrl = null,
        string? websiteUrl = null,
        string? accountManagementUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<VendorMetadata>(VendorErrors.InvalidName);
        }

        var normalizedName = NormalizeName(name);

        // Check if vendor already exists
        if (await _vendorRepository.ExistsByNameAsync(normalizedName, cancellationToken))
        {
            return Result.Failure<VendorMetadata>(VendorErrors.AlreadyExists);
        }

        var vendor = new VendorMetadata
        {
            Name = name.Trim(),
            NormalizedName = normalizedName,
            Category = category ?? "Other",
            LogoUrl = logoUrl,
            WebsiteUrl = websiteUrl,
            AccountManagementUrl = accountManagementUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _vendorRepository.AddAsync(vendor, cancellationToken);
        
        // Invalidate cache
        InvalidateCache();
        
        _logger.LogInformation("Created new vendor: {VendorName} (ID: {VendorId})", created.Name, created.Id);

        return Result.Success(created);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateVendorAsync(
        string vendorId,
        string? logoUrl = null,
        string? websiteUrl = null,
        string? accountManagementUrl = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var vendor = await _vendorRepository.GetByIdAsync(vendorId, cancellationToken);
        if (vendor == null)
        {
            return Result.Failure(VendorErrors.NotFound);
        }

        var updated = false;

        if (logoUrl != null && vendor.LogoUrl != logoUrl)
        {
            vendor.LogoUrl = logoUrl;
            updated = true;
        }

        if (websiteUrl != null && vendor.WebsiteUrl != websiteUrl)
        {
            vendor.WebsiteUrl = websiteUrl;
            updated = true;
        }

        if (accountManagementUrl != null && vendor.AccountManagementUrl != accountManagementUrl)
        {
            vendor.AccountManagementUrl = accountManagementUrl;
            updated = true;
        }

        if (category != null && vendor.Category != category)
        {
            vendor.Category = category;
            updated = true;
        }

        if (updated)
        {
            await _vendorRepository.UpdateVendorAsync(vendor, cancellationToken);
            InvalidateCache();
            _logger.LogInformation("Updated vendor: {VendorName} (ID: {VendorId})", vendor.Name, vendor.Id);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<VendorMetadata>> GetOrCreateFromServiceNameAsync(
        string serviceName,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Result.Failure<VendorMetadata>(VendorErrors.InvalidName);
        }

        // Try to match existing vendor first
        var matchResult = await MatchVendorAsync(serviceName, cancellationToken);
        if (matchResult.IsFailure)
        {
            // MatchVendorAsync only fails on unexpected errors, propagate as general error
            return Result.Failure<VendorMetadata>(GeneralErrors.UnexpectedError);
        }

        if (matchResult.Value != null)
        {
            return Result.Success(matchResult.Value);
        }

        // Create new vendor from service name (fallback logic)
        _logger.LogInformation("Creating fallback vendor for unknown service: {ServiceName}", serviceName);
        
        var createResult = await CreateVendorAsync(
            serviceName,
            category ?? "Other",
            cancellationToken: cancellationToken);

        if (createResult.IsSuccess)
        {
            // Queue for background enrichment
            await QueueForEnrichmentAsync(createResult.Value.Id, cancellationToken);
        }

        return createResult;
    }

    /// <inheritdoc />
    public Task<Result> QueueForEnrichmentAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorId))
        {
            return Task.FromResult(Result.Failure(VendorErrors.NotFound));
        }

        lock (_queueLock)
        {
            _enrichmentQueue.Add(vendorId);
        }

        _logger.LogDebug("Queued vendor for enrichment: {VendorId}", vendorId);
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Gets vendors pending enrichment (for background job)
    /// </summary>
    public static IEnumerable<string> GetPendingEnrichmentVendors()
    {
        lock (_queueLock)
        {
            var vendors = _enrichmentQueue.ToList();
            _enrichmentQueue.Clear();
            return vendors;
        }
    }

    /// <inheritdoc />
    public async Task<Result> EnrichVendorAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        var vendor = await _vendorRepository.GetByIdAsync(vendorId, cancellationToken);
        if (vendor == null)
        {
            return Result.Failure(VendorErrors.NotFound);
        }

        _logger.LogInformation("Enriching vendor: {VendorName} (ID: {VendorId})", vendor.Name, vendor.Id);

        // Try to enrich with publicly available information
        var enriched = false;

        // Generate website URL from vendor name if not present
        if (string.IsNullOrEmpty(vendor.WebsiteUrl))
        {
            var guessedUrl = GuessWebsiteUrl(vendor.Name);
            if (!string.IsNullOrEmpty(guessedUrl))
            {
                vendor.WebsiteUrl = guessedUrl;
                enriched = true;
            }
        }

        // Generate logo URL using common favicon services if not present
        if (string.IsNullOrEmpty(vendor.LogoUrl) && !string.IsNullOrEmpty(vendor.WebsiteUrl))
        {
            vendor.LogoUrl = GenerateFaviconUrl(vendor.WebsiteUrl);
            enriched = true;
        }

        if (enriched)
        {
            await _vendorRepository.UpdateVendorAsync(vendor, cancellationToken);
            InvalidateCache();
            _logger.LogInformation("Enriched vendor with logo and website: {VendorName}", vendor.Name);
        }
        else
        {
            _logger.LogDebug("No enrichment data found for vendor: {VendorName}", vendor.Name);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public string NormalizeName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return string.Empty;
        }

        // Convert to lowercase
        var normalized = serviceName.ToLowerInvariant();

        // Remove common suffixes
        normalized = RemoveCommonSuffixes(normalized);

        // Remove special characters and extra spaces
        normalized = SpecialCharsRegex().Replace(normalized, " ");
        normalized = MultipleSpacesRegex().Replace(normalized, " ");

        return normalized.Trim();
    }

    #region Private Methods

    private async Task<IEnumerable<VendorMetadata>> GetCachedVendorsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(AllVendorsCacheKey, out IEnumerable<VendorMetadata>? cachedVendors) && cachedVendors != null)
        {
            return cachedVendors;
        }

        var vendors = await _vendorRepository.GetAllForCacheAsync(cancellationToken);
        var vendorList = vendors.ToList();

        _cache.Set(AllVendorsCacheKey, vendorList, CacheDuration);
        _logger.LogDebug("Cached {Count} vendors", vendorList.Count);

        return vendorList;
    }

    private void InvalidateCache()
    {
        _cache.Remove(AllVendorsCacheKey);
        _logger.LogDebug("Vendor cache invalidated");
    }

    private VendorMetadata? FindBestMatch(string normalizedName, IEnumerable<VendorMetadata> vendors)
    {
        const double similarityThreshold = 0.85;
        VendorMetadata? bestMatch = null;
        double bestSimilarity = 0;

        foreach (var vendor in vendors)
        {
            var similarity = CalculateSimilarity(normalizedName, vendor.NormalizedName);
            if (similarity >= similarityThreshold && similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestMatch = vendor;
            }
        }

        return bestMatch;
    }

    private static double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            return 0;
        }

        if (source == target)
        {
            return 1.0;
        }

        // Check if one contains the other
        if (source.Contains(target) || target.Contains(source))
        {
            var shorter = source.Length < target.Length ? source : target;
            var longer = source.Length >= target.Length ? source : target;
            return (double)shorter.Length / longer.Length;
        }

        // Levenshtein distance-based similarity
        var distance = LevenshteinDistance(source, target);
        var maxLength = Math.Max(source.Length, target.Length);
        return 1.0 - ((double)distance / maxLength);
    }

    private static int LevenshteinDistance(string source, string target)
    {
        var sourceLength = source.Length;
        var targetLength = target.Length;

        var matrix = new int[sourceLength + 1, targetLength + 1];

        for (var i = 0; i <= sourceLength; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= targetLength; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[sourceLength, targetLength];
    }

    private static string RemoveCommonSuffixes(string name)
    {
        var suffixes = new[] { " inc", " inc.", " llc", " ltd", " ltd.", " corp", " corp.", " co", " co." };
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
            }
        }
        return name;
    }

    private static string? GuessWebsiteUrl(string vendorName)
    {
        // Common vendor name to domain mappings
        var knownMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "netflix", "https://www.netflix.com" },
            { "spotify", "https://www.spotify.com" },
            { "amazon prime", "https://www.amazon.com/prime" },
            { "disney+", "https://www.disneyplus.com" },
            { "disney plus", "https://www.disneyplus.com" },
            { "hulu", "https://www.hulu.com" },
            { "hbo max", "https://www.max.com" },
            { "apple music", "https://music.apple.com" },
            { "apple tv+", "https://tv.apple.com" },
            { "youtube premium", "https://www.youtube.com/premium" },
            { "adobe", "https://www.adobe.com" },
            { "microsoft 365", "https://www.microsoft.com/microsoft-365" },
            { "office 365", "https://www.microsoft.com/microsoft-365" },
            { "dropbox", "https://www.dropbox.com" },
            { "google one", "https://one.google.com" },
            { "icloud", "https://www.icloud.com" },
            { "slack", "https://slack.com" },
            { "zoom", "https://zoom.us" },
            { "notion", "https://www.notion.so" },
            { "figma", "https://www.figma.com" },
            { "canva", "https://www.canva.com" },
            { "github", "https://github.com" },
            { "linkedin premium", "https://www.linkedin.com/premium" },
            { "grammarly", "https://www.grammarly.com" },
            { "nordvpn", "https://nordvpn.com" },
            { "expressvpn", "https://www.expressvpn.com" },
            { "1password", "https://1password.com" },
            { "lastpass", "https://www.lastpass.com" },
            { "audible", "https://www.audible.com" },
            { "kindle unlimited", "https://www.amazon.com/kindle-unlimited" },
            { "paramount+", "https://www.paramountplus.com" },
            { "peacock", "https://www.peacocktv.com" },
            { "crunchyroll", "https://www.crunchyroll.com" }
        };

        // Check known mappings
        var normalizedName = vendorName.ToLowerInvariant().Trim();
        if (knownMappings.TryGetValue(normalizedName, out var url))
        {
            return url;
        }

        // Try to generate URL from name (simple heuristic)
        var cleanName = Regex.Replace(normalizedName, @"[^a-z0-9]", "");
        if (!string.IsNullOrEmpty(cleanName) && cleanName.Length >= 3)
        {
            return $"https://www.{cleanName}.com";
        }

        return null;
    }

    private static string GenerateFaviconUrl(string websiteUrl)
    {
        // Use Google's favicon service as a fallback
        try
        {
            var uri = new Uri(websiteUrl);
            return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64";
        }
        catch
        {
            return string.Empty;
        }
    }

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex SpecialCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();

    #endregion
}
