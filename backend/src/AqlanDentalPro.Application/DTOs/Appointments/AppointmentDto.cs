namespace AqlanDentalPro.Application.DTOs.Appointments;

public class AppointmentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientNumber { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? DoctorColor { get; set; }
    public string AppointmentDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string AppointmentType { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreateAppointmentRequest
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string AppointmentDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 30;
    public string AppointmentType { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? Notes { get; set; }
}

public class UpdateAppointmentStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
