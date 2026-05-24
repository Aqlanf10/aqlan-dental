using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Enforces per-role patient data access rules.
/// Doctors (Orthodontist / GeneralDentist / OralSurgeon) may only see patients
/// linked to them via appointment, visit, treatment-plan step, or primary-doctor assignment.
/// All other staff roles receive full access.
/// </summary>
public interface IPatientAccessService
{
    /// <summary>True when the current user has a doctor role.</summary>
    bool IsDoctor { get; }

    /// <summary>
    /// True when the current user is allowed to see the full profile
    /// (phone, address, financial data).  Doctors always receive the limited clinical view.
    /// </summary>
    bool HasFullAccess { get; }

    /// <summary>
    /// Returns the Doctor.Id for the currently logged-in user, or null if they are
    /// not a doctor or have no Doctor record in the database.
    /// </summary>
    Task<Guid?> GetCurrentDoctorIdAsync();

    /// <summary>
    /// Returns true when the current user may access the given patient.
    /// For non-doctor roles always returns true.
    /// For doctor roles queries all doctor-patient links (primary doctor, appointments,
    /// visits, treatment-plan steps).
    /// </summary>
    Task<bool> CanAccessPatientAsync(Guid patientId);

    /// <summary>
    /// Returns the set of patient IDs that the current doctor is linked to.
    /// Returns null for non-doctor roles (meaning "no filter required").
    /// </summary>
    Task<HashSet<Guid>?> GetAccessiblePatientIdsAsync();
}
