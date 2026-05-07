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
[Authorize]
public class DoctorSchedulesController(AppDbContext db) : ControllerBase
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
    public async Task<IActionResult> SetDaySchedule(
        Guid doctorId, int dayOfWeek, [FromBody] DoctorScheduleRequest req)
    {
        if (dayOfWeek is < 0 or > 6)
            return BadRequest(new { message = "يجب أن يكون رقم اليوم بين 0 و 6 (الأحد=0)" });

        var existing = await db.DoctorSchedules
            .FirstOrDefaultAsync(ds => ds.DoctorId == doctorId && ds.DayOfWeek == dayOfWeek && ds.IsActive);

        if (existing is null)
        {
            var schedule = new Domain.Entities.DoctorSchedule
            {
                DoctorId = doctorId,
                DayOfWeek = dayOfWeek,
                StartTime = TimeOnly.Parse(req.StartTime),
                EndTime = TimeOnly.Parse(req.EndTime),
                IsWorking = req.IsWorking,
                BreakStart = req.BreakStart != null ? TimeOnly.Parse(req.BreakStart) : null,
                BreakEnd = req.BreakEnd != null ? TimeOnly.Parse(req.BreakEnd) : null,
                SlotDurationMinutes = req.SlotDurationMinutes ?? 30
            };
            db.DoctorSchedules.Add(schedule);
        }
        else
        {
            existing.StartTime = TimeOnly.Parse(req.StartTime);
            existing.EndTime = TimeOnly.Parse(req.EndTime);
            existing.IsWorking = req.IsWorking;
            existing.BreakStart = req.BreakStart != null ? TimeOnly.Parse(req.BreakStart) : null;
            existing.BreakEnd = req.BreakEnd != null ? TimeOnly.Parse(req.BreakEnd) : null;
            existing.SlotDurationMinutes = req.SlotDurationMinutes ?? 30;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "تم حفظ جدول العمل بنجاح" });
    }

    /// <summary>Batch-set schedule for all days at once.</summary>
    [HttpPut]
    public async Task<IActionResult> SetFullSchedule(
        Guid doctorId, [FromBody] List<DoctorScheduleRequest> scheduleDays)
    {
        foreach (var req in scheduleDays)
        {
            if (req.DayOfWeek is < 0 or > 6)
                return BadRequest(new { message = $"يوم غير صالح: {req.DayOfWeek}" });

            var existing = await db.DoctorSchedules
                .FirstOrDefaultAsync(ds => ds.DoctorId == doctorId && ds.DayOfWeek == req.DayOfWeek && ds.IsActive);

            if (existing is null)
            {
                db.DoctorSchedules.Add(new Domain.Entities.DoctorSchedule
                {
                    DoctorId = doctorId,
                    DayOfWeek = req.DayOfWeek,
                    StartTime = TimeOnly.Parse(req.StartTime),
                    EndTime = TimeOnly.Parse(req.EndTime),
                    IsWorking = req.IsWorking,
                    BreakStart = req.BreakStart != null ? TimeOnly.Parse(req.BreakStart) : null,
                    BreakEnd = req.BreakEnd != null ? TimeOnly.Parse(req.BreakEnd) : null,
                    SlotDurationMinutes = req.SlotDurationMinutes ?? 30
                });
            }
            else
            {
                existing.StartTime = TimeOnly.Parse(req.StartTime);
                existing.EndTime = TimeOnly.Parse(req.EndTime);
                existing.IsWorking = req.IsWorking;
                existing.BreakStart = req.BreakStart != null ? TimeOnly.Parse(req.BreakStart) : null;
                existing.BreakEnd = req.BreakEnd != null ? TimeOnly.Parse(req.BreakEnd) : null;
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
