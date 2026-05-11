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
public class AppointmentsController(AppointmentService service, AppDbContext db, ICurrentUserService currentUser, IWhatsAppService whatsapp) : ControllerBase
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
        [FromQuery] Guid? doctorId,
        [FromQuery] Guid? patientId)
    {
        var fromDate = from != null ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today);
        var toDate = to != null ? DateOnly.Parse(to) : fromDate;

        var list = await service.GetByDateRangeAsync(fromDate, toDate, doctorId, patientId);
        return Ok(list);
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
        catch
        {
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

        // 4. Validate appointment status — only certain statuses can start a visit
        var allowedStatuses = new[] { AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.Arrived, AppointmentStatus.Waiting, AppointmentStatus.Called, AppointmentStatus.InRoom, AppointmentStatus.InProgress };
        if (!allowedStatuses.Contains(appointment.Status))
            return BadRequest(new { message = "لا يمكن بدء زيارة لموعد بحالة " + appointment.Status.ToString() });

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

        // 6. Update appointment status to InProgress
        appointment.Status = AppointmentStatus.InProgress;
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
