namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// W05: canonical business-date rule for patient-journey mutations.
/// A future appointment is blocked unless an explicitly authorized manager
/// supplies a non-empty operational reason.
/// </summary>
public interface IJourneyBusinessDatePolicy
{
    JourneyBusinessDateDecision Evaluate(
        DateOnly appointmentDate,
        bool overrideRequested,
        string? overrideReason,
        bool canOverride);
}

public sealed record JourneyBusinessDateDecision(
    bool Allowed,
    bool Overridden,
    DateOnly BusinessDate,
    string? OverrideReason,
    string? ErrorCode,
    string? Message);
