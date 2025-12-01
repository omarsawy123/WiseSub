using Hangfire;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;

namespace WiseSub.Infrastructure.BackgroundServices.Jobs;

/// <summary>
/// Background job that enriches vendor metadata with logos and website URLs.
/// Scheduled to run weekly to update vendor information.
/// </summary>
public class VendorEnrichmentJob
{
    private readonly ILogger<VendorEnrichmentJob> _logger;
    private readonly IVendorMetadataService _vendorMetadataService;
    private readonly IVendorMetadataRepository _vendorRepository;

    public VendorEnrichmentJob(
        ILogger<VendorEnrichmentJob> logger,
        IVendorMetadataService vendorMetadataService,
        IVendorMetadataRepository vendorRepository)
    {
        _logger = logger;
        _vendorMetadataService = vendorMetadataService;
        _vendorRepository = vendorRepository;
    }

    /// <summary>
    /// Enriches all vendors that are pending enrichment.
    /// Called by the scheduled job or can be triggered manually.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task EnrichPendingVendorsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting vendor enrichment job");

        try
        {
            // Get vendors from the enrichment queue
            var pendingVendorIds = VendorMetadataService.GetPendingEnrichmentVendors().ToList();
            
            if (pendingVendorIds.Count == 0)
            {
                _logger.LogDebug("No vendors pending enrichment");
                return;
            }

            _logger.LogInformation("Processing {Count} vendors for enrichment", pendingVendorIds.Count);

            var enrichedCount = 0;
            var failedCount = 0;

            foreach (var vendorId in pendingVendorIds)
            {
                try
                {
                    var result = await _vendorMetadataService.EnrichVendorAsync(vendorId, cancellationToken);
                    if (result.IsSuccess)
                    {
                        enrichedCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to enrich vendor {VendorId}: {Error}", 
                            vendorId, result.ErrorMessage);
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enriching vendor {VendorId}", vendorId);
                    failedCount++;
                }

                // Small delay to avoid overwhelming external services
                await Task.Delay(100, cancellationToken);
            }

            _logger.LogInformation(
                "Vendor enrichment completed. Enriched: {EnrichedCount}, Failed: {FailedCount}",
                enrichedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vendor enrichment job");
            throw;
        }
    }

    /// <summary>
    /// Enriches all vendors that are missing logo or website URL.
    /// Scheduled to run weekly.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task EnrichAllIncompleteVendorsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting full vendor enrichment scan");

        try
        {
            // Get all vendors that need enrichment
            var allVendors = await _vendorRepository.GetAllForCacheAsync(cancellationToken);
            var incompleteVendors = allVendors
                .Where(v => string.IsNullOrEmpty(v.LogoUrl) || string.IsNullOrEmpty(v.WebsiteUrl))
                .ToList();

            if (incompleteVendors.Count == 0)
            {
                _logger.LogDebug("All vendors have complete metadata");
                return;
            }

            _logger.LogInformation("Found {Count} vendors with incomplete metadata", incompleteVendors.Count);

            var enrichedCount = 0;

            foreach (var vendor in incompleteVendors)
            {
                try
                {
                    var result = await _vendorMetadataService.EnrichVendorAsync(vendor.Id, cancellationToken);
                    if (result.IsSuccess)
                    {
                        enrichedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enriching vendor {VendorId} ({VendorName})", 
                        vendor.Id, vendor.Name);
                }

                // Small delay to avoid overwhelming external services
                await Task.Delay(100, cancellationToken);
            }

            _logger.LogInformation("Full vendor enrichment completed. Enriched: {EnrichedCount}", enrichedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in full vendor enrichment job");
            throw;
        }
    }
}
