namespace AqlanDentalPro.Application.Services;

/// <summary>
/// CORE-APPT-008: resolves which appointments a reminder run should target.
/// <para>
/// Extracted as a pure function (no clock, no DB, no globals) so the date/hour
/// arithmetic is directly testable — the bug it replaces was invisible precisely
/// because it lived inline inside a method that needs an SMS gateway to run.
/// </para>
/// </summary>
public readonly record struct ReminderWindow(DateOnly Date, TimeOnly Start, TimeOnly End)
{
    /// <summary>
    /// The window for a reminder sent <paramref name="hoursBefore"/> hours ahead of
    /// the appointment, evaluated at <paramref name="clinicNow"/> (which must already
    /// be clinic-local time — passing a UTC value reintroduces CORE-APPT-007/008).
    /// <para>
    /// The target moment is simply <c>clinicNow + hoursBefore</c>; appointments are
    /// matched in the one-hour bucket starting there. The previous implementation
    /// derived the date as <c>AddDays(hoursBefore >= 24 ? 1 : 0)</c> and ignored the
    /// time of day entirely, which is only correct when the run happens to fire at
    /// the same hour as the appointment.
    /// </para>
    /// </summary>
    public static ReminderWindow For(DateTime clinicNow, int hoursBefore)
    {
        var target = clinicNow.AddHours(hoursBefore);
        var start = TimeOnly.FromDateTime(target);

        // A bucket that would spill past midnight is clamped to end-of-day rather
        // than wrapping: the remainder belongs to the NEXT clinic date and is picked
        // up by the run that targets that date. Wrapping would silently attribute
        // after-midnight appointments to the wrong day.
        //
        // Detect the wrap from TimeOnly itself (it wraps at midnight) rather than
        // comparing against a hard-coded 23:00 — at exactly 23:00 that comparison is
        // off by one and yields an inverted 23:00–00:00 range that matches nothing.
        var candidateEnd = start.AddHours(1);
        var end = candidateEnd > start ? candidateEnd : TimeOnly.MaxValue;

        return new ReminderWindow(DateOnly.FromDateTime(target), start, end);
    }

    /// <summary>True when an appointment starting at <paramref name="startTime"/> falls in this window.</summary>
    public bool Contains(TimeOnly startTime) => startTime >= Start && startTime <= End;
}
