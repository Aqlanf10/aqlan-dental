using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// FIN-SETTINGS — read helper for the <c>finance.*</c> Settings namespace.
/// Mirrors the <see cref="FinanceClinicIdentity"/> fallback pattern: every key
/// has a default in <see cref="FinanceSettingsKeys.Defaults"/> so missing rows
/// never break behavior. Defaults preserve the current production values.
/// </summary>
public sealed class FinanceSettingsReader(AppDbContext db)
{
    /// <summary>
    /// Returns the raw string value for <paramref name="key"/>, falling back to
    /// <see cref="FinanceSettingsKeys.Defaults"/> when the row is missing or empty.
    /// </summary>
    public async Task<string> GetAsync(string key, CancellationToken ct = default)
    {
        var stored = await db.Settings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(stored)) return stored.Trim();
        return FinanceSettingsKeys.Defaults.TryGetValue(key, out var fallback) ? fallback : "";
    }

    /// <summary>
    /// Parses the value as <see cref="decimal"/>; falls back to the default when
    /// the row is missing or unparsable.
    /// </summary>
    public async Task<decimal> GetDecimalAsync(string key, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, ct);
        return decimal.TryParse(raw, out var v) ? v
            : (decimal.TryParse(FinanceSettingsKeys.Defaults.GetValueOrDefault(key), out var d) ? d : 0m);
    }

    /// <summary>
    /// Parses the value as <see cref="int"/>; falls back to the default when
    /// the row is missing or unparsable.
    /// </summary>
    public async Task<int> GetIntAsync(string key, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, ct);
        return int.TryParse(raw, out var v) ? v
            : (int.TryParse(FinanceSettingsKeys.Defaults.GetValueOrDefault(key), out var d) ? d : 0);
    }

    /// <summary>
    /// Parses the value as <see cref="bool"/> (accepts "true"/"false" case-insensitively,
    /// also "1"/"0"); falls back to the default when the row is missing or unparsable.
    /// </summary>
    public async Task<bool> GetBoolAsync(string key, CancellationToken ct = default)
    {
        var raw = (await GetAsync(key, ct)).Trim().ToLowerInvariant();
        if (raw is "true" or "1" or "yes" or "on") return true;
        if (raw is "false" or "0" or "no" or "off") return false;
        return bool.TryParse(FinanceSettingsKeys.Defaults.GetValueOrDefault(key), out var b) && b;
    }

    /// <summary>
    /// Parses the value as the enum <typeparamref name="T"/> by name (case-insensitive);
    /// falls back to the default when the row is missing or unparsable.
    /// </summary>
    public async Task<T> GetEnumAsync<T>(string key, T fallback, CancellationToken ct = default)
        where T : struct, Enum
    {
        var raw = await GetAsync(key, ct);
        if (Enum.TryParse<T>(raw, ignoreCase: true, out var parsed)) return parsed;
        if (Enum.TryParse<T>(FinanceSettingsKeys.Defaults.GetValueOrDefault(key), ignoreCase: true, out var d)) return d;
        return fallback;
    }
}
