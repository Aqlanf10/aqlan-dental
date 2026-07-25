using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Canonical guard for creating records that belong to a patient.
/// The AppDbContext global soft-delete filter means this query accepts only
/// an existing, active patient and rejects missing or archived files.
/// </summary>
public static class ActivePatientWriteGuard
{
    public const string ErrorMessage = "لا يمكن إضافة سجل لمريض غير موجود أو مؤرشف";

    public static Task<bool> ExistsAsync(
        AppDbContext db,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        if (patientId == Guid.Empty) return Task.FromResult(false);

        return db.Patients
            .AsNoTracking()
            .AnyAsync(patient => patient.Id == patientId, cancellationToken);
    }

    public static async Task EnsureAsync(
        AppDbContext db,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        if (!await ExistsAsync(db, patientId, cancellationToken))
            throw new ArgumentException(ErrorMessage);
    }
}
