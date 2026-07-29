using System.Text.Json;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// CORE-APPT-011: reconciles the two independent "already sent" records for SMS
/// reminders.
/// <para>
/// The background job tracks each reminder window separately in
/// <c>SmsReminderWindowsSent</c> (a JSON int array), while the manual/fallback path
/// in <c>SmsService.SendAppointmentReminderAsync</c> sets the legacy single boolean
/// <c>ConfirmationSent</c>. Neither observed the other, so a manual 24-hour reminder
/// followed by the job reaching the same window texted the patient twice.
/// </para>
/// <para>
/// <c>ConfirmationSent</c> carries no window information. Per owner decision it is
/// interpreted as the <see cref="LegacyConfirmationWindowHours"/> window only: that
/// is what the manual path is used for in practice (a day-before nudge), it stops
/// the common duplicate, and it leaves the short window free to fire. Treating it as
/// "every window already sent" was rejected deliberately — it would risk NOT
/// reminding a patient shortly before their appointment, working against the
/// no-show problem the system exists to reduce.
/// </para>
/// <para>
/// Pure functions over the two stored values: no clock, no database, no global
/// state, so the interpretation is directly testable.
/// </para>
/// </summary>
public static class ReminderSentWindows
{
    /// <summary>The window the legacy <c>ConfirmationSent</c> boolean is taken to mean.</summary>
    public const int LegacyConfirmationWindowHours = 24;

    /// <summary>Parses the stored JSON array; unset or malformed values yield an empty list.</summary>
    public static List<int> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The windows already sent, combining the per-window record with the legacy
    /// boolean. Never returns duplicates.
    /// </summary>
    public static List<int> Merge(string? json, bool legacyConfirmationSent)
    {
        var windows = Parse(json);
        if (legacyConfirmationSent && !windows.Contains(LegacyConfirmationWindowHours))
            windows.Add(LegacyConfirmationWindowHours);
        return windows;
    }

    /// <summary>True when <paramref name="hoursBefore"/> has already been sent by either path.</summary>
    public static bool WasSent(string? json, bool legacyConfirmationSent, int hoursBefore) =>
        Merge(json, legacyConfirmationSent).Contains(hoursBefore);
}
