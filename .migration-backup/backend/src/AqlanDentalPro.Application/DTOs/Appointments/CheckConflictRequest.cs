namespace AqlanDentalPro.Application.DTOs.Appointments;

public class CheckConflictRequest
{
    public Guid DoctorId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public Guid? ExcludeId { get; set; }
}
