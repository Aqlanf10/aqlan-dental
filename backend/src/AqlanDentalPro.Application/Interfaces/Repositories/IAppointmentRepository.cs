using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IAppointmentRepository : IGenericRepository<Appointment>
{
    Task<bool> HasConflictAsync(Guid doctorId, DateOnly date, TimeOnly start, TimeOnly end, Guid? excludeId = null);

    /// <summary>CORE-APPT-003: same shape as <see cref="HasConflictAsync"/>, scoped to a clinic room.</summary>
    Task<bool> HasRoomConflictAsync(Guid roomId, DateOnly date, TimeOnly start, TimeOnly end, Guid? excludeId = null);

    /// <summary>
    /// Atomically books the appointment: inside a transaction + per-doctor (and,
    /// when a room is set, per-room) advisory lock it re-checks for a doctor or
    /// room time conflict and inserts only if clear. Returns false when a conflict
    /// is detected (caller surfaces the Arabic message). Closes the double-booking
    /// race (C-15) between conflict-check and insert, and (CORE-APPT-003) the
    /// analogous race for two different doctors booking the same room.
    /// </summary>
    Task<bool> TryCreateWithConflictGuardAsync(Appointment appointment);

    /// <summary>
    /// CORE-APPT-002/003: same atomic guard as <see cref="TryCreateWithConflictGuardAsync"/>,
    /// for rescheduling an existing appointment. The caller must have already assigned
    /// the appointment's new DoctorId/AppointmentDate/StartTime/EndTime/ClinicRoomId on
    /// the tracked entity before calling this — the conflict check and the save both
    /// read those (already-mutated) values, excluding the appointment's own id. Returns
    /// false (without saving) when a doctor or room conflict is detected, closing the
    /// double-booking race between check and save that a plain
    /// HasConflictAsync-then-SaveChanges has.
    /// </summary>
    Task<bool> TryUpdateWithConflictGuardAsync(Appointment appointment);

    /// <summary>
    /// Returns appointments for the clinic-local day. Production callers omit
    /// <paramref name="clinicDate"/> and use the configured clinic timezone; tests
    /// may pass an explicit date to verify midnight-boundary behavior deterministically.
    /// </summary>
    Task<IEnumerable<Appointment>> GetTodayAsync(
        Guid? branchId,
        Guid? doctorId,
        DateOnly? clinicDate = null);

    Task<IEnumerable<Appointment>> GetByDateRangeAsync(DateOnly from, DateOnly to, Guid? branchId, Guid? doctorId, Guid? patientId = null, AppointmentStatus? status = null);
    Task<Appointment?> GetWithDetailAsync(Guid id);
    Task<IEnumerable<Appointment>> GetByPatientAsync(Guid patientId);
}