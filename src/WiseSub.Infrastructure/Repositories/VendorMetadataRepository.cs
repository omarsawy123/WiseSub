using Microsoft.EntityFrameworkCore;
using WiseSub.Domain.Entities;
using WiseSub.Infrastructure.Data;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for VendorMetadata entity with caching support
/// </summary>
public class VendorMetadataRepository : Repository<VendorMetadata>, IVendorMetadataRepository
{
    public VendorMetadataRepository(WiseSubDbContext context) : base(context)
    {
    }

    public async Task<VendorMetadata?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(v => v.NormalizedName == normalizedName, cancellationToken);
    }

    public async Task<IEnumerable<VendorMetadata>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchTerm.ToLower();
        return await _dbSet
            .Where(v => v.NormalizedName.Contains(normalizedSearch) || v.Name.ToLower().Contains(normalizedSearch))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<VendorMetadata>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.Category == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<VendorMetadata>> GetAllForCacheAsync(CancellationToken cancellationToken = default)
    {
        // Returns all vendors for in-memory caching
        return await _dbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(v => v.NormalizedName == normalizedName, cancellationToken);
    }

    public async Task UpdateVendorAsync(VendorMetadata vendor, CancellationToken cancellationToken = default)
    {
        vendor.UpdatedAt = DateTime.UtcNow;
        await UpdateAsync(vendor, cancellationToken);
    }
}
