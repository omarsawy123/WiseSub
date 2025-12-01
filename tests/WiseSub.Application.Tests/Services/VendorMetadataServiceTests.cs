using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;
using WiseSub.Domain.Entities;

namespace WiseSub.Application.Tests.Services;

/// <summary>
/// Property tests for VendorMetadataService
/// Covers Task 13.1 (vendor metadata matching) and Task 13.2 (fallback to service name)
/// </summary>
public class VendorMetadataServiceTests
{
    private readonly Mock<IVendorMetadataRepository> _mockRepository;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<VendorMetadataService>> _mockLogger;
    private readonly VendorMetadataService _service;

    public VendorMetadataServiceTests()
    {
        _mockRepository = new Mock<IVendorMetadataRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<VendorMetadataService>>();
        _service = new VendorMetadataService(
            _mockRepository.Object,
            _cache,
            _mockLogger.Object);
    }

    #region Task 13.1 - Property 44: Vendor Metadata Matching

    /// <summary>
    /// Feature: subscription-tracker, Property 44: Vendor metadata matching
    /// For any identified service name, the system SHALL attempt to match it against the VendorMetadata database
    /// Validates: Requirements 10.1
    /// </summary>
    [Theory]
    [InlineData("Netflix", "netflix")]
    [InlineData("NETFLIX", "netflix")]
    [InlineData("Netflix Inc", "netflix")]
    [InlineData("Netflix Inc.", "netflix")]
    [InlineData("Spotify", "spotify")]
    [InlineData("SPOTIFY PREMIUM", "spotify premium")]
    [InlineData("Adobe Creative Cloud", "adobe creative cloud")]
    public async Task MatchVendorAsync_NormalizesServiceName_AndMatchesAgainstDatabase(
        string inputServiceName, string expectedNormalizedName)
    {
        // Arrange
        var vendor = new VendorMetadata
        {
            Id = Guid.NewGuid().ToString(),
            Name = inputServiceName,
            NormalizedName = expectedNormalizedName,
            Category = "Entertainment"
        };

        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync(expectedNormalizedName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        // Act
        var result = await _service.MatchVendorAsync(inputServiceName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.NormalizedName.Should().Be(expectedNormalizedName);
        
        // Verify the repository was called with normalized name
        _mockRepository.Verify(
            r => r.GetByNormalizedNameAsync(expectedNormalizedName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 44: Vendor metadata matching
    /// Verifies exact match takes precedence over fuzzy match
    /// Validates: Requirements 10.1
    /// </summary>
    [Fact]
    public async Task MatchVendorAsync_ExactMatch_ReturnedImmediately()
    {
        // Arrange
        var exactMatchVendor = new VendorMetadata
        {
            Id = "vendor-1",
            Name = "Netflix",
            NormalizedName = "netflix",
            Category = "Entertainment",
            LogoUrl = "https://netflix.com/logo.png"
        };

        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync("netflix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(exactMatchVendor);

        // Act
        var result = await _service.MatchVendorAsync("Netflix");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be("vendor-1");
        result.Value.Name.Should().Be("Netflix");
        
        // Should not need to fetch all vendors for fuzzy matching
        _mockRepository.Verify(
            r => r.GetAllForCacheAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 44: Vendor metadata matching
    /// Verifies fuzzy matching works when exact match not found (85% similarity threshold)
    /// Validates: Requirements 10.1
    /// </summary>
    [Theory]
    [InlineData("Netflx", "netflix")] // Typo - should match
    [InlineData("Netflixx", "netflix")] // Extra character - should match
    [InlineData("Spotfy", "spotify")] // Missing character - should match
    public async Task MatchVendorAsync_FuzzyMatch_ReturnsVendorAboveThreshold(
        string inputServiceName, string expectedMatch)
    {
        // Arrange
        var vendors = new List<VendorMetadata>
        {
            new() { Id = "v1", Name = "Netflix", NormalizedName = "netflix", Category = "Entertainment" },
            new() { Id = "v2", Name = "Spotify", NormalizedName = "spotify", Category = "Music" },
            new() { Id = "v3", Name = "Amazon Prime", NormalizedName = "amazon prime", Category = "Entertainment" }
        };

        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorMetadata?)null);

        _mockRepository
            .Setup(r => r.GetAllForCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendors);

        // Act
        var result = await _service.MatchVendorAsync(inputServiceName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.NormalizedName.Should().Be(expectedMatch);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 44: Vendor metadata matching
    /// Verifies no match returned when similarity is below threshold
    /// Validates: Requirements 10.1
    /// </summary>
    [Theory]
    [InlineData("XYZ Service")] // Completely different
    [InlineData("Random App")] // No match
    [InlineData("ABC")] // Too short to match anything
    public async Task MatchVendorAsync_NoMatch_ReturnsNull(string inputServiceName)
    {
        // Arrange
        var vendors = new List<VendorMetadata>
        {
            new() { Id = "v1", Name = "Netflix", NormalizedName = "netflix", Category = "Entertainment" },
            new() { Id = "v2", Name = "Spotify", NormalizedName = "spotify", Category = "Music" }
        };

        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorMetadata?)null);

        _mockRepository
            .Setup(r => r.GetAllForCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendors);

        // Act
        var result = await _service.MatchVendorAsync(inputServiceName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 44: Vendor metadata matching
    /// Verifies empty/null service names are handled gracefully
    /// Validates: Requirements 10.1
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MatchVendorAsync_EmptyOrNullServiceName_ReturnsNull(string? inputServiceName)
    {
        // Act
        var result = await _service.MatchVendorAsync(inputServiceName!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        
        // Should not call repository for empty input
        _mockRepository.Verify(
            r => r.GetByNormalizedNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion


    #region Task 13.2 - Property 46: Fallback to Service Name

    /// <summary>
    /// Feature: subscription-tracker, Property 46: Fallback to service name
    /// When VM is unavailable for a service, the SMS SHALL use the extracted service name
    /// Validates: Requirements 10.3
    /// </summary>
    [Theory]
    [InlineData("New Streaming Service")]
    [InlineData("Unknown App Pro")]
    [InlineData("My Custom Subscription")]
    public async Task GetOrCreateFromServiceNameAsync_NoExistingVendor_CreatesNewVendorFromServiceName(
        string serviceName)
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorMetadata?)null);

        _mockRepository
            .Setup(r => r.GetAllForCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<VendorMetadata>());

        _mockRepository
            .Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        VendorMetadata? capturedVendor = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<VendorMetadata>(), It.IsAny<CancellationToken>()))
            .Callback<VendorMetadata, CancellationToken>((v, _) => capturedVendor = v)
            .ReturnsAsync((VendorMetadata v, CancellationToken _) => v);

        // Act
        var result = await _service.GetOrCreateFromServiceNameAsync(serviceName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(serviceName.Trim());
        
        // Verify a new vendor was created with the service name
        capturedVendor.Should().NotBeNull();
        capturedVendor!.Name.Should().Be(serviceName.Trim());
        capturedVendor.Category.Should().Be("Other"); // Default category
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 46: Fallback to service name
    /// Verifies that existing vendor is returned when match is found
    /// Validates: Requirements 10.3
    /// </summary>
    [Fact]
    public async Task GetOrCreateFromServiceNameAsync_ExistingVendor_ReturnsExistingVendor()
    {
        // Arrange
        var existingVendor = new VendorMetadata
        {
            Id = "existing-vendor-id",
            Name = "Netflix",
            NormalizedName = "netflix",
            Category = "Entertainment",
            LogoUrl = "https://netflix.com/logo.png",
            WebsiteUrl = "https://www.netflix.com"
        };

        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync("netflix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVendor);

        // Act
        var result = await _service.GetOrCreateFromServiceNameAsync("Netflix");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be("existing-vendor-id");
        result.Value.LogoUrl.Should().Be("https://netflix.com/logo.png");
        
        // Should not create a new vendor
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<VendorMetadata>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 46: Fallback to service name
    /// Verifies that category is preserved when provided
    /// Validates: Requirements 10.3
    /// </summary>
    [Theory]
    [InlineData("New Music App", "Music")]
    [InlineData("Productivity Tool", "Productivity")]
    [InlineData("Cloud Storage Service", "Cloud Storage")]
    public async Task GetOrCreateFromServiceNameAsync_WithCategory_PreservesCategory(
        string serviceName, string category)
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorMetadata?)null);

        _mockRepository
            .Setup(r => r.GetAllForCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<VendorMetadata>());

        _mockRepository
            .Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        VendorMetadata? capturedVendor = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<VendorMetadata>(), It.IsAny<CancellationToken>()))
            .Callback<VendorMetadata, CancellationToken>((v, _) => capturedVendor = v)
            .ReturnsAsync((VendorMetadata v, CancellationToken _) => v);

        // Act
        var result = await _service.GetOrCreateFromServiceNameAsync(serviceName, category);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedVendor.Should().NotBeNull();
        capturedVendor!.Category.Should().Be(category);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 46: Fallback to service name
    /// Verifies invalid service names are rejected
    /// Validates: Requirements 10.3
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetOrCreateFromServiceNameAsync_InvalidServiceName_ReturnsFailure(string? serviceName)
    {
        // Act
        var result = await _service.GetOrCreateFromServiceNameAsync(serviceName!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Vendor.InvalidName");
    }

    #endregion


    #region Name Normalization Tests

    /// <summary>
    /// Verifies name normalization removes common suffixes and special characters
    /// </summary>
    [Theory]
    [InlineData("Netflix Inc", "netflix")]
    [InlineData("Netflix Inc.", "netflix")]
    [InlineData("Spotify LLC", "spotify")]
    [InlineData("Adobe Corp", "adobe")]
    [InlineData("Microsoft Corp.", "microsoft")]
    [InlineData("Apple Ltd", "apple")]
    [InlineData("Google Co", "google")]
    public void NormalizeName_RemovesCommonSuffixes(string input, string expected)
    {
        // Act
        var result = _service.NormalizeName(input);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Verifies name normalization handles special characters
    /// </summary>
    [Theory]
    [InlineData("Disney+", "disney")]
    [InlineData("HBO Max", "hbo max")]
    [InlineData("Apple TV+", "apple tv")]
    [InlineData("YouTube Premium", "youtube premium")]
    public void NormalizeName_HandlesSpecialCharacters(string input, string expected)
    {
        // Act
        var result = _service.NormalizeName(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Vendor Enrichment Tests

    /// <summary>
    /// Verifies vendor enrichment generates website URL for known vendors
    /// </summary>
    [Fact]
    public async Task EnrichVendorAsync_KnownVendor_GeneratesWebsiteUrl()
    {
        // Arrange
        var vendor = new VendorMetadata
        {
            Id = "vendor-1",
            Name = "Netflix",
            NormalizedName = "netflix",
            Category = "Entertainment",
            WebsiteUrl = null,
            LogoUrl = null
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync("vendor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _mockRepository
            .Setup(r => r.UpdateVendorAsync(It.IsAny<VendorMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.EnrichVendorAsync("vendor-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        vendor.WebsiteUrl.Should().NotBeNullOrEmpty();
        vendor.WebsiteUrl.Should().Contain("netflix");
    }

    /// <summary>
    /// Verifies vendor enrichment generates favicon URL when website is available
    /// </summary>
    [Fact]
    public async Task EnrichVendorAsync_WithWebsiteUrl_GeneratesFaviconUrl()
    {
        // Arrange
        var vendor = new VendorMetadata
        {
            Id = "vendor-1",
            Name = "Custom Service",
            NormalizedName = "custom service",
            Category = "Other",
            WebsiteUrl = "https://www.customservice.com",
            LogoUrl = null
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync("vendor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _mockRepository
            .Setup(r => r.UpdateVendorAsync(It.IsAny<VendorMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.EnrichVendorAsync("vendor-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        vendor.LogoUrl.Should().NotBeNullOrEmpty();
        vendor.LogoUrl.Should().Contain("google.com/s2/favicons");
    }

    /// <summary>
    /// Verifies vendor enrichment returns failure for non-existent vendor
    /// </summary>
    [Fact]
    public async Task EnrichVendorAsync_NonExistentVendor_ReturnsFailure()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorMetadata?)null);

        // Act
        var result = await _service.EnrichVendorAsync("non-existent");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Vendor.NotFound");
    }

    #endregion

    #region Caching Tests

    /// <summary>
    /// Verifies vendors are cached after first retrieval
    /// </summary>
    [Fact]
    public async Task MatchVendorAsync_CachesVendorsAfterFirstCall()
    {
        // Arrange
        var vendors = new List<VendorMetadata>
        {
            new() { Id = "v1", Name = "Netflix", NormalizedName = "netflix", Category = "Entertainment" }
        };

        _mockRepository
            .Setup(r => r.GetByNormalizedNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorMetadata?)null);

        _mockRepository
            .Setup(r => r.GetAllForCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendors);

        // Act - Call twice
        await _service.MatchVendorAsync("Unknown Service 1");
        await _service.MatchVendorAsync("Unknown Service 2");

        // Assert - GetAllForCacheAsync should only be called once due to caching
        _mockRepository.Verify(
            r => r.GetAllForCacheAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
