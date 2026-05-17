using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Policy = "StaffOnly")]
public class AppointmentsController(AppointmentService service, AppDbContext db, ICurrentUserService currentUser, IWhatsAppService whatsapp, ILogger<AppointmentsController> logger) : ControllerBase
{
    /// <summary>Check if a time slot conflicts with existing appointments</summary>
    [HttpPost("check-conflict")]
    public async Task<IActionResult> CheckConflict([FromBody] CheckConflictRequest req)
    {
        var hasConflict = await service.CheckConflictAsync(
            req.DoctorId, req.Date, req.StartTime, req.DurationMinutes, req.ExcludeId);
        return Ok(new { hasConflict });
    }

    /// <summary>Get daily appointment statistics for a specific date</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetDailyStats([FromQuery] string? date)
    {
        DateOnly targetDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            targetDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!DateOnly.TryParse(date, out targetDate))
        {
            return BadRequest(new { message = "تنسيق التاريخ غير صالح. استخدم YYYY-MM-DD" });
        }

        var stats = await service.GetDailyStatsAsync(targetDate);
        return Ok(stats);
    }

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
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] Guid? doctorId,
        [FromQuery] Guid? patientId,
        [FromQuery] string? status,
        [FromQuery] Guid? branchId)
    {
        // GAP-01 FIX: Accept both from/to (backend standard) and startDate/endDate (frontend convention)
        var fromDateStr = from ?? startDate;
        var toDateStr = to ?? endDate;

        DateOnly fromDate;
        if (fromDateStr != null)
        {
            if (!DateOnly.TryParse(fromDateStr, out fromDate))
                return BadRequest(new { message = "تنسيق تاريخ البداية غير صالح. استخدم YYYY-MM-DD" });
        }
        else
        {
            fromDate = DateOnly.FromDateTime(DateTime.Today);
        }

        DateOnly toDate;
        if (toDateStr != null)
        {
            if (!DateOnly.TryParse(toDateStr, out toDate))
                return BadRequest(new { message = "تنسيق تاريخ النهاية غير صالح. استخدم YYYY-MM-DD" });
        }
        else
        {
            toDate = fromDate;
        }

        // GAP-01 FIX: Safely parse status filter with Arabic error message
        AppointmentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
                return BadRequest(new { message = $"حالة الموعد '{status}' غير صالحة" });
            statusFilter = parsedStatus;
        }

        // GAP-01 FIX: Pass status to service for DB-level filtering (was in-memory before)
        var result = await service.GetByDateRangeAsync(fromDate, toDate, doctorId, patientId, statusFilter);

        return Ok(result);
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> GetByPatient(Guid patientId)
    {
        var list = await service.GetByPatientAsync(patientId);
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

    // ─── DELETE /api/appointments/{id} (soft-delete) ─────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف بالفعل" });

        appointment.IsActive = false;
        appointment.DeletedAt = DateTime.UtcNow;
        appointment.DeletedBy = currentUser.UserId;
        appointment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الموعد بنجاح" });
    }

    // ─── POST /api/appointments/{id}/send-reminder ───────────────────────────
    [HttpPost("{id:guid}/send-reminder")]
    public async Task<IActionResult> SendReminder(Guid id)
    {
        var appointment = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        if (appointment.Patient is null)
            return BadRequest(new { message = "بيانات المريض غير متوفرة" });

        var patientName = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}".Trim();
        var doctorName = appointment.Doctor?.Name ?? "الطبيب";
        var dateStr = appointment.AppointmentDate.ToString("yyyy/MM/dd");
        var timeStr = appointment.StartTime.ToString("HH:mm");

        try
        {
            var result = await whatsapp.SendAppointmentReminderAsync(new SendAppointmentReminderRequest
            {
                AppointmentId = appointment.Id,
                HoursBefore = 0 // manual trigger = send now regardless of timing
            });

            if (result is null)
                return BadRequest(new { message = "فشل إرسال التذكير عبر واتساب" });

            appointment.ConfirmationSent = true;
            appointment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(new { message = "تم إرسال التذكير بنجاح" });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send WhatsApp reminder for appointment {AppointmentId}", id);
            return BadRequest(new { message = "فشل إرسال التذكير عبر واتساب" });
        }
    }

    // ─── POST /api/appointments/{id}/start-visit ──────────────────────────────
    [HttpPost("{id:guid}/start-visit")]
    public async Task<IActionResult> StartVisit(Guid id)
    {
        // 1. Find appointment
        var appointment = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        // 2. Validate patient exists and is active
        if (appointment.Patient == null || !appointment.Patient.IsActive)
            return BadRequest(new { message = "المريض غير موجود أو مؤرشف" });

        // 3. Prevent duplicate visit for same appointment
        var existingVisit = await db.Visits
            .AnyAsync(v => v.AppointmentId == id && v.IsActive);

        if (existingVisit)
            return Conflict(new { message = "تم إنشاء زيارة لهذا الموعد مسبقًا" });

        // 4. Validate appointment status transition using centralized rules
        var targetStatus = AppointmentStatus.InProgress;
        if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, targetStatus))
            return BadRequest(new { message = $"لا يمكن تغيير حالة الموعد من {appointment.Status} إلى {targetStatus}. يجب اتباع تسلسل الحالات الصحيح" });

        // 5. Create visit linked to appointment
        var visit = new Visit
        {
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            DoctorId = appointment.DoctorId,
            VisitDate = appointment.AppointmentDate,
            VisitType = "Consultation",
            Specialty = appointment.Specialty,
            ChiefComplaint = appointment.Notes,
        };

        db.Visits.Add(visit);

        // 6. Update appointment status to InProgress (transition already validated above)
        appointment.Status = targetStatus;
        appointment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Load navigation for response
        await db.Entry(visit).Reference(v => v.Doctor).LoadAsync();

        return Ok(new
        {
            visit.Id,
            visit.PatientId,
            visit.AppointmentId,
            VisitDate = visit.VisitDate.ToString("yyyy-MM-dd"),
            DoctorName = visit.Doctor?.Name,
            message = "تم إنشاء الزيارة بنجاح"
        });
    }
}
