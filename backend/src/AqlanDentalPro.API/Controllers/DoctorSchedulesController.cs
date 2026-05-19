using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Doctor schedule (working hours) management.
/// Sprint 5: Appointments enhancement.
/// </summary>
[ApiController]
[Route("api/doctors/{doctorId:guid}/schedule")]
[Authorize(Policy = "StaffOnly")]
public class DoctorSchedulesController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Get schedule for a specific doctor.</summary>
    [HttpGet]
    public async Task<IActionResult> GetSchedule(Guid doctorId)
    {
        var schedules = await db.DoctorSchedules
            .Where(ds => ds.DoctorId == doctorId && ds.IsActive)
            .OrderBy(ds => ds.DayOfWeek)
            .Select(ds => new
            {
                ds.Id,
                ds.DoctorId,
                ds.DayOfWeek,
                StartTime = ds.StartTime.ToString("HH:mm"),
                EndTime = ds.EndTime.ToString("HH:mm"),
                ds.IsWorking,
                BreakStart = ds.BreakStart != null ? ds.BreakStart.Value.ToString("HH:mm") : null,
                BreakEnd = ds.BreakEnd != null ? ds.BreakEnd.Value.ToString("HH:mm") : null,
                ds.SlotDurationMinutes
            })
            .ToListAsync();

        return Ok(schedules);
    }

    /// <summary>Set or update schedule for a specific day.</summary>
    [HttpPut("{dayOfWeek:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetDaySchedule(
        Guid doctorId, int dayOfWeek, [FromBody] DoctorScheduleRequest req)
    {
        if (dayOfWeek is < 0 or > 6)
            return BadRequest(new { message = "يجب أن يكون رقم اليوم بين 0 و 6 (الأحد=0)" });

        var validationError = ValidateScheduleRequest(req);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var startTime = TimeOnly.Parse(req.StartTime);
        var endTime = TimeOnly.Parse(req.EndTime);
        TimeOnly? breakStart = req.BreakStart != null ? TimeOnly.Parse(req.BreakStart) : null;
        TimeOnly? breakEnd = req.BreakEnd != null ? TimeOnly.Parse(req.BreakEnd) : null;

        var validationTimeError = ValidateScheduleTimes(startTime, endTime, breakStart, breakEnd, req.SlotDurationMinutes ?? 30);
        if (validationTimeError != null)
            return BadRequest(new { message = validationTimeError });

        var existing = await db.DoctorSchedules
            .FirstOrDefaultAsync(ds => ds.DoctorId == doctorId && ds.DayOfWeek == dayOfWeek && ds.IsActive);

        if (existing is null)
        {
            var schedule = new Domain.Entities.DoctorSchedule
            {
                DoctorId = doctorId,
                DayOfWeek = dayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                IsWorking = req.IsWorking,
                BreakStart = breakStart,
                BreakEnd = breakEnd,
                SlotDurationMinutes = req.SlotDurationMinutes ?? 30
            };
            db.DoctorSchedules.Add(schedule);
        }
        else
        {
            existing.StartTime = startTime;
            existing.EndTime = endTime;
            existing.IsWorking = req.IsWorking;
            existing.BreakStart = breakStart;
            existing.BreakEnd = breakEnd;
            existing.SlotDurationMinutes = req.SlotDurationMinutes ?? 30;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "تم حفظ جدول العمل بنجاح" });
    }

    /// <summary>Batch-set schedule for all days at once.</summary>
    [HttpPut]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetFullSchedule(
        Guid doctorId, [FromBody] List<DoctorScheduleRequest> scheduleDays)
    {
        foreach (var req in scheduleDays)
        {
            if (req.DayOfWeek is < 0 or > 6)
                return BadRequest(new { message = $"يوم غير صالح: {req.DayOfWeek}" });

            var validationError = ValidateScheduleRequest(req);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            var startTime = TimeOnly.Parse(req.StartTime);
            var endTime = TimeOnly.Parse(req.EndTime);
            TimeOnly? breakStart = req.BreakStart != null ? TimeOnly.Parse(req.BreakStart) : null;
            TimeOnly? breakEnd = req.BreakEnd != null ? TimeOnly.Parse(req.BreakEnd) : null;

            var validationTimeError = ValidateScheduleTimes(startTime, endTime, breakStart, breakEnd, req.SlotDurationMinutes ?? 30);
            if (validationTimeError != null)
                return BadRequest(new { message = validationTimeError });
        }

        // Batch-load all existing schedules for this doctor in one query instead of N queries
        var existingSchedules = await db.DoctorSchedules
            .Where(ds => ds.DoctorId == doctorId && ds.IsActive)
            .ToDictionaryAsync(ds => ds.DayOfWeek, ds => ds);

        foreach (var req in scheduleDays)
        {
            var startTime = TimeOnly.Parse(req.StartTime);
            var endTime = TimeOnly.Parse(req.EndTime);
            TimeOnly? breakStart = req.BreakStart != null ? TimeOnly.Parse(req.BreakStart) : null;
            TimeOnly? breakEnd = req.BreakEnd != null ? TimeOnly.Parse(req.BreakEnd) : null;

            if (!existingSchedules.TryGetValue(req.DayOfWeek, out var existing))
            {
                db.DoctorSchedules.Add(new Domain.Entities.DoctorSchedule
                {
                    DoctorId = doctorId,
                    DayOfWeek = req.DayOfWeek,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsWorking = req.IsWorking,
                    BreakStart = breakStart,
                    BreakEnd = breakEnd,
                    SlotDurationMinutes = req.SlotDurationMinutes ?? 30
                });
            }
            else
            {
                existing.StartTime = startTime;
                existing.EndTime = endTime;
                existing.IsWorking = req.IsWorking;
                existing.BreakStart = breakStart;
                existing.BreakEnd = breakEnd;
                existing.SlotDurationMinutes = req.SlotDurationMinutes ?? 30;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "تم حفظ جدول العمل بالكامل بنجاح" });
    }

    /// <summary>Get available time slots for a doctor on a specific date.</summary>
    [HttpGet("slots")]
    public async Task<IActionResult> GetAvailableSlots(
        Guid doctorId, [FromQuery] string date)
    {
        var dateOnly = DateOnly.Parse(date);
        // .NET DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
        var dotnetDow = (int)dateOnly.DayOfWeek;

        var schedule = await db.DoctorSchedules
            .FirstOrDefaultAsync(ds => ds.DoctorId == doctorId && ds.DayOfWeek == dotnetDow && ds.IsActive && ds.IsWorking);

        if (schedule is null)
            return Ok(new { available = false, slots = Array.Empty<string>(), message = "الطبيب غير متاح في هذا اليوم" });

        // Get existing appointments for that day
        var bookedSlots = await db.Appointments
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate == dateOnly && a.IsActive
                && a.Status != Domain.Enums.AppointmentStatus.Cancelled
                && a.Status != Domain.Enums.AppointmentStatus.NoShow)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var slots = new List<string>();
        var current = schedule.StartTime;
        var end = schedule.EndTime;
        var slotMinutes = schedule.SlotDurationMinutes;

        while (current.AddMinutes(slotMinutes) <= end)
        {
            // Skip break time
            if (schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue &&
                current >= schedule.BreakStart.Value && current < schedule.BreakEnd.Value)
            {
                current = schedule.BreakEnd.Value;
                continue;
            }

            var slotEnd = current.AddMinutes(slotMinutes);

            // Check if slot conflicts with existing appointment
            var isBooked = bookedSlots.Any(b =>
                current < b.EndTime && slotEnd > b.StartTime);

            if (!isBooked)
                slots.Add(current.ToString("HH:mm"));

            current = current.AddMinutes(slotMinutes);
        }

        return Ok(new { available = true, slots, slotDuration = schedule.SlotDurationMinutes });
    }

    /// <summary>Delete a specific schedule entry (soft-delete).</summary>
    [HttpDelete("api/doctor-schedules/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        var schedule = await db.DoctorSchedules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ds => ds.Id == id);

        if (schedule is null)
            return NotFound(new { message = "جدول الدوام غير موجود" });

        schedule.IsActive = false;
        schedule.DeletedAt = DateTime.UtcNow;
        schedule.DeletedBy = currentUser.UserId;

        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف جدول الدوام بنجاح" });
    }

    /// <summary>
    /// Validate that break times are either both provided or both null.
    /// </summary>
    private static string? ValidateScheduleRequest(DoctorScheduleRequest req)
    {
        // If only one of BreakStart/BreakEnd is provided, return error
        if ((req.BreakStart != null && req.BreakEnd == null) ||
            (req.BreakStart == null && req.BreakEnd != null))
        {
            return "يجب تحديد وقتي بداية ونهاية الاستراحة معاً أو تركهما فارغين";
        }

        return null;
    }

    /// <summary>
    /// Validate schedule time constraints and slot duration.
    /// </summary>
    private static string? ValidateScheduleTimes(
        TimeOnly startTime, TimeOnly endTime,
        TimeOnly? breakStart, TimeOnly? breakEnd,
        int slotDurationMinutes)
    {
        // EndTime must be greater than StartTime
        if (endTime <= startTime)
            return "وقت الانتهاء يجب أن يكون بعد وقت البدء";

        // Break time validations (only if both are provided)
        if (breakStart.HasValue && breakEnd.HasValue)
        {
            if (breakStart.Value < startTime)
                return "وقت بداية الاستراحة يجب أن يكون ضمن ساعات العمل";

            if (breakEnd.Value > endTime)
                return "وقت نهاية الاستراحة يجب أن يكون ضمن ساعات العمل";

            if (breakEnd.Value <= breakStart.Value)
                return "وقت نهاية الاستراحة يجب أن يكون بعد وقت بدايتها";
        }

        // SlotDurationMinutes must be between 10 and 120
        if (slotDurationMinutes is < 10 or > 120)
            return "مدة الحجز يجب أن تكون بين 10 و 120 دقيقة";

        return null;
    }
}

public class DoctorScheduleRequest
{
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "17:00";
    public bool IsWorking { get; set; } = true;
    public string? BreakStart { get; set; }
    public string? BreakEnd { get; set; }
    public int? SlotDurationMinutes { get; set; }
}
