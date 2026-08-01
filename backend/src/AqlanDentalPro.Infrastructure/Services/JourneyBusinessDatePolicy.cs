using AqlanDentalPro.Application.Interfaces.Services;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// W05: one rule for arrival, queue entry, and visit start.
/// Past and same-day appointments are allowed. Future appointments require an
/// explicit manager override and an operational reason.
/// </summary>
public sealed class JourneyBusinessDatePolicy(IClinicClock clock) : IJourneyBusinessDatePolicy
{
    public JourneyBusinessDateDecision Evaluate(
        DateOnly appointmentDate,
        bool overrideRequested,
        string? overrideReason,
        bool canOverride)
    {
        var businessDate = clock.Today();
        if (appointmentDate <= businessDate)
        {
            return new JourneyBusinessDateDecision(
                Allowed: true,
                Overridden: false,
                BusinessDate: businessDate,
                OverrideReason: null,
                ErrorCode: null,
                Message: null);
        }

        if (!overrideRequested)
        {
            return Denied(
                businessDate,
                "future_appointment",
                $"لا يمكن تنفيذ رحلة المريض لموعد مستقبلي ({appointmentDate:yyyy-MM-dd}). تاريخ العيادة اليوم هو {businessDate:yyyy-MM-dd}.");
        }

        if (!canOverride)
        {
            return Denied(
                businessDate,
                "future_appointment_override_forbidden",
                "تجاوز تاريخ الموعد المستقبلي متاح للمدير فقط.");
        }

        var normalizedReason = overrideReason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return Denied(
                businessDate,
                "future_appointment_override_reason_required",
                "سبب التجاوز الإداري إلزامي.");
        }

        return new JourneyBusinessDateDecision(
            Allowed: true,
            Overridden: true,
            BusinessDate: businessDate,
            OverrideReason: normalizedReason,
            ErrorCode: null,
            Message: null);
    }

    private static JourneyBusinessDateDecision Denied(
        DateOnly businessDate,
        string errorCode,
        string message) => new(
            Allowed: false,
            Overridden: false,
            BusinessDate: businessDate,
            OverrideReason: null,
            ErrorCode: errorCode,
            Message: message);
}
