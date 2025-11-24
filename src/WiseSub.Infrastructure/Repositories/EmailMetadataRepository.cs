using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Data;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Repository for email metadata operations
/// </summary>
public class EmailMetadataRepository : Repository<EmailMetadata>, IEmailMetadataRepository
{
    public EmailMetadataRepository(WiseSubDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EmailMetadata>> GetUnprocessedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.EmailMetadata
            .Where(em => em.Status != EmailProcessingStatus.Completed)
            .OrderBy(em => em.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmailMetadata?> GetByExternalEmailIdAsync(
        string externalEmailId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EmailMetadata
            .FirstOrDefaultAsync(em => em.ExternalEmailId == externalEmailId, cancellationToken);
    }

    public async Task<int> GetProcessedCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EmailMetadata
            .CountAsync(em => em.Status == EmailProcessingStatus.Completed, cancellationToken);
    }

    public async Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EmailMetadata
            .CountAsync(em => em.Status != EmailProcessingStatus.Completed, cancellationToken);
    }

    public async Task BulkAddAsync(
        List<EmailMetadata> emailMetadataList,
        CancellationToken cancellationToken = default)
    {
        if (emailMetadataList == null || !emailMetadataList.Any())
            return;

        await _context.EmailMetadata.AddRangeAsync(emailMetadataList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingExternalProcessedIdsAsync(
        List<string> externalIds,
        CancellationToken cancellationToken = default)
    {
        if (externalIds == null || !externalIds.Any())
            return new HashSet<string>();

        var existingIds = await _context.EmailMetadata
            .Where(em => externalIds.Contains(em.ExternalEmailId) && 
                         em.Status == EmailProcessingStatus.Completed)
            .Select(em => em.ExternalEmailId)
            .ToListAsync(cancellationToken);

        return existingIds.ToHashSet();
    }

    public async Task<List<EmailMetadata>> GetUnprocessedByExternalIdsAsync(
        List<string> externalIds,
        CancellationToken cancellationToken = default)
    {
        if (externalIds == null || !externalIds.Any())
            return new List<EmailMetadata>();

        // Optimized query: Returns ONLY unprocessed emails
        // Filters: NOT Completed AND NOT Processing
        // Returns: Pending, Queued, Failed
        return await _context.EmailMetadata
            .Where(em => externalIds.Contains(em.ExternalEmailId))
            .Where(em => em.Status != EmailProcessingStatus.Completed && 
                         em.Status != EmailProcessingStatus.Processing)
            .ToListAsync(cancellationToken);
    }
}
