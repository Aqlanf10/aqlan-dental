namespace AqlanDentalPro.Domain.Entities;

public class BookingRequest : BaseEntity
{
    public string PatientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ServiceType { get; set; }
    public string? PreferredDate { get; set; }
    public string? PreferredTime { get; set; }
    public string? Notes { get; set; }
    public BookingRequestStatus Status { get; set; } = BookingRequestStatus.Pending;
    public string? StaffNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ConvertedToAppointmentId { get; set; }
}

public enum BookingRequestStatus
{
    Pending = 0,
    Reviewed = 1,
    Confirmed = 2,
    Rejected = 3
}
