using System.Text.RegularExpressions;

namespace AqlanDentalPro.Application.Utilities;

/// <summary>
/// Normalizes Yemen phone numbers to the unified format: 967XXXXXXXXX (12 digits)
/// Accepted inputs: 770123456, 0770123456, +967770123456, 967770123456
/// </summary>
public static partial class PhoneNormalizer
{
    [GeneratedRegex(@"^\+?967(\d{9})$")]
    private static partial Regex WithCountryCode();

    [GeneratedRegex(@"^0(\d{9})$")]
    private static partial Regex WithLeadingZero();

    [GeneratedRegex(@"^(\d{9})$")]
    private static partial Regex NineDigits();

    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = Regex.Replace(phone.Trim(), @"[\s\-\(\)]", "");

        if (WithCountryCode().Match(digits) is { Success: true } m1)
            return "967" + m1.Groups[1].Value;

        if (WithLeadingZero().Match(digits) is { Success: true } m2)
            return "967" + m2.Groups[1].Value;

        if (NineDigits().Match(digits) is { Success: true } m3)
            return "967" + m3.Groups[1].Value;

        return digits; // return as-is if unrecognized format
    }
}
