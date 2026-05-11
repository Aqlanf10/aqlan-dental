using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateDoctorRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Specialty { get; init; }
    public string? LicenseNumber { get; init; }
    public Guid? BranchId { get; init; }
    public string? Color { get; init; }
    public string? AvatarInitials { get; init; }
    public Guid? UserId { get; init; }
    public CompensationType? CompensationType { get; init; }
    public decimal? DefaultCommissionPercentage { get; init; }
    public string? CompensationNotes { get; init; }
}

public sealed class UpdateDoctorRequest
{
    public string? Name { get; init; }
    public string? Specialty { get; init; }
    public string? LicenseNumber { get; init; }
    public Guid? BranchId { get; init; }
    public string? Color { get; init; }
    public string? AvatarInitials { get; init; }
    public CompensationType? CompensationType { get; init; }
    public decimal? DefaultCommissionPercentage { get; init; }
    public string? CompensationNotes { get; init; }
}

[ApiController]
[Route("api/doctors")]
[Authorize]
public class DoctorsController(
    AppDbContext db,
    ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/doctors — List doctors with optional filters.
    /// Default behavior (status=active) is backward compatible with the original endpoint.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? branchId)
    {
        var normalizedStatus = status?.ToLower();

        // When status is active (default), use the normal query which respects
        // the global soft-delete filter (IsActive == true) — backward compatible.
        IQueryable<Doctor> query = normalizedStatus switch
        {
            "inactive" or "all" => db.Doctors.IgnoreQueryFilters(),
            _ => db.Doctors
        };

        // Apply status filter
        query = normalizedStatus switch
        {
            "active" => query.Where(d => d.IsActive),
            "inactive" => query.Where(d => !d.IsActive),
            // "all" or default (no filter on IsActive — for "all" show everything,
            // for default/null the global filter already ensures only active)
            "all" => query,
            _ => query // default: global filter handles IsActive
        };

        // Apply branch filter
        if (branchId.HasValue)
            query = query.Where(d => d.BranchId == branchId.Value);

        var doctors = await query
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Specialty,
                d.Color,
                d.AvatarInitials,
                d.BranchId,
                // New fields (backward compat — added, not removed)
                d.LicenseNumber,
                d.IsActive,
                CompensationType = d.CompensationType.ToString(),
                d.DefaultCommissionPercentage,
                d.CompensationNotes,
                BranchName = db.Branches
                    .Where(b => b.Id == d.BranchId)
                    .Select(b => b.Name)
                    .FirstOrDefault(),
                UserName = db.Users
                    .Where(u => u.Id == d.UserId)
                    .Select(u => u.Username)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(doctors);
    }

    /// <summary>
    /// GET /api/doctors/{id} — Get single doctor with full details.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var doctor = await db.Doctors.IgnoreQueryFilters()
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Specialty,
                d.LicenseNumber,
                d.BranchId,
                d.Color,
                d.AvatarInitials,
                d.UserId,
                d.IsActive,
                CompensationType = d.CompensationType.ToString(),
                d.DefaultCommissionPercentage,
                d.CompensationNotes,
                d.CreatedAt,
                d.UpdatedAt,
                BranchName = db.Branches
                    .Where(b => b.Id == d.BranchId)
                    .Select(b => b.Name)
                    .FirstOrDefault(),
                UserName = db.Users
                    .Where(u => u.Id == d.UserId)
                    .Select(u => u.Username)
                    .FirstOrDefault(),
                ScheduleCount = db.DoctorSchedules.Count(s => s.DoctorId == d.Id)
            })
            .FirstOrDefaultAsync();

        if (doctor is null)
            return NotFound(new { message = "الطبيب غير موجود" });

        return Ok(doctor);
    }

    /// <summary>
    /// POST /api/doctors — Create a new doctor (AdminOnly).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateDoctorRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "اسم الطبيب مطلوب" });

        // If UserId provided, verify the user exists
        if (req.UserId.HasValue && req.UserId.Value != Guid.Empty)
        {
            var userExists = await db.Users.AnyAsync(u => u.Id == req.UserId.Value);
            if (!userExists)
                return BadRequest(new { message = "المستخدم المحدد غير موجود" });
        }

        var initials = !string.IsNullOrWhiteSpace(req.AvatarInitials)
            ? req.AvatarInitials
            : GenerateInitials(req.Name);

        var doctor = new Doctor
        {
            Name = req.Name.Trim(),
            Specialty = req.Specialty?.Trim(),
            LicenseNumber = req.LicenseNumber?.Trim(),
            BranchId = req.BranchId,
            Color = !string.IsNullOrWhiteSpace(req.Color) ? req.Color : "#0E7490",
            AvatarInitials = initials,
            UserId = req.UserId ?? Guid.Empty,
            CompensationType = req.CompensationType ?? CompensationType.None,
            DefaultCommissionPercentage = req.DefaultCommissionPercentage,
            CompensationNotes = req.CompensationNotes?.Trim()
        };

        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        return Created($"/api/doctors/{doctor.Id}", new
        {
            doctor.Id,
            doctor.Name,
            doctor.Specialty,
            doctor.LicenseNumber,
            doctor.BranchId,
            doctor.Color,
            doctor.AvatarInitials,
            doctor.UserId,
            doctor.IsActive,
            CompensationType = doctor.CompensationType.ToString(),
            doctor.DefaultCommissionPercentage,
            doctor.CompensationNotes
        });
    }

    /// <summary>
    /// PUT /api/doctors/{id} — Update doctor (AdminOnly). All fields optional for partial update.
    /// Cannot change UserId (linked user is immutable after creation).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDoctorRequest req)
    {
        var doctor = await db.Doctors.FindAsync(id);
        if (doctor is null)
            return NotFound(new { message = "الطبيب غير موجود" });

        // Track whether Name changed for auto-suggesting initials
        var nameChanged = false;

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "اسم الطبيب مطلوب" });
            doctor.Name = req.Name.Trim();
            nameChanged = true;
        }

        if (req.Specialty is not null)
            doctor.Specialty = req.Specialty.Trim();

        if (req.LicenseNumber is not null)
            doctor.LicenseNumber = req.LicenseNumber.Trim();

        if (req.BranchId.HasValue)
            doctor.BranchId = req.BranchId.Value;

        if (req.Color is not null)
            doctor.Color = req.Color;

        // Auto-suggest new initials if Name changed and AvatarInitials not explicitly provided
        if (req.AvatarInitials is not null)
        {
            doctor.AvatarInitials = req.AvatarInitials;
        }
        else if (nameChanged)
        {
            doctor.AvatarInitials = GenerateInitials(doctor.Name);
        }

        if (req.CompensationType.HasValue)
            doctor.CompensationType = req.CompensationType.Value;

        if (req.DefaultCommissionPercentage.HasValue)
            doctor.DefaultCommissionPercentage = req.DefaultCommissionPercentage;

        if (req.CompensationNotes is not null)
            doctor.CompensationNotes = req.CompensationNotes.Trim();

        await db.SaveChangesAsync();

        return Ok(new
        {
            doctor.Id,
            doctor.Name,
            doctor.Specialty,
            doctor.LicenseNumber,
            doctor.BranchId,
            doctor.Color,
            doctor.AvatarInitials,
            doctor.UserId,
            doctor.IsActive,
            CompensationType = doctor.CompensationType.ToString(),
            doctor.DefaultCommissionPercentage,
            doctor.CompensationNotes
        });
    }

    /// <summary>
    /// PUT /api/doctors/{id}/status — Toggle doctor active/inactive (AdminOnly).
    /// When deactivating, checks for future active appointments.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var doctor = await db.Doctors.FindAsync(id);
        if (doctor is null)
            return NotFound(new { message = "الطبيب غير موجود" });

        if (doctor.IsActive)
        {
            // Deactivating — check if doctor has future active appointments
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var hasFutureAppointments = await db.Appointments
                .AnyAsync(a => a.DoctorId == id
                    && a.AppointmentDate >= today
                    && a.Status != AppointmentStatus.Cancelled
                    && a.Status != AppointmentStatus.NoShow);

            if (hasFutureAppointments)
                return Conflict(new { message = "لا يمكن تعطيل الطبيب لأنه لديه مواعيد مستقبلية نشطة. قم بإلغاء أو إعادة جدولة المواعيد أولاً" });
        }

        doctor.IsActive = !doctor.IsActive;
        await db.SaveChangesAsync();

        return Ok(new { message = doctor.IsActive ? "تم تفعيل الطبيب بنجاح" : "تم تعطيل الطبيب بنجاح" });
    }

    /// <summary>
    /// DELETE /api/doctors/{id} — Soft-delete doctor (AdminOnly).
    /// Checks for linked patients, appointments, visits, payments before deleting.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doctor = await db.Doctors.FindAsync(id);
        if (doctor is null)
            return NotFound(new { message = "الطبيب غير موجود" });

        // Check for linked data
        var hasPatients = await db.Patients.AnyAsync(p => p.PrimaryDoctorId == id);
        var hasAppointments = await db.Appointments.AnyAsync(a => a.DoctorId == id);
        var hasVisits = await db.Visits.AnyAsync(v => v.DoctorId == id);
        var hasPayments = await db.Payments.AnyAsync(p => p.DoctorId == id);

        if (hasPatients || hasAppointments || hasVisits || hasPayments)
            return Conflict(new { message = "لا يمكن حذف الطبيب لأنه مرتبط ببيانات نشطة. استخدم التعطيل بدلاً من الحذف" });

        doctor.IsActive = false;
        doctor.DeletedAt = DateTime.UtcNow;
        doctor.DeletedBy = currentUser.UserId;

        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف الطبيب بنجاح" });
    }

    /// <summary>
    /// Generate avatar initials from the first letter of the first word in the name.
    /// </summary>
    private static string GenerateInitials(string name)
    {
        var firstWord = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return !string.IsNullOrEmpty(firstWord)
            ? firstWord[..1]
            : "د";
    }
}
