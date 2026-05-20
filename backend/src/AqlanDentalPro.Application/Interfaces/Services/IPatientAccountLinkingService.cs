namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Unified service for linking PatientAccounts to messaging User entities.
/// Eliminates the duplicated EnsureLinkedUserAsync logic that was previously
/// split between PatientPortalService and PatientPortalMessagingService.
/// </summary>
public interface IPatientAccountLinkingService
{
    /// <summary>
    /// Ensures a PatientAccount has a LinkedUserId for messaging system integration.
    /// If already linked, returns the existing LinkedUserId.
    /// If not linked, finds an existing User by username or creates a new one,
    /// then links it to the PatientAccount.
    /// </summary>
    /// <param name="patientId">The patient ID whose account should be linked</param>
    /// <returns>The LinkedUserId, or null if no PatientAccount exists for this patient</returns>
    Task<Guid?> EnsureLinkedUserAsync(Guid patientId);
}
