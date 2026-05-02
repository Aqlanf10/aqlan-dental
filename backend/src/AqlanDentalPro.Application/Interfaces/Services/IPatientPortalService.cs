using AqlanDentalPro.Application.DTOs.PatientPortal;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPatientPortalService
{
    // Auth - Username/Password login
    Task<(PatientAuthResponse? response, string? error)> LoginAsync(string username, string password);
    Task<(bool success, string? error)> RequestCredentialsViaWhatsAppAsync(string phoneNumber);

    // Portal account management
    Task<(PatientPortalAccountInfoDto? info, string? error)> GetPortalAccountInfoAsync(Guid patientId);
    Task<(PatientPasswordResetResponseDto? result, string? error)> ResetPasswordAsync(Guid patientId);
    Task<(PatientAccountCreationResult? result, string? error)> EnsurePortalAccountAsync(Guid patientId);

    // Dashboard & data
    Task<PatientPortalDashboardDto> GetDashboardAsync(Guid patientId);
    Task<List<PatientAppointmentDto>> GetAppointmentsAsync(Guid patientId, int limit = 20);
    Task<(PatientAppointmentDto? result, string? error)> RequestAppointmentAsync(Guid patientId, PatientAppointmentRequestDto req);
    Task<(bool success, string? error)> CancelAppointmentAsync(Guid patientId, Guid appointmentId);
    Task<List<PatientTreatmentDto>> GetTreatmentsAsync(Guid patientId, int limit = 20);
    Task<List<PatientPrescriptionDto>> GetPrescriptionsAsync(Guid patientId, int limit = 20);
    Task<PatientFinancialSummaryDto> GetFinancialSummaryAsync(Guid patientId);
    Task<Guid?> GetPatientIdByPhoneAsync(string phoneNumber);
}

public class PatientAccountCreationResult
{
    public string Username { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}
