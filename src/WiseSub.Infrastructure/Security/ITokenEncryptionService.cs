namespace WiseSub.Infrastructure.Security;

/// <summary>
/// Service for encrypting and decrypting OAuth tokens using AES-256
/// </summary>
public interface ITokenEncryptionService
{
    /// <summary>
    /// Encrypts a plain text token using AES-256 encryption
    /// </summary>
    /// <param name="plainText">The plain text token to encrypt</param>
    /// <returns>Base64 encoded encrypted token</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts an encrypted token back to plain text
    /// </summary>
    /// <param name="encryptedText">The Base64 encoded encrypted token</param>
    /// <returns>The decrypted plain text token</returns>
    string Decrypt(string encryptedText);
}
