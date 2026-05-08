namespace AqlanDentalPro.Domain.Entities;

public class BookingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PatientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ServiceType { get; set; }
    public DateOnly? PreferredDate { get; set; }
    public string? PreferredTime { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "pending"; // pending | confirmed | cancelled
    public string? StaffNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
