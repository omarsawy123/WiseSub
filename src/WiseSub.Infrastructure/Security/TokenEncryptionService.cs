using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace WiseSub.Infrastructure.Security;

/// <summary>
/// Implementation of token encryption service using AES-256
/// </summary>
public class TokenEncryptionService : ITokenEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public TokenEncryptionService(IConfiguration configuration)
    {
        // Get encryption key from configuration
        // In production, this should come from Azure Key Vault
        // For development, use User Secrets
        var encryptionKey = configuration["Encryption:Key"] 
            ?? throw new InvalidOperationException("Encryption key not configured");
        
        var encryptionIV = configuration["Encryption:IV"]
            ?? throw new InvalidOperationException("Encryption IV not configured");

        // Ensure key is 32 bytes (256 bits) for AES-256
        _key = Convert.FromBase64String(encryptionKey);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException("Encryption key must be 32 bytes (256 bits) for AES-256");
        }

        // Ensure IV is 16 bytes (128 bits)
        _iv = Convert.FromBase64String(encryptionIV);
        if (_iv.Length != 16)
        {
            throw new InvalidOperationException("Encryption IV must be 16 bytes (128 bits)");
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
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
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

        var cipherText = Convert.FromBase64String(encryptedText);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(cipherText);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }
}
