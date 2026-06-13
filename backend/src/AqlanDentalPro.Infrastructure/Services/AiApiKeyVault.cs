using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Stores optional provider API keys encrypted in the Settings table.
/// The encryption key is derived from AiSettings:EncryptionKey, with the
/// production JWT secret as a stable fallback. Environment provider keys
/// remain supported and are used when no stored override exists.
/// </summary>
public sealed class AiApiKeyVault(
    AppDbContext db,
    IConfiguration configuration,
    ILogger<AiApiKeyVault> logger)
{
    private const string SettingPrefix = "ai.secret.";
    private const string CipherPrefix = "v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public async Task<string?> ResolveAsync(
        string provider,
        string environmentVariable,
        CancellationToken cancellationToken = default)
    {
        var encrypted = await GetStoredValueAsync(provider, cancellationToken);
        if (!string.IsNullOrWhiteSpace(encrypted))
        {
            try
            {
                return Decrypt(encrypted);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                logger.LogError(ex, "Unable to decrypt the stored AI key for provider {Provider}", provider);
                throw new InvalidOperationException(
                    "تعذر قراءة مفتاح الذكاء الاصطناعي المحفوظ. أعد إدخاله من الإعدادات.");
            }
        }

        return Environment.GetEnvironmentVariable(environmentVariable);
    }

    public async Task StoreAsync(
        string provider,
        string secret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("مفتاح API لا يمكن أن يكون فارغاً.", nameof(secret));

        var key = SettingKey(provider);
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            db.Settings.Add(new Setting
            {
                Key = key,
                Value = Encrypt(secret.Trim()),
                Category = "ai-secret",
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            setting.Value = Encrypt(secret.Trim());
            setting.Category = "ai-secret";
            setting.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task ClearStoredAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(
            s => s.Key == SettingKey(provider), cancellationToken);
        if (setting is not null)
            db.Settings.Remove(setting);
    }

    public async Task<AiApiKeyStatus> GetStatusAsync(
        string provider,
        string environmentVariable,
        CancellationToken cancellationToken = default)
    {
        var encrypted = await GetStoredValueAsync(provider, cancellationToken);
        if (!string.IsNullOrWhiteSpace(encrypted))
        {
            try
            {
                return BuildStatus(Decrypt(encrypted), "settings");
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                logger.LogWarning(ex, "Stored AI key status could not be read for provider {Provider}", provider);
                return new AiApiKeyStatus(false, null, "invalid");
            }
        }

        var environmentKey = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(environmentKey)
            ? new AiApiKeyStatus(false, null, "none")
            : BuildStatus(environmentKey, "environment");
    }

    private async Task<string?> GetStoredValueAsync(string provider, CancellationToken cancellationToken) =>
        await db.Settings
            .Where(s => s.Key == SettingKey(provider))
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

    private string Encrypt(string plainText)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(DeriveEncryptionKey(), TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, Encoding.UTF8.GetBytes(SettingPrefix));

        return string.Join(':',
            CipherPrefix,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
    }

    private string Decrypt(string protectedValue)
    {
        var parts = protectedValue.Split(':');
        if (parts.Length != 4 || parts[0] != CipherPrefix)
            throw new FormatException("Unsupported AI secret format.");

        var nonce = Convert.FromBase64String(parts[1]);
        var ciphertext = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(DeriveEncryptionKey(), TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(SettingPrefix));
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] DeriveEncryptionKey()
    {
        var masterSecret = configuration["AiSettings:EncryptionKey"]
            ?? configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(masterSecret) || masterSecret.Contains("CHANGE_ME"))
            throw new InvalidOperationException(
                "يجب ضبط مفتاح تشفير آمن قبل حفظ مفاتيح الذكاء الاصطناعي.");

        return SHA256.HashData(
            Encoding.UTF8.GetBytes($"aqlan-dental:ai-settings:v1:{masterSecret}"));
    }

    private static string SettingKey(string provider) =>
        $"{SettingPrefix}{provider.Trim().ToLowerInvariant()}";

    private static AiApiKeyStatus BuildStatus(string secret, string source)
    {
        var masked = secret.Length > 8 ? $"********{secret[^4..]}" : "********";
        return new AiApiKeyStatus(true, masked, source);
    }
}

public sealed record AiApiKeyStatus(bool Configured, string? Masked, string Source);
