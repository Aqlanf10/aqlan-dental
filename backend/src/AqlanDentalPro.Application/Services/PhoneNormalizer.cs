namespace AqlanDentalPro.Application.Services;

/// <summary>
/// توحيد صيغة أرقام الهاتف اليمنية — كل الأرقام تُحفظ بصيغة 967XXXXXXXXX
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// توحيد رقم الهاتف:
    /// - إزالة المسافات والشرطات والأقواس
    /// - تحويل الأرقام العربية/الهندية إلى إنجليزية
    /// - إزالة + أو 00 من البداية
    /// - توحيد أرقام اليمن: 770245745 أو 0770245745 أو +967770245745 → 967770245745
    /// </summary>
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        // Convert Arabic/Indic digits to English
        var result = new System.Text.StringBuilder(phone.Length);
        foreach (char c in phone)
        {
            result.Append(c switch
            {
                '٠' or '۰' => '0',
                '١' or '۱' => '1',
                '٢' or '۲' => '2',
                '٣' or '۳' => '3',
                '٤' or '۴' => '4',
                '٥' or '۵' => '5',
                '٦' or '۶' => '6',
                '٧' or '۷' => '7',
                '٨' or '۸' => '8',
                '٩' or '۹' => '9',
                _ => c
            });
        }

        var normalized = result.ToString();

        // Remove spaces, dashes, parentheses, dots
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[\s\-\.\(\)]", "");

        // Remove leading + or 00
        if (normalized.StartsWith("+")) normalized = normalized[1..];
        if (normalized.StartsWith("00")) normalized = normalized[2..];

        // Yemen numbers: if starts with 0, remove leading 0 and add 967
        if (normalized.StartsWith("0") && normalized.Length >= 9)
        {
            normalized = "967" + normalized[1..];
        }
        // If starts with 7 and length is 9 (like 770245745), add 967
        else if (normalized.StartsWith("7") && normalized.Length == 9)
        {
            normalized = "967" + normalized;
        }

        return normalized;
    }
}
