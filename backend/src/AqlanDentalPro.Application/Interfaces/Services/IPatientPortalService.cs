using AqlanDentalPro.Application.DTOs.PatientPortal;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPatientPortalService
{
    Task<(bool success, string? error)> SendVerificationCodeAsync(string phoneNumber);
    Task<(PatientAuthResponse? response, string? error)> VerifyCodeAsync(string phoneNumber, string code);
    Task<PatientPortalDashboardDto> GetDashboardAsync(Guid patientId);
    Task<List<PatientAppointmentDto>> GetAppointmentsAsync(Guid patientId, int limit = 20);
    Task<(PatientAppointmentDto? result, string? error)> RequestAppointmentAsync(Guid patientId, PatientAppointmentRequestDto req);
    Task<(bool success, string? error)> CancelAppointmentAsync(Guid patientId, Guid appointmentId);
    Task<List<PatientTreatmentDto>> GetTreatmentsAsync(Guid patientId, int limit = 20);
    Task<List<PatientPrescriptionDto>> GetPrescriptionsAsync(Guid patientId, int limit = 20);
    Task<PatientFinancialSummaryDto> GetFinancialSummaryAsync(Guid patientId);
    Task<Guid?> GetPatientIdByPhoneAsync(string phoneNumber);
}
