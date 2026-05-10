namespace AqlanDentalPro.Application.DTOs.PatientPortal;

// Auth
public class PatientLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class PatientForgotPasswordRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}

public class PatientResetPasswordRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class PatientChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class PatientRefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class PatientAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public PatientPortalProfileDto Profile { get; set; } = null!;
    public bool MustChangePassword { get; set; }
}

// Account info — lightweight DTO for account creation
public class PatientAccountInfoDto
{
    public string PatientNumber { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

// Credentials — for staff to view patient portal account info (no password)
public class PatientPortalCredentialsDto
{
    public string Username { get; set; } = string.Empty;
    public bool AccountActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool HasPortalAccount { get; set; }
}

// Password reset response — temporary password shown once only
public class PatientPasswordResetResponseDto
{
    public string TemporaryPassword { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// Profile — extended with more patient-facing fields
public class PatientPortalProfileDto
{
    public Guid Id { get; set; }
    public string PatientNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Gender { get; set; }
    public int? Age { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? PrimaryDoctorName { get; set; }
    public string AccountStatus { get; set; } = "active";
    public string? LastLogin { get; set; }
    public string? Username { get; set; }
}

// Profile update request — patient can only update limited fields
public class PatientProfileUpdateDto
{
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Address { get; set; }
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
    public bool CanCancel { get; set; }
}

public class PatientAppointmentRequestDto
{
    public string AppointmentDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }
}

// Treatment — enriched with visit context
public class PatientTreatmentDto
{
    public Guid Id { get; set; }
    public string TreatmentType { get; set; } = string.Empty;
    public string? ToothNumber { get; set; }
    public string? MaterialUsed { get; set; }
    public string? DoctorName { get; set; }
    public string? VisitDate { get; set; }
    public string? Specialty { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

// Visit summary — safe patient-facing view of visits
public class PatientVisitDto
{
    public Guid Id { get; set; }
    public string VisitDate { get; set; } = string.Empty;
    public string? VisitType { get; set; }
    public string? Specialty { get; set; }
    public string? DoctorName { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? TreatmentDone { get; set; }
    public string? Instructions { get; set; }
    public string? NextVisitPlan { get; set; }
    public string? NextVisitDate { get; set; }
    public int TreatmentCount { get; set; }
}

// Prescription — with proper drug details
public class PatientPrescriptionDto
{
    public Guid Id { get; set; }
    public string? Diagnosis { get; set; }
    public List<PrescriptionDrugDto> Drugs { get; set; } = [];
    public string? Instructions { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

// Single drug in a prescription
public class PrescriptionDrugDto
{
    public string Name { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Notes { get; set; }
}

// Payment
public class PatientPaymentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ServiceDescription { get; set; }
    public string? ReceiptNumber { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

// Contract summary for patient
public class PatientContractDto
{
    public Guid Id { get; set; }
    public string? Specialty { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StartDate { get; set; }
}

public class PatientFinancialSummaryDto
{
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int ActiveContracts { get; set; }
    public List<PatientContractDto> Contracts { get; set; } = [];
    public List<PatientPaymentDto> RecentPayments { get; set; } = [];
}

// Doctor (for appointment request — portal-accessible)
public class PatientDoctorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Specialty { get; set; }
}

// Clinic info
public class PatientClinicInfoDto
{
    public string ClinicName { get; set; } = "مركز د. عقلان الكامل لطب وتقويم الأسنان";
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Address { get; set; }
    public string? WorkingHours { get; set; }
}

// Dashboard — enriched with more sections
public class PatientPortalDashboardDto
{
    public PatientPortalProfileDto Profile { get; set; } = null!;
    public PatientAppointmentDto? NextAppointment { get; set; }
    public int TotalAppointments { get; set; }
    public int UpcomingAppointments { get; set; }
    public int CompletedTreatments { get; set; }
    public PatientFinancialSummaryDto Finance { get; set; } = new();
    public PatientPrescriptionDto? LatestPrescription { get; set; }
    public PatientClinicInfoDto ClinicInfo { get; set; } = new();
}
