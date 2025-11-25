using Microsoft.Extensions.Configuration;
using WiseSub.Infrastructure.Security;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Security;

public class TokenEncryptionServiceTests
{
    private readonly ITokenEncryptionService _encryptionService;

    public TokenEncryptionServiceTests()
    {
        // Create configuration with test encryption key (IV is now generated per encryption)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = "kI38I1KsiqNMQyOpkC2UU1YBvsdMycIfcRNSm3h7Zfs=" // 32 bytes base64
            })
            .Build();

        _encryptionService = new TokenEncryptionService(configuration);
    }

    [Fact]
    public void Encrypt_ValidToken_ReturnsEncryptedString()
    {
        // Arrange
        var plainToken = "test-oauth-token-12345";

        // Act
        var encrypted = _encryptionService.Encrypt(plainToken);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
        Assert.NotEqual(plainToken, encrypted);
    }

    [Fact]
    public void Decrypt_EncryptedToken_ReturnsOriginalToken()
    {
        // Arrange
        var plainToken = "test-oauth-token-12345";
        var encrypted = _encryptionService.Encrypt(plainToken);

        // Act
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainToken, decrypted);
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_PreservesToken()
    {
        // Arrange
        var originalToken = "ya29.a0AfH6SMBx...long-oauth-token...xyz";

        // Act
        var encrypted = _encryptionService.Encrypt(originalToken);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(originalToken, decrypted);
    }

    [Fact]
    public void Encrypt_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var emptyToken = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(emptyToken));
    }

    [Fact]
    public void Decrypt_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var emptyToken = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(emptyToken));
    }

    [Fact]
    public void Encrypt_SameTokenTwice_ProducesDifferentEncryptedValues()
    {
        // Arrange
        var token = "test-token";

        // Act
        var encrypted1 = _encryptionService.Encrypt(token);
        var encrypted2 = _encryptionService.Encrypt(token);

        // Assert
        // With random IV per encryption, same plaintext produces different ciphertexts
        // This is the SECURE behavior - prevents pattern detection
        Assert.NotEqual(encrypted1, encrypted2);
        
        // But both should decrypt to the same value
        Assert.Equal(_encryptionService.Decrypt(encrypted1), _encryptionService.Decrypt(encrypted2));
    }

    [Fact]
    public void Encrypt_DifferentTokens_ProducesDifferentEncryptedValues()
    {
        // Arrange
        var token1 = "test-token-1";
        var token2 = "test-token-2";

        // Act
        var encrypted1 = _encryptionService.Encrypt(token1);
        var encrypted2 = _encryptionService.Encrypt(token2);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Decrypt_TooShortCiphertext_ThrowsArgumentException()
    {
        // Arrange - ciphertext shorter than IV size (16 bytes)
        var tooShort = Convert.ToBase64String(new byte[10]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(tooShort));
    }
}
