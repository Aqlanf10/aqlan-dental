using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IAppointmentRepository : IGenericRepository<Appointment>
{
    Task<bool> HasConflictAsync(Guid doctorId, DateOnly date, TimeOnly start, TimeOnly end, Guid? excludeId = null);

    /// <summary>
    /// Atomically books the appointment: inside a transaction + per-doctor advisory
    /// lock it re-checks for a time conflict and inserts only if clear. Returns
    /// false when a conflict is detected (caller surfaces the Arabic message).
    /// Closes the double-booking race (C-15) between conflict-check and insert.
    /// </summary>
    Task<bool> TryCreateWithConflictGuardAsync(Appointment appointment);
    Task<IEnumerable<Appointment>> GetTodayAsync(Guid? branchId, Guid? doctorId);
    Task<IEnumerable<Appointment>> GetByDateRangeAsync(DateOnly from, DateOnly to, Guid? branchId, Guid? doctorId, Guid? patientId = null, AppointmentStatus? status = null);
    Task<Appointment?> GetWithDetailAsync(Guid id);
    Task<IEnumerable<Appointment>> GetByPatientAsync(Guid patientId);
}
