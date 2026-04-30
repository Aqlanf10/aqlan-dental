namespace AqlanDentalPro.Application.DTOs.PatientPortal;

// Auth
public class PatientLoginRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}

public class PatientVerifyRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class PatientAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public PatientPortalProfileDto Profile { get; set; } = null!;
}

// Profile
public class PatientPortalProfileDto
{
    public Guid Id { get; set; }
    public string PatientNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public int? Age { get; set; }
    public string? PrimaryDoctorName { get; set; }
}

// Appointment
public class PatientAppointmentDto
{
    public Guid Id { get; set; }
    public string AppointmentDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class PatientAppointmentRequestDto
{
    public string AppointmentDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }
}

// Treatment
public class PatientTreatmentDto
{
    public Guid Id { get; set; }
    public string TreatmentType { get; set; } = string.Empty;
    public string? ToothNumber { get; set; }
    public string? MaterialUsed { get; set; }
    public string? DoctorName { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

// Prescription
public class PatientPrescriptionDto
{
    public Guid Id { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

// Payment
public class PatientPaymentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReceiptNumber { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class PatientFinancialSummaryDto
{
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int ActiveContracts { get; set; }
    public List<PatientPaymentDto> RecentPayments { get; set; } = [];
}

// Dashboard
public class PatientPortalDashboardDto
{
    public PatientPortalProfileDto Profile { get; set; } = null!;
    public PatientAppointmentDto? NextAppointment { get; set; }
    public int TotalAppointments { get; set; }
    public int CompletedTreatments { get; set; }
    public PatientFinancialSummaryDto Finance { get; set; } = new();
}
