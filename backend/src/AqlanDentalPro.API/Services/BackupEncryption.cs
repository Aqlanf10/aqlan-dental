using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// SEC-08: AES-GCM encryption helper for backup files. Backups contain full PHI (patient
/// names, phones, DOBs, doctor license numbers, employee salaries) — storing them as
/// plaintext JSON on disk means any filesystem exposure (offsite backup copy, misconfigured
/// S3, system backup inclusion) leaks the entire clinic's PHI.
///
/// This helper encrypts/decrypts byte arrays using AES-GCM with a key from the
/// BACKUP_ENCRYPTION_KEY env var (32 bytes = 256 bits, base64-encoded).
///
/// File format (all little-endian):
///   [16 bytes nonce] [N bytes ciphertext] [16 bytes GCM tag]
/// The tag is appended (AES-GCM in .NET returns ciphertext and tag separately; we combine
/// them for single-file storage). Decryption reverses the split.
///
/// If BACKUP_ENCRYPTION_KEY is not set, the helper throws — encryption is mandatory in
/// Production. In Development, callers may skip encryption (plaintext fallback) for convenience.
/// </summary>
public static class BackupEncryption
{
    private const int NonceSize = 12; // AES-GCM standard nonce size
    private const int TagSize = 16;   // AES-GCM standard tag size

    /// <summary>
    /// Loads the encryption key from BACKUP_ENCRYPTION_KEY env var. Throws if unset/invalid.
    /// </summary>
    public static byte[] LoadKeyFromEnvironment()
    {
        var keyBase64 = Environment.GetEnvironmentVariable("BACKUP_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException(
                "SEC-08: BACKUP_ENCRYPTION_KEY env var is not set. Backup encryption is required. " +
                "Generate a 32-byte key (e.g., 'openssl rand -base64 32') and set BACKUP_ENCRYPTION_KEY.");

        var key = Convert.TryFromBase64String(keyBase64, new byte[64], out _)
            ? Convert.FromBase64String(keyBase64)
            : Encoding.UTF8.GetBytes(keyBase64);

        if (key.Length != 32)
            throw new InvalidOperationException(
                $"SEC-08: BACKUP_ENCRYPTION_KEY must be 32 bytes (256 bits) for AES-256-GCM. Current length: {key.Length} bytes. " +
                "Generate with: openssl rand -base64 32");

        return key;
    }

    /// <summary>
    /// Encrypts plaintext bytes. Returns: [nonce(12)] [ciphertext(N)] [tag(16)].
    /// </summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var gcm = new AesGcm(key, TagSize);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // Combine: nonce + ciphertext + tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);
        return result;
    }

    /// <summary>
    /// Decrypts bytes in format: [nonce(12)] [ciphertext(N)] [tag(16)]. Returns plaintext.
    /// Throws CryptographicException if the key is wrong or the data was tampered with.
    /// </summary>
    public static byte[] Decrypt(byte[] encrypted, byte[] key)
    {
        if (encrypted.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted backup is too short — corrupted or not encrypted.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[encrypted.Length - NonceSize - TagSize];

        Buffer.BlockCopy(encrypted, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encrypted, NonceSize, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(encrypted, NonceSize + ciphertext.Length, tag, 0, TagSize);

        var plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, TagSize);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
