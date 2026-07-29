using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Doctor room assignments ("تعيينات غرف الأطباء"): resolves a doctor's standing room
/// name for the clinic-queue call flow. Extracted from ClinicQueueController so the
/// fallback logic is unit-testable — the call/recall endpoints themselves can't run
/// under EF InMemory (advisory-lock raw SQL), same constraint as SurgeryController.Create.
/// Static + AppDbContext parameter follows the PermissionGuard precedent.
/// </summary>
public static class DoctorRoomResolver
{
    /// <summary>
    /// Returns the ArabicName of the doctor's default clinic room, or null when the
    /// doctor is unset, has no assignment, or the assigned room was soft-deleted
    /// (the global IsActive query filter nulls the navigation for deleted rooms).
    /// </summary>
    public static async Task<string?> ResolveDefaultRoomNameAsync(AppDbContext db, Guid? doctorId)
    {
        if (!doctorId.HasValue) return null;

        return await db.Doctors
            .Where(d => d.Id == doctorId.Value && d.DefaultClinicRoom != null)
            .Select(d => d.DefaultClinicRoom!.ArabicName)
            .FirstOrDefaultAsync();
    }
}
