namespace AqlanDentalPro.Application.Exceptions;

/// <summary>
/// Thrown when a booking request slot is no longer available (race condition).
/// </summary>
public class SlotNotAvailableException : Exception
{
    public SlotNotAvailableException(string message) : base(message) { }
}
