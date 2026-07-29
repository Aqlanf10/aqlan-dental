using System.Globalization;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Shared utility methods for booking request processing.
/// Extracted from BookingRequestService for testability and reuse.
/// </summary>
public static class BookingUtilities
{
    /// <summary>
    /// Normalizes an Arabic AM/PM time string (e.g., "9:00 ص") to 24h format (e.g., "09:00").
    /// Also handles already-24h format strings (e.g., "09:00").
    /// Returns null if the input cannot be parsed.
    /// </summary>
    public static string? NormalizeTo24h(string? time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return null;

        time = time.Trim();

        // Already in 24h format like "09:00" or "14:30"
        if (TimeOnly.TryParseExact(time, "HH:mm", out var t24))
            return t24.ToString("HH:mm");

        // Arabic AM/PM format: "9:00 ص", "2:30 م", "12:00 م"
        var isPM = time.Contains('م');
        var isAM = time.Contains('ص');

        if (!isPM && !isAM)
        {
            // Try general parse as fallback
            if (TimeOnly.TryParse(time, out var tGeneral))
                return tGeneral.ToString("HH:mm");
            return null;
        }

        // Remove Arabic markers and parse
        var cleanTime = time.Replace("ص", "").Replace("م", "").Trim();
        if (!TimeOnly.TryParse(cleanTime, out var parsed))
            return null;

        // Convert to 24h
        if (isPM && parsed.Hour < 12)
            parsed = parsed.AddHours(12);
        else if (isAM && parsed.Hour == 12)
            parsed = parsed.AddHours(-12);

        return parsed.ToString("HH:mm");
    }

    /// <summary>
    /// Checks if a booking request's PreferredTime matches a 24h slot format.
    /// Handles both Arabic ("9:00 ص") and 24h ("09:00") formats.
    /// </summary>
    public static bool IsSameSlotTime(string? preferredTime, string? slot24h)
    {
        var normalized = NormalizeTo24h(preferredTime);
        return normalized == slot24h;
    }

    /// <summary>
    /// Normalizes a phone number by removing spaces, dashes, parentheses, and plus signs.
    /// </summary>
    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        return phone.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "");
    }

    /// <summary>
    /// Normalizes a patient name by trimming, collapsing whitespace, and lowercasing
    /// so that minor formatting differences don't bypass duplicate detection.
    /// </summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        // Collapse multiple spaces/tabs into a single space, trim, and lowercase
        return CultureInfo.CurrentCulture.TextInfo.ToLower(name.Trim())
            .Replace("  ", " ").Replace("  ", " ").Trim();
    }
}
