using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CheckConflictRequest
{
    public Guid DoctorId { get; init; }
    public string Date { get; init; } = string.Empty;
    public string StartTime { get; init; } = string.Empty;
    public int DurationMinutes { get; init; }
    public Guid? ExcludeId { get; init; }
}

[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController(AppointmentService service, AppDbContext db) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IActionResult> GetToday([FromQuery] Guid? doctorId)
    {
        var list = await service.GetTodayAsync(doctorId);
        return Ok(list);
    }

    [HttpGet]
    public async Task<IActionResult> GetByRange(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? startDate,  // alias for from
        [FromQuery] string? endDate,    // alias for to
        [FromQuery] Guid? doctorId)
    {
        var fromDateStr = from ?? startDate;
        var toDateStr = to ?? endDate;
        var fromDate = fromDateStr != null ? DateOnly.Parse(fromDateStr) : DateOnly.FromDateTime(DateTime.Today);
        var toDate = toDateStr != null ? DateOnly.Parse(toDateStr) : fromDate;

        var list = await service.GetByDateRangeAsync(fromDate, toDate, doctorId);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AppointmentDto>> GetById(Guid id)
    {
        var result = await service.GetByIdAsync(id);
        return result == null ? NotFound(new { message = "الموعد غير موجود" }) : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create([FromBody] CreateAppointmentRequest req)
    {
        var (result, error) = await service.CreateAsync(req);
        if (error != null)
            return Conflict(new { message = error });
        return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AppointmentDto>> Update(Guid id, [FromBody] CreateAppointmentRequest req)
    {
        var (result, error) = await service.UpdateAsync(id, req);
        if (error != null)
            return error.Contains("تعارض") ? Conflict(new { message = error }) : NotFound(new { message = error });
        return Ok(result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppointmentStatusRequest req)
    {
        var (result, error) = await service.UpdateStatusAsync(id, req.Status);
        if (error != null) return BadRequest(new { message = error });
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("check-conflict")]
    public async Task<IActionResult> CheckConflict([FromBody] CheckConflictRequest req)
    {
        var appointmentDate = DateOnly.Parse(req.Date);
        var startTime = TimeOnly.Parse(req.StartTime);
        var endTime = startTime.AddMinutes(req.DurationMinutes);

        var query = db.Appointments
            .Where(a => a.DoctorId == req.DoctorId
                      && a.AppointmentDate == appointmentDate
                      && a.Status != AppointmentStatus.Cancelled);

        if (req.ExcludeId.HasValue)
            query = query.Where(a => a.Id != req.ExcludeId.Value);

        var conflicts = await query.ToListAsync();

        var hasConflict = conflicts.Any(a =>
        {
            var aStart = a.StartTime;
            var aEnd = aStart.AddMinutes(a.DurationMinutes);
            return startTime < aEnd && endTime > aStart;
        });

        return Ok(new { hasConflict });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound(new { message = "الموعد غير موجود" });

        appointment.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الموعد بنجاح" });
    }
}
