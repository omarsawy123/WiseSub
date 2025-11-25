using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace WiseSub.Infrastructure.Security;

/// <summary>
/// Implementation of token encryption service using AES-256
/// Uses random IV per encryption operation for enhanced security.
/// The IV is prepended to the ciphertext and extracted during decryption.
/// </summary>
public class TokenEncryptionService : ITokenEncryptionService
{
    private readonly byte[] _key;
    private const int IvSize = 16; // 128 bits for AES

    public TokenEncryptionService(IConfiguration configuration)
    {
        // Get encryption key from configuration
        // In production, this should come from Azure Key Vault
        // For development, use User Secrets
        var encryptionKey = configuration["Encryption:Key"] 
            ?? throw new InvalidOperationException("Encryption key not configured");

        // Ensure key is 32 bytes (256 bits) for AES-256
        _key = Convert.FromBase64String(encryptionKey);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException("Encryption key must be 32 bytes (256 bits) for AES-256");
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentException("Plain text cannot be null or empty", nameof(plainText));
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // Generate random IV for each encryption
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        
        // Prepend IV to the ciphertext so we can extract it during decryption
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);
        
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            throw new ArgumentException("Encrypted text cannot be null or empty", nameof(encryptedText));
        }

        var fullCipher = Convert.FromBase64String(encryptedText);
        
        if (fullCipher.Length < IvSize)
        {
            throw new ArgumentException("Encrypted text is too short to contain IV", nameof(encryptedText));
        }

        // Extract IV from the beginning of the ciphertext
        var iv = new byte[IvSize];
        var cipherText = new byte[fullCipher.Length - IvSize];
        
        Buffer.BlockCopy(fullCipher, 0, iv, 0, IvSize);
        Buffer.BlockCopy(fullCipher, IvSize, cipherText, 0, cipherText.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(cipherText);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }
}
