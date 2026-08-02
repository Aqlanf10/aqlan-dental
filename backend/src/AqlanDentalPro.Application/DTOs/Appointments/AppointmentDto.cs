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
    public Guid? OrthoCaseId { get; set; }

    // YOLO-S1: Companion/Guardian + Color + Treatment Package
    public string? CompanionName { get; set; }
    public string? CompanionPhone { get; set; }
    public string? CompanionRelationship { get; set; }
    public string? AppointmentColor { get; set; }
    public Guid? PackageId { get; set; }
    public string? PackageName { get; set; }
    public string? PackageColor { get; set; }

    // Reminder availability (for frontend button state)
    /// <summary>Whether the patient has an email on file (for showing/disabling email reminder button).</summary>
    public bool HasEmail { get; set; }
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
    public Guid? OrthoCaseId { get; set; }

    // YOLO-S1: Companion/Guardian + Color + Treatment Package — all optional,
    // nullable, and default null so existing callers see no behavior change.
    public string? CompanionName { get; set; }
    public string? CompanionPhone { get; set; }
    public string? CompanionRelationship { get; set; }
    public string? AppointmentColor { get; set; }
    public Guid? PackageId { get; set; }
}

public class UpdateAppointmentStatusRequest : AqlanDentalPro.Application.DTOs.Journey.IFutureAppointmentOverrideRequest
{
    public string Status { get; set; } = string.Empty;
    public bool OverrideFutureAppointment { get; set; }
    public string? OverrideReason { get; set; }
}

/// <summary>Request body for batch updating appointment statuses.</summary>
public class BatchUpdateStatusRequest : AqlanDentalPro.Application.DTOs.Journey.IFutureAppointmentOverrideRequest
{
    public List<Guid> AppointmentIds { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public bool OverrideFutureAppointment { get; set; }
    public string? OverrideReason { get; set; }
}

/// <summary>Request body for calling a patient to a room.</summary>
public class CallPatientRequest
{
    public string RoomName { get; set; } = string.Empty;
}
