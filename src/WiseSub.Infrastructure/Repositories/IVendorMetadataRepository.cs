using WiseSub.Domain.Entities;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Repository interface for VendorMetadata entity operations with caching
/// </summary>
public interface IVendorMetadataRepository : IRepository<VendorMetadata>
{
    /// <summary>
    /// Gets vendor metadata by normalized name
    /// </summary>
    Task<VendorMetadata?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for vendor metadata by partial name match
    /// </summary>
    Task<IEnumerable<VendorMetadata>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets vendors by category
    /// </summary>
    Task<IEnumerable<VendorMetadata>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all vendors (for caching)
    /// </summary>
    Task<IEnumerable<VendorMetadata>> GetAllForCacheAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a vendor exists by normalized name
    /// </summary>
    Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates vendor metadata
    /// </summary>
    Task UpdateVendorAsync(VendorMetadata vendor, CancellationToken cancellationToken = default);
}
