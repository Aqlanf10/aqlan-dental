namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Canonical clinic clock. All business-date decisions and event timestamps
/// must flow through this abstraction so Asia/Aden midnight boundaries are
/// deterministic and testable.
/// </summary>
public interface IClinicClock
{
    DateOnly Today();
    DateTime UtcNow();
    DateTime ClinicNow();
    DateOnly DateFromUtc(DateTime utc);
    string TimeZoneId { get; }
}
