using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Repositories;

public class AppointmentRepository(AppDbContext context)
    : GenericRepository<Appointment>(context), IAppointmentRepository
{
    public async Task<bool> HasConflictAsync(
        Guid doctorId, DateOnly date, TimeOnly start, TimeOnly end, Guid? excludeId = null)
    {
        return await DbSet.AnyAsync(a =>
            a.DoctorId == doctorId &&
            a.AppointmentDate == date &&
            a.IsActive &&
            a.Status != AppointmentStatus.Cancelled &&
            a.Status != AppointmentStatus.NoShow &&
            a.StartTime < end &&
            a.EndTime > start &&
            (excludeId == null || a.Id != excludeId));
    }

    public async Task<bool> TryCreateWithConflictGuardAsync(Appointment appointment)
    {
        // Non-relational providers (InMemory tests) have no advisory lock — fall
        // back to a plain check + insert. Logic is identical; only the cross-
        // process race protection (which needs PostgreSQL) is unavailable.
        if (!Context.Database.IsRelational())
        {
            if (await HasConflictAsync(appointment.DoctorId, appointment.AppointmentDate, appointment.StartTime, appointment.EndTime))
                return false;
            await DbSet.AddAsync(appointment);
            await Context.SaveChangesAsync();
            return true;
        }

        await using var tx = await Context.Database.BeginTransactionAsync();
        try
        {
            // Advisory lock scoped to the doctor serializes concurrent bookings for
            // the same doctor, making the conflict-check + insert atomic (C-15).
            var lockKey = (int)(appointment.DoctorId.GetHashCode() % 100000);
            await Context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            if (await HasConflictAsync(appointment.DoctorId, appointment.AppointmentDate, appointment.StartTime, appointment.EndTime))
            {
                await tx.RollbackAsync();
                return false;
            }

            await DbSet.AddAsync(appointment);
            await Context.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<Appointment>> GetTodayAsync(Guid? branchId, Guid? doctorId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = DbSet
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == today);

        if (branchId.HasValue) query = query.Where(a => a.BranchId == branchId);
        if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId);

        return await query.OrderBy(a => a.StartTime).ToListAsync();
    }

    public async Task<IEnumerable<Appointment>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, Guid? branchId, Guid? doctorId, Guid? patientId = null, AppointmentStatus? status = null)
    {
        var query = DbSet
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate >= from && a.AppointmentDate <= to);

        if (branchId.HasValue) query = query.Where(a => a.BranchId == branchId);
        if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId);
        if (patientId.HasValue) query = query.Where(a => a.PatientId == patientId);
        // GAP-01 FIX: Apply status filter at DB level instead of in-memory
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);

        return await query.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).ToListAsync();
    }

    public async Task<Appointment?> GetWithDetailAsync(Guid id) =>
        await DbSet
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<IEnumerable<Appointment>> GetByPatientAsync(Guid patientId) =>
        await DbSet
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.StartTime)
            .ToListAsync();
}
