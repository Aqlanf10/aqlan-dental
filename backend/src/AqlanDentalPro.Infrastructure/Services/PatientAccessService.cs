using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Determines which patients the current user may view.
/// Doctor roles (Orthodontist / GeneralDentist / OralSurgeon) are restricted to patients
/// they are linked to.  All other staff roles have unrestricted access.
/// </summary>
public class PatientAccessService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IConfiguration configuration,
    ILogger<PatientAccessService> logger) : IPatientAccessService
{
    private static readonly HashSet<UserRole> DoctorRoles =
    [
        UserRole.Orthodontist,
        UserRole.GeneralDentist,
        UserRole.OralSurgeon,
    ];

    private bool IsDoctorPatientScopingEnabled =>
        configuration.GetValue("Security:EnableDoctorPatientScoping", false);

    public bool IsDoctor =>
        IsDoctorPatientScopingEnabled &&
        currentUser.Role.HasValue &&
        DoctorRoles.Contains(currentUser.Role.Value);

    public bool HasFullAccess => !IsDoctor;

    public async Task<Guid?> GetCurrentDoctorIdAsync()
    {
        if (!IsDoctor || currentUser.UserId == null)
            return null;

        var userId = currentUser.UserId.Value;
        return await db.Doctors
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CanAccessPatientAsync(Guid patientId)
    {
        if (!IsDoctor)
            return true;

        var doctorId = await GetCurrentDoctorIdAsync();
        if (doctorId == null)
        {
            logger.LogWarning(
                "Patient access denied: user {UserId} has a doctor role but no Doctor record — denying access to patient {PatientId}",
                currentUser.UserId, patientId);
            return false;
        }

        var d = doctorId.Value;

        var patientExists = await db.Patients
            .AnyAsync(p => p.Id == patientId && p.IsActive);
        if (!patientExists)
            return false;

        // Check primary doctor assignment
        if (await db.Patients.AnyAsync(p => p.Id == patientId && p.PrimaryDoctorId == d))
            return true;

        // Check appointment link
        if (await db.Appointments.AnyAsync(a => a.PatientId == patientId && a.DoctorId == d && a.IsActive))
            return true;

        // Check visit link
        if (await db.Visits.AnyAsync(v => v.PatientId == patientId && v.DoctorId == d && v.IsActive))
            return true;

        // Check treatment plan step link
        if (await db.PatientTreatmentPlanSteps.AnyAsync(s => s.PatientId == patientId && s.ResponsibleDoctorId == d && s.IsActive))
            return true;

        // Check internal referral link (doctor is the recipient of an active referral)
        if (await db.InternalReferrals.AnyAsync(r => r.PatientId == patientId && r.ToDoctorId == d && r.IsActive))
            return true;

        return false;
    }

    public async Task<HashSet<Guid>?> GetAccessiblePatientIdsAsync()
    {
        if (!IsDoctor)
            return null;

        var doctorId = await GetCurrentDoctorIdAsync();
        if (doctorId == null)
            return [];

        var d = doctorId.Value;
        var ids = new HashSet<Guid>();

        // HOTFIX PR165: Replace fragile IQueryable.Union() with sequential ToListAsync queries.
        // The 5-way Union() fails on PostgreSQL when EF Core global soft-delete query filters
        // are active, causing HTTP 500 for doctors on GET /api/patients.
        // Each query is now simple and independently translatable by EF Core/Npgsql.
        // Each query is wrapped in try-catch for resilience against missing tables
        // (e.g. if a migration hasn't been applied yet).

        // 1. Patients where this doctor is the primary doctor
        await SafeAddIdsAsync(ids, async () => await db.Patients
            .Where(p => p.PrimaryDoctorId == d && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(), "Patients.PrimaryDoctorId");

        // 2. Patients linked via appointments
        await SafeAddIdsAsync(ids, async () => await db.Appointments
            .Where(a => a.DoctorId == d && a.IsActive)
            .Select(a => a.PatientId)
            .ToListAsync(), "Appointments.DoctorId");

        // 3. Patients linked via visits
        await SafeAddIdsAsync(ids, async () => await db.Visits
            .Where(v => v.DoctorId == d && v.IsActive)
            .Select(v => v.PatientId)
            .ToListAsync(), "Visits.DoctorId");

        // 4. Patients linked via treatment plan steps
        await SafeAddIdsAsync(ids, async () => await db.PatientTreatmentPlanSteps
            .Where(s => s.ResponsibleDoctorId == d && s.IsActive)
            .Select(s => s.PatientId)
            .ToListAsync(), "PatientTreatmentPlanSteps.ResponsibleDoctorId");

        // 5. Patients linked via internal referrals (doctor is the recipient)
        await SafeAddIdsAsync(ids, async () => await db.InternalReferrals
            .Where(r => r.ToDoctorId == d && r.IsActive)
            .Select(r => r.PatientId)
            .ToListAsync(), "InternalReferrals.ToDoctorId");

        return ids;
    }

    /// <summary>
    /// Safely executes a query and adds results to the hash set.
    /// If the query fails (e.g. table doesn't exist due to pending migration),
    /// logs a warning and continues without crashing.
    /// </summary>
    private async Task SafeAddIdsAsync(HashSet<Guid> ids, Func<Task<List<Guid>>> query, string label)
    {
        try
        {
            var result = await query();
            foreach (var id in result) ids.Add(id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PatientAccessService: Query for {Label} failed (table may not exist yet). Skipping.",
                label);
        }
    }
}
