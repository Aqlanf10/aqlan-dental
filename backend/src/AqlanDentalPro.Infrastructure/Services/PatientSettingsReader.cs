using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public sealed class PatientSettingsReader(AppDbContext db) : IPatientSettingsReader
{
    public const string NumberPrefixKey = "patient.number_prefix";
    public const string DefaultNumberPrefix = "GM";
    private const int MaxPrefixLength = 8;

    public async Task<string> GetNumberPrefixAsync(CancellationToken cancellationToken = default)
    {
        var stored = await db.Settings
            .AsNoTracking()
            .Where(setting => setting.Key == NumberPrefixKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return NormalizePrefix(stored);
    }

    internal static string NormalizePrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultNumberPrefix;

        var normalized = new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Take(MaxPrefixLength)
            .ToArray())
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? DefaultNumberPrefix : normalized;
    }
}
