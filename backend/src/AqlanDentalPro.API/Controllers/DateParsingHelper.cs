using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// ERR-01 FIX: Shared helper for safe date parsing from user input.
/// Returns 400 with Arabic message on invalid dates instead of throwing 500.
/// </summary>
public static class DateParsingHelper
{
    /// <summary>
    /// Safely parses a date string from user input. Returns null if input is null/empty.
    /// Returns a BadRequest ObjectResult if the date format is invalid.
    /// </summary>
    public static (DateOnly? date, IActionResult? error) TryParseDate(string? input, string fieldName = "التاريخ")
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null);

        if (DateOnly.TryParse(input, out var parsed))
            return (parsed, null);

        return (null, new BadRequestObjectResult(new { message = $"تنسيق {fieldName} غير صالح. استخدم YYYY-MM-DD" }));
    }

    /// <summary>
    /// Safely parses a date string from user input with a default value.
    /// Returns a BadRequest ObjectResult if the date format is invalid.
    /// </summary>
    public static (DateOnly date, IActionResult? error) TryParseDateOrDefault(string? input, DateOnly defaultValue, string fieldName = "التاريخ")
    {
        if (string.IsNullOrWhiteSpace(input))
            return (defaultValue, null);

        if (DateOnly.TryParse(input, out var parsed))
            return (parsed, null);

        return (default, new BadRequestObjectResult(new { message = $"تنسيق {fieldName} غير صالح. استخدم YYYY-MM-DD" }));
    }

    /// <summary>
    /// Safely parses a datetime string from user input. Returns null if input is null/empty.
    /// Returns a BadRequest ObjectResult if the format is invalid.
    /// </summary>
    public static (DateTime? dateTime, IActionResult? error) TryParseDateTime(string? input, string fieldName = "التاريخ والوقت")
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null);

        if (DateTime.TryParse(input, out var parsed))
            return (parsed, null);

        return (null, new BadRequestObjectResult(new { message = $"تنسيق {fieldName} غير صالح" }));
    }
}
