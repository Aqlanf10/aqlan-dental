using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Policy = "StaffOnly")]
public class AppointmentsController(AppointmentService service, AppDbContext db, ICurrentUserService currentUser, IWhatsAppService whatsapp, IEmailService emailService, ILogger<AppointmentsController> logger) : ControllerBase
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
        // Check room conflict BEFORE creating the appointment to avoid ghost appointments
        if (req.ClinicRoomId.HasValue)
        {
            var date = DateOnly.Parse(req.AppointmentDate);
            var start = TimeOnly.Parse(req.StartTime);
            var end = start.AddMinutes(req.DurationMinutes);

            var roomConflict = await db.Appointments
                .AnyAsync(a => a.ClinicRoomId == req.ClinicRoomId
                            && a.AppointmentDate == date
                            && a.StartTime < end
                            && a.EndTime > start
                            && a.IsActive
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.NoShow);

            if (roomConflict)
                return Conflict(new { message = "الغرفة محجوزة في هذا الوقت" });
        }

        var (result, error) = await service.CreateAsync(req);
        if (error != null)
            return Conflict(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AppointmentDto>> Update(Guid id, [FromBody] CreateAppointmentRequest req)
    {
        // Check room conflict BEFORE updating the appointment to avoid ghost state
        if (req.ClinicRoomId.HasValue)
        {
            var date = DateOnly.Parse(req.AppointmentDate);
            var start = TimeOnly.Parse(req.StartTime);
            var end = start.AddMinutes(req.DurationMinutes);

            var roomConflict = await db.Appointments
                .AnyAsync(a => a.ClinicRoomId == req.ClinicRoomId
                            && a.AppointmentDate == date
                            && a.StartTime < end
                            && a.EndTime > start
                            && a.IsActive
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.NoShow
                            && a.Id != id);

            if (roomConflict)
                return Conflict(new { message = "الغرفة محجوزة في هذا الوقت" });
        }

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
        if (result == null) return NotFound();

        // Auto-add to queue when appointment status changes to Arrived or Waiting
        if (req.Status is "Arrived" or "Waiting")
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var activeStatuses = new HashSet<ClinicQueueStatus>
                {
                    ClinicQueueStatus.Waiting,
                    ClinicQueueStatus.Called,
                    ClinicQueueStatus.InRoom,
                    ClinicQueueStatus.InProgress
                };

                // Check if a queue item already exists for this appointment
                var existingQueueItem = await db.ClinicQueueItems
                    .AnyAsync(q => q.AppointmentId == id && q.QueueDate == today
                                && activeStatuses.Contains(q.Status)
                                && q.IsActive);

                if (!existingQueueItem)
                {
                    var appointment = await db.Appointments.FindAsync(id);
                    if (appointment != null)
                    {
                        var queueItem = new ClinicQueueItem
                        {
                            PatientId = appointment.PatientId,
                            AppointmentId = appointment.Id,
                            DoctorId = appointment.DoctorId,
                            ServiceId = appointment.ServiceId,
                            ClinicRoomId = appointment.ClinicRoomId,
                            RoomName = appointment.RoomName,
                            Status = ClinicQueueStatus.Waiting,
                            QueueDate = today,
                        };
                        db.ClinicQueueItems.Add(queueItem);
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to auto-add appointment {AppointmentId} to queue", id);
                // Don't fail the status update if queue add fails
            }
        }

        return Ok(result);
    }

    // ─── POST /api/appointments/batch-status ─────────────────────────────────
    /// <summary>Update multiple appointments status at once (e.g., marking no-shows at end of day).</summary>
    [HttpPost("batch-status")]
    public async Task<IActionResult> BatchUpdateStatus([FromBody] BatchUpdateStatusRequest req)
    {
        if (req.AppointmentIds.Count > 50)
            return BadRequest(new { message = "لا يمكن تحديث أكثر من 50 موعد في المرة الواحدة" });

        if (!Enum.TryParse<AppointmentStatus>(req.Status, true, out var targetStatus))
            return BadRequest(new { message = "حالة الموعد غير صالحة" });

        var appointments = await db.Appointments
            .Where(a => req.AppointmentIds.Contains(a.Id) && a.IsActive)
            .ToListAsync();

        var updated = 0;
        var skipped = 0;
        foreach (var appointment in appointments)
        {
            if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, targetStatus))
            {
                appointment.Status = targetStatus;
                appointment.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        await db.SaveChangesAsync();

        return Ok(new { updated, skipped, message = $"تم تحديث {updated} موعد، تم تخطي {skipped} موعد بسبب تعارض في الحالة" });
    }

    // ─── GET /api/appointments/upcoming ──────────────────────────────────────
    /// <summary>Get upcoming appointments for the next N hours.</summary>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcoming([FromQuery] int hours = 2)
    {
        if (hours < 1) hours = 1;
        if (hours > 72) hours = 72;

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);
        var maxTime = currentTime.AddHours(hours);

        var targetStatuses = new List<AppointmentStatus>
        {
            AppointmentStatus.Scheduled,
            AppointmentStatus.Confirmed
        };

        // For same-day appointments
        var appointments = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.IsActive
                     && targetStatuses.Contains(a.Status)
                     && a.AppointmentDate == today
                     && a.StartTime >= currentTime
                     && a.StartTime <= maxTime)
            .OrderBy(a => a.StartTime)
            .ToListAsync();

        // If hours span across midnight, also get next day's early appointments
        if (maxTime < currentTime)
        {
            var tomorrow = today.AddDays(1);
            var tomorrowAppointments = await db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a => a.IsActive
                         && targetStatuses.Contains(a.Status)
                         && a.AppointmentDate == tomorrow
                         && a.StartTime <= maxTime)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            appointments = appointments.Concat(tomorrowAppointments).ToList();
        }

        var result = appointments.Select(a => new
        {
            a.Id,
            PatientName = a.Patient != null ? $"{a.Patient.FirstName} {a.Patient.LastName}".Trim() : "",
            DoctorName = a.Doctor != null ? a.Doctor.Name : "",
            a.AppointmentDate,
            StartTime = a.StartTime.ToString("HH:mm"),
            EndTime = a.EndTime.ToString("HH:mm"),
            Status = a.Status.ToString(),
            a.AppointmentType
        });

        return Ok(result);
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

        // H4 FIX: Cancel any active queue item for this appointment.
        // Previously, soft-deleting an appointment left the linked ClinicQueueItem
        // in an active state (Waiting/Called/InRoom), causing the TV display and
        // queue listing to show patients for deleted appointments.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeQueueItem = await db.ClinicQueueItems
            .FirstOrDefaultAsync(q => q.AppointmentId == id
                && q.QueueDate == today
                && q.Status != ClinicQueueStatus.Completed
                && q.Status != ClinicQueueStatus.Cancelled
                && q.IsActive);

        if (activeQueueItem != null)
        {
            activeQueueItem.Status = ClinicQueueStatus.Cancelled;
            activeQueueItem.CancelledAt = DateTime.UtcNow;
            activeQueueItem.UpdatedAt = DateTime.UtcNow;
        }

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

    // ─── POST /api/appointments/{id}/send-email-reminder ──────────────────────
    /// <summary>
    /// Send an appointment reminder email to the patient.
    /// Requires StaffOnly authorization.
    /// Returns Arabic error if patient has no email on file.
    /// </summary>
    [HttpPost("{id:guid}/send-email-reminder")]
    public async Task<IActionResult> SendEmailReminder(Guid id)
    {
        var appointment = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        if (appointment.Patient is null)
            return BadRequest(new { message = "بيانات المريض غير متوفرة" });

        // ── Resolve patient email via PatientAccount → User (same pattern as AppointmentReminderJob) ──
        var patientEmail = await db.PatientAccounts
            .Where(pa => pa.PatientId == appointment.PatientId && pa.LinkedUserId != null)
            .Join(db.Users, pa => pa.LinkedUserId, u => u.Id, (pa, u) => u.Email)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(patientEmail))
            return BadRequest(new { message = "لا يوجد بريد إلكتروني مسجل لهذا المريض" });

        var patientName = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}".Trim();
        var doctorName = appointment.Doctor?.Name ?? "الطبيب";
        var dateStr = appointment.AppointmentDate.ToString("yyyy/MM/dd");
        var timeStr = appointment.StartTime.ToString("HH:mm");
        var clinicService = appointment.Service?.ArabicName;

        var subject = $"تذكير بموعدك في مركز عقلان الكامل";
        var htmlBody = EmailService.BuildAppointmentReminderHtml(
            patientName, doctorName, dateStr, timeStr, clinicService, appointment.Notes);

        try
        {
            var sent = await emailService.SendAppointmentReminderAsync(
                patientEmail, subject, htmlBody, appointment.Id);

            if (!sent)
                return BadRequest(new { message = "تعذر إرسال التذكير، حاول مرة أخرى" });

            logger.LogInformation(
                "Manual email reminder sent for appointment {AppointmentId} to {Email}",
                id, patientEmail);

            return Ok(new { message = "تم إرسال تذكير الموعد بنجاح" });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send email reminder for appointment {AppointmentId}", id);
            return BadRequest(new { message = "تعذر إرسال التذكير، حاول مرة أخرى" });
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
