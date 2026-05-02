using AqlanDentalPro.Application.DTOs.PatientPortal;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPatientPortalService
{
    Task<(bool success, string? error)> SendVerificationCodeAsync(string phoneNumber);
    Task<(PatientAuthResponse? response, string? error)> VerifyCodeAsync(string phoneNumber, string code);
    Task<PatientPortalDashboardDto> GetDashboardAsync(Guid patientId);
    Task<PatientPortalProfileDto> GetProfileAsync(Guid patientId);
    Task<(PatientPortalProfileDto? result, string? error)> UpdateProfileAsync(Guid patientId, PatientProfileUpdateDto req);
    Task<List<PatientAppointmentDto>> GetAppointmentsAsync(Guid patientId, int limit = 20);
    Task<(PatientAppointmentDto? result, string? error)> RequestAppointmentAsync(Guid patientId, PatientAppointmentRequestDto req);
    Task<(bool success, string? error)> CancelAppointmentAsync(Guid patientId, Guid appointmentId);
    Task<List<PatientTreatmentDto>> GetTreatmentsAsync(Guid patientId, int limit = 20);
    Task<List<PatientVisitDto>> GetVisitsAsync(Guid patientId, int limit = 20);
    Task<List<PatientPrescriptionDto>> GetPrescriptionsAsync(Guid patientId, int limit = 20);
    Task<PatientFinancialSummaryDto> GetFinancialSummaryAsync(Guid patientId);
    Task<List<PatientDoctorDto>> GetDoctorsAsync();
    Task<PatientClinicInfoDto> GetClinicInfoAsync();
    Task<Guid?> GetPatientIdByPhoneAsync(string phoneNumber);
}
