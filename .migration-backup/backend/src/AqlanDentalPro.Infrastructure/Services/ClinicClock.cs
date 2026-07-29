using AqlanDentalPro.Application.Interfaces.Services;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>Thin adapter exposing ClinicTimeProvider to the Application layer.</summary>
public sealed class ClinicClock : IClinicClock
{
    public DateOnly Today() => ClinicTimeProvider.ClinicToday();
}
