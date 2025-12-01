using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service interface for vendor metadata operations including matching, enrichment, and caching
/// </summary>
public interface IVendorMetadataService
{
    /// <summary>
    /// Matches a service name to vendor metadata using normalized name comparison
    /// </summary>
    /// <param name="serviceName">The service name to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matched vendor metadata or null if not found</returns>
    Task<Result<VendorMetadata?>> MatchVendorAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets vendor metadata by ID
    /// </summary>
    Task<Result<VendorMetadata>> GetByIdAsync(string vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all vendors for a specific category
    /// </summary>
    Task<Result<IEnumerable<VendorMetadata>>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches vendors by name (partial match)
    /// </summary>
    Task<Result<IEnumerable<VendorMetadata>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new vendor metadata record
    /// </summary>
    Task<Result<VendorMetadata>> CreateVendorAsync(
        string name,
        string category,
        string? logoUrl = null,
        string? websiteUrl = null,
        string? accountManagementUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates vendor metadata with enriched information
    /// </summary>
    Task<Result> UpdateVendorAsync(
        string vendorId,
        string? logoUrl = null,
        string? websiteUrl = null,
        string? accountManagementUrl = null,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates vendor metadata from a service name if it doesn't exist (fallback logic)
    /// </summary>
    Task<Result<VendorMetadata>> GetOrCreateFromServiceNameAsync(
        string serviceName,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a vendor for background enrichment
    /// </summary>
    Task<Result> QueueForEnrichmentAsync(string vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enriches vendor metadata with logo and website URL (called by background job)
    /// </summary>
    Task<Result> EnrichVendorAsync(string vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalizes a service name for matching
    /// </summary>
    string NormalizeName(string serviceName);
}
