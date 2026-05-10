namespace AqlanDentalPro.Application.Exceptions;

/// <summary>
/// Thrown when a patient submits a duplicate booking request for the same preferred date.
/// </summary>
public class DuplicateBookingRequestException : Exception
{
    public DuplicateBookingRequestException(string message) : base(message) { }
}
