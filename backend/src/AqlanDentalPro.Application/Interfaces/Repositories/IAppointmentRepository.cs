using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IAppointmentRepository : IGenericRepository<Appointment>
{
    Task<bool> HasConflictAsync(Guid doctorId, DateOnly date, TimeOnly start, TimeOnly end, Guid? excludeId = null);
    Task<IEnumerable<Appointment>> GetTodayAsync(Guid? branchId, Guid? doctorId);
    Task<IEnumerable<Appointment>> GetByDateRangeAsync(DateOnly from, DateOnly to, Guid? branchId, Guid? doctorId);
}
