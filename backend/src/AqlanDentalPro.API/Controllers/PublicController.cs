using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicController(AppDbContext db) => _db = db;

    [HttpGet("public/queue")]
    [AllowAnonymous]
    public async Task<IActionResult> GetQueue()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == today && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.StartTime)
            .Select(a => new
            {
                a.Id,
                PatientDisplayName = a.Patient.FirstName + " " + a.Patient.LastName.Substring(0, 1) + ".",
                AppointmentType = a.AppointmentType ?? "—",
                StartTime = a.StartTime.ToString(@"hh\:mm"),
                EndTime = (string?)a.EndTime.ToString(@"hh\:mm"),
                DoctorName = (string?)(a.Doctor != null ? a.Doctor.Name : null),
                DoctorColor = (string?)(a.Doctor != null ? a.Doctor.Color : null),
                Status = a.Status.ToString()
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>قائمة الأطباء العامة (للحجز العام)</summary>
    [HttpGet("public/doctors")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDoctors()
    {
        var doctors = await _db.Doctors
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Specialty, d.Color, d.AvatarInitials })
            .ToListAsync();
        return Ok(doctors);
    }

    /// <summary>الأوقات المتاحة لطبيب في يوم محدد (للحجز العام)</summary>
    [HttpGet("public/doctors/{doctorId:guid}/slots")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableSlots(Guid doctorId, [FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var dateOnly))
            return BadRequest(new { message = "تنسيق التاريخ غير صحيح" });

        if (dateOnly < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "لا يمكن الحجز في تاريخ سابق" });

        var dotnetDow = (int)dateOnly.DayOfWeek;
        var schedule = await _db.DoctorSchedules
            .FirstOrDefaultAsync(ds => ds.DoctorId == doctorId && ds.DayOfWeek == dotnetDow
                && ds.IsActive && ds.IsWorking);

        if (schedule is null)
            return Ok(new { available = false, slots = Array.Empty<string>() });

        var bookedSlots = await _db.Appointments
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate == dateOnly && a.IsActive
                && a.Status != AppointmentStatus.Cancelled
                && a.Status != AppointmentStatus.NoShow)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var slots = new List<string>();
        var current = schedule.StartTime;
        var slotMinutes = schedule.SlotDurationMinutes;

        while (current.AddMinutes(slotMinutes) <= schedule.EndTime)
        {
            if (schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue &&
                current >= schedule.BreakStart.Value && current < schedule.BreakEnd.Value)
            {
                current = schedule.BreakEnd.Value;
                continue;
            }

            var slotEnd = current.AddMinutes(slotMinutes);
            var isBooked = bookedSlots.Any(b => current < b.EndTime && slotEnd > b.StartTime);
            if (!isBooked) slots.Add(current.ToString("HH:mm"));
            current = current.AddMinutes(slotMinutes);
        }

        return Ok(new { available = true, slots, slotDuration = schedule.SlotDurationMinutes });
    }
}
