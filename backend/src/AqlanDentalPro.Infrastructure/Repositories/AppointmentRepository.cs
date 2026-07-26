using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
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

    // CORE-APPT-003: same shape as HasConflictAsync but scoped to the clinic room
    // instead of the doctor — a room can only host one appointment at a time
    // regardless of which doctor booked it.
    public async Task<bool> HasRoomConflictAsync(
        Guid roomId, DateOnly date, TimeOnly start, TimeOnly end, Guid? excludeId = null)
    {
        return await DbSet.AnyAsync(a =>
            a.ClinicRoomId == roomId &&
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
            if (await HasConflictOrRoomConflictAsync(appointment, excludeId: null))
                return false;
            await DbSet.AddAsync(appointment);
            await Context.SaveChangesAsync();
            await LoadDisplayDetailsAsync(appointment);
            return true;
        }

        await using var tx = await Context.Database.BeginTransactionAsync();
        try
        {
            // Advisory lock(s) scoped to the doctor (and the room, if any) serialize
            // concurrent bookings against the same doctor or room, making the
            // conflict-check + insert atomic (C-15, extended by CORE-APPT-003 to
            // also cover the room — a doctor-only lock doesn't stop two different
            // doctors from double-booking the same room).
            await AcquireDoctorAndRoomLocksAsync(appointment.DoctorId, appointment.ClinicRoomId);

            if (await HasConflictOrRoomConflictAsync(appointment, excludeId: null))
            {
                await tx.RollbackAsync();
                return false;
            }

            await DbSet.AddAsync(appointment);
            await Context.SaveChangesAsync();

            // SEQ-18: the create endpoint immediately maps this same entity to its DTO.
            // Load the display-only references before committing so PatientName,
            // DoctorName, and package metadata are not returned as empty values.
            // Keeping the load inside the transaction also prevents a 500-after-save
            // ghost success if enrichment itself fails.
            await LoadDisplayDetailsAsync(appointment);

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> TryUpdateWithConflictGuardAsync(Appointment appointment)
    {
        if (!Context.Database.IsRelational())
        {
            if (await HasConflictOrRoomConflictAsync(appointment, excludeId: appointment.Id))
                return false;
            DbSet.Update(appointment);
            await Context.SaveChangesAsync();
            return true;
        }

        await using var tx = await Context.Database.BeginTransactionAsync();
        try
        {
            // Same doctor+room advisory locks as TryCreateWithConflictGuardAsync —
            // serializes concurrent reschedules onto the same doctor or room, closing
            // the race between the conflict check and the save (CORE-APPT-002/003).
            await AcquireDoctorAndRoomLocksAsync(appointment.DoctorId, appointment.ClinicRoomId);

            if (await HasConflictOrRoomConflictAsync(appointment, excludeId: appointment.Id))
            {
                await tx.RollbackAsync();
                return false;
            }

            DbSet.Update(appointment);
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

    private async Task<bool> HasConflictOrRoomConflictAsync(Appointment appointment, Guid? excludeId)
    {
        if (await HasConflictAsync(appointment.DoctorId, appointment.AppointmentDate, appointment.StartTime, appointment.EndTime, excludeId))
            return true;

        return appointment.ClinicRoomId.HasValue &&
            await HasRoomConflictAsync(appointment.ClinicRoomId.Value, appointment.AppointmentDate, appointment.StartTime, appointment.EndTime, excludeId);
    }

    /// <summary>
    /// Acquires the per-doctor advisory lock and, when a room is involved, the
    /// per-room advisory lock too — always in ascending numeric key order. A
    /// doctor id and a room id can independently be the contended resource (two
    /// different doctors can collide on the same room; the same doctor can't
    /// collide with themselves on two rooms), so both must be held for the
    /// conflict-check + save to be atomic. Acquiring in a fixed, deterministic
    /// order — regardless of which key belongs to which resource — is what
    /// prevents a classic deadlock: two concurrent transactions can never end up
    /// waiting on each other's lock in a cycle if every transaction that needs
    /// N of these locks always takes them in the same relative order.
    /// </summary>
    private async Task AcquireDoctorAndRoomLocksAsync(Guid doctorId, Guid? roomId)
    {
        var doctorKey = (int)(doctorId.GetHashCode() % 100000);
        if (!roomId.HasValue)
        {
            await Context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", doctorKey);
            return;
        }

        var roomKey = (int)(roomId.Value.GetHashCode() % 100000) + 100000;
        var (first, second) = doctorKey <= roomKey ? (doctorKey, roomKey) : (roomKey, doctorKey);

        await Context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", first);
        if (second != first)
            await Context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", second);
    }

    private async Task LoadDisplayDetailsAsync(Appointment appointment)
    {
        var entry = Context.Entry(appointment);

        if (!entry.Reference(a => a.Patient).IsLoaded)
            await entry.Reference(a => a.Patient).LoadAsync();

        if (!entry.Reference(a => a.Doctor).IsLoaded)
            await entry.Reference(a => a.Doctor).LoadAsync();

        if (appointment.PackageId.HasValue && !entry.Reference(a => a.Package).IsLoaded)
            await entry.Reference(a => a.Package).LoadAsync();
    }

    public async Task<IEnumerable<Appointment>> GetTodayAsync(
        Guid? branchId,
        Guid? doctorId,
        DateOnly? clinicDate = null)
    {
        // SEQ-19: DateTime.Today follows the host/container timezone (typically UTC),
        // not the configured clinic timezone. Use the clinic-local date by default;
        // the optional explicit date keeps midnight-boundary tests deterministic.
        var today = clinicDate ?? ClinicTimeProvider.ClinicToday();
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
            .Include(a => a.Package) // YOLO-S1: eager-load for PackageName/Color in DTO
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<IEnumerable<Appointment>> GetByPatientAsync(Guid patientId) =>
        await DbSet
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.StartTime)
            .ToListAsync();
}