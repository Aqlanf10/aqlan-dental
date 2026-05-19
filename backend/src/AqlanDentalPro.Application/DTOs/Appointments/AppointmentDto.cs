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

    // Queue / clinic-flow fields (Sprint 4.5)
    public string? RoomName { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? InRoomAt { get; set; }

    // Patient Journey fields (Sprint: Command Center)
    public Guid? ServiceId { get; set; }
    public Guid? ClinicRoomId { get; set; }
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
    public Guid? ServiceId { get; set; }
    public Guid? ClinicRoomId { get; set; }
}

public class UpdateAppointmentStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

/// <summary>Request body for batch updating appointment statuses.</summary>
public class BatchUpdateStatusRequest
{
    public List<Guid> AppointmentIds { get; set; } = [];
    public string Status { get; set; } = string.Empty;
}

/// <summary>Request body for calling a patient to a room.</summary>
public class CallPatientRequest
{
    public string RoomName { get; set; } = string.Empty;
}
