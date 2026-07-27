using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Policy = "StaffOnly")]
// CORE-PAT-006 (security): this controller returns PatientName, PatientNumber,
// CompanionPhone and appointment notes. Without the per-patient access filter a
// doctor denied by /api/patients/{id} could still read the same patient's
// identity and history through /api/appointments/patient/{id}. Every other
// patient-data controller (Visits, Prescriptions, LabOrders, Documents…)
// already applies it.
[ServiceFilter(typeof(PatientAccessFilter))]
public class AppointmentsController(AppointmentService service, AppDbContext db, ICurrentUserService currentUser, IWhatsAppService whatsapp, IEmailService emailService, IRealTimePushService pushService, IPatientAccessService patientAccess, IAuditService audit, ILogger<AppointmentsController> logger) : ControllerBase
{
    /// <summary>
    /// Best-effort push of JourneyUpdated. Scoped to the caller's branch when
    /// resolvable; falls back to PushToAllAsync for admin callers without a branch.
    /// Never throws — push failure must not fail the HTTP request.
    /// </summary>
    private async Task PushJourneyUpdatedAsync(string action, Guid? appointmentId = null, Guid? patientId = null, Guid? branchId = null)
    {
        try
        {
            var payload = new { action, appointmentId, patientId, branchId };
            if (branchId.HasValue && branchId.Value != Guid.Empty)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.JourneyUpdated, payload);
            else
                await pushService.PushToAllAsync(MessagingHubEvents.JourneyUpdated, payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push JourneyUpdated ({Action})", action);
        }
    }

    /// <summary>
    /// CORE-APPT-001: [ServiceFilter(typeof(PatientAccessFilter))] only enforces on
    /// endpoints that carry a "patientId" route/query value — which is exactly one
    /// endpoint in this controller (GetByPatient). Every other action that reads or
    /// writes a specific patient's appointment (by appointment id, or by request-body
    /// PatientId) needs this explicit check, mirroring PatientsController's
    /// DenyIfDoctorCannotAccess. Returns null when access is allowed (or the caller
    /// is not a restricted doctor role).
    /// </summary>
    private async Task<ActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor)
            return null;

        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            logger.LogWarning(
                "Appointment patient access denied: user {UserId} (role {Role}) attempted to access patient {PatientId}",
                currentUser.UserId, currentUser.Role, patientId);

            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", role = currentUser.Role?.ToString(), userId = currentUser.UserId, module = "appointments" });

            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }

        return null;
    }

    /// <summary>
    /// CORE-APPT-001: list endpoints (today/range/upcoming/recall) carry no single
    /// patientId to gate on — a restricted doctor must instead have the result set
    /// itself filtered down to patients they're linked to. Returns null for
    /// non-doctor roles (no filter needed).
    /// </summary>
    private Task<HashSet<Guid>?> GetAccessiblePatientIdsIfDoctorAsync() =>
        patientAccess.IsDoctor ? patientAccess.GetAccessiblePatientIdsAsync() : Task.FromResult<HashSet<Guid>?>(null);

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
            targetDate = ClinicTimeProvider.ClinicToday();
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

        var accessible = await GetAccessiblePatientIdsIfDoctorAsync();
        if (accessible != null)
            list = list.Where(a => accessible.Contains(a.PatientId));

        return Ok(list);
    }

    /// <summary>
    /// GET /api/appointments/recall-candidates — patients who no-showed within
    /// the window (default 30 days) and have no upcoming appointment. Feeds the
    /// recall worklist so the backlog of missed visits gets actively chased.
    /// </summary>
    [HttpGet("recall-candidates")]
    public async Task<IActionResult> GetRecallCandidates([FromQuery] int windowDays = 30)
    {
        if (windowDays is < 1 or > 365)
            return BadRequest(new { message = "نافذة الأيام يجب أن تكون بين 1 و 365" });

        var today = ClinicTimeProvider.ClinicToday();
        var since = today.AddDays(-windowDays);
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var noShowQuery = db.Appointments.Where(a =>
            a.IsActive && a.Status == AppointmentStatus.NoShow
            && a.AppointmentDate >= since && a.AppointmentDate <= today
            && !db.Appointments.Any(f =>
                f.IsActive && f.PatientId == a.PatientId
                && f.AppointmentDate >= today
                && f.Status != AppointmentStatus.Cancelled
                && f.Status != AppointmentStatus.NoShow));
        if (branchId.HasValue) noShowQuery = noShowQuery.Where(a => a.BranchId == branchId);

        var candidatesQuery = noShowQuery
            .GroupBy(a => a.PatientId)
            .Select(g => new
            {
                PatientId = g.Key,
                MissedCount = g.Count(),
                LastMissedDate = g.Max(a => a.AppointmentDate),
            })
            .Join(db.Patients.Where(p => p.IsActive),
                c => c.PatientId, p => p.Id,
                (c, p) => new
                {
                    c.PatientId,
                    PatientName = (p.FirstName + " " + p.LastName).Trim(),
                    p.PatientNumber,
                    p.Phone,
                    c.MissedCount,
                    c.LastMissedDate,
                });

        var accessible = await GetAccessiblePatientIdsIfDoctorAsync();
        if (accessible != null)
            candidatesQuery = candidatesQuery.Where(c => accessible.Contains(c.PatientId));

        var candidates = await candidatesQuery
            .OrderByDescending(c => c.LastMissedDate)
            .ThenByDescending(c => c.MissedCount)
            .Take(200)
            .ToListAsync();

        return Ok(new { items = candidates, totalCount = candidates.Count, windowDays });
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
            fromDate = ClinicTimeProvider.ClinicToday();
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

        // CORE-APPT-001: when patientId is supplied, PatientAccessFilter already gated it
        // via the query string. When omitted, a restricted doctor must not see every
        // patient's appointments in the range just by naming a doctorId/date window.
        if (!patientId.HasValue)
        {
            var accessible = await GetAccessiblePatientIdsIfDoctorAsync();
            if (accessible != null)
                result = result.Where(a => accessible.Contains(a.PatientId));
        }

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
        if (result == null) return NotFound(new { message = "الموعد غير موجود" });

        var denied = await DenyIfDoctorCannotAccess(result.PatientId);
        if (denied != null) return denied;

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req)
    {
        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied != null) return denied;

        var orthoValidation = await ValidateOrthoCaseLinkAsync(req);
        if (orthoValidation != null)
            return orthoValidation;

        // CORE-APPT-003: the room (and doctor) conflict check now happens atomically
        // inside AppointmentService.CreateAsync (TryCreateWithConflictGuardAsync,
        // under a transaction + advisory lock) — a separate pre-check here was a
        // non-atomic TOCTOU gap (two concurrent requests for different doctors could
        // both pass this plain AnyAsync and double-book the room).
        var (result, error) = await service.CreateAsync(req);
        if (error != null)
            return Conflict(new { message = error });

        // SignalR: best-effort push so daily-ops screens invalidate instantly.
        await PushJourneyUpdatedAsync("appointment-created", result!.Id, req.PatientId, branchId: currentUser.BranchId);

        return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateAppointmentRequest req)
    {
        // CORE-APPT-001 follow-up (Codex review): AppointmentService.UpdateAsync loads
        // the appointment by route `id` and never reassigns PatientId — it always stays
        // whichever patient the appointment already belongs to. Checking req.PatientId
        // here would let a restricted doctor name one of their OWN accessible patients
        // in the body while actually mutating (and reading back PHI for) a completely
        // different, inaccessible patient's appointment. Must authorize against the
        // appointment's stored owner, not the request body.
        var ownerPatientId = await db.Appointments.Where(a => a.Id == id).Select(a => (Guid?)a.PatientId).FirstOrDefaultAsync();
        if (ownerPatientId.HasValue)
        {
            var denied = await DenyIfDoctorCannotAccess(ownerPatientId.Value);
            if (denied != null) return denied;
        }

        var orthoValidation = await ValidateOrthoCaseLinkAsync(req);
        if (orthoValidation != null)
            return orthoValidation;

        // CORE-APPT-003: the room (and doctor) conflict check now happens atomically
        // inside AppointmentService.UpdateAsync (TryUpdateWithConflictGuardAsync,
        // under a transaction + advisory lock) — a separate pre-check here was a
        // non-atomic TOCTOU gap (two concurrent requests for different doctors could
        // both pass this plain AnyAsync and double-book the room).
        var (result, error) = await service.UpdateAsync(id, req);
        if (error != null)
            return error == "الموعد غير موجود" ? NotFound(new { message = error }) : Conflict(new { message = error });

        // SignalR: best-effort push so daily-ops screens invalidate instantly.
        await PushJourneyUpdatedAsync("appointment-updated", id, req.PatientId, branchId: currentUser.BranchId);

        return Ok(result);
    }

    private async Task<IActionResult?> ValidateOrthoCaseLinkAsync(CreateAppointmentRequest req)
    {
        if (!req.OrthoCaseId.HasValue)
            return null;

        var validCase = await db.OrthoCases
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Id == req.OrthoCaseId.Value
                && c.PatientId == req.PatientId
                && c.IsActive
                && c.Status == OrthoCaseStatus.Active);

        return validCase
            ? null
            : BadRequest(new { message = "حالة التقويم غير موجودة أو لا تخص المريض المحدد" });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppointmentStatusRequest req)
    {
        var ownerPatientId = await db.Appointments.Where(a => a.Id == id).Select(a => (Guid?)a.PatientId).FirstOrDefaultAsync();
        if (ownerPatientId.HasValue)
        {
            var denied = await DenyIfDoctorCannotAccess(ownerPatientId.Value);
            if (denied != null) return denied;
        }

        var (result, error) = await service.UpdateStatusAsync(id, req.Status);
        if (error != null) return BadRequest(new { message = error });
        if (result == null) return NotFound();

        // Auto-add to queue when appointment status changes to Arrived or Waiting
        if (req.Status is "Arrived" or "Waiting")
        {
            try
            {
                var today = ClinicTimeProvider.ClinicToday();
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

        // SignalR: best-effort push so daily-ops screens invalidate instantly.
        // patientId resolved from the appointment for the payload (best-effort — null if not found).
        Guid? patientIdForPush = null;
        try { patientIdForPush = await db.Appointments.Where(a => a.Id == id).Select(a => a.PatientId).FirstOrDefaultAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to resolve patientId for JourneyUpdated push (appointment {AppointmentId})", id); }
        await PushJourneyUpdatedAsync("appointment-status-changed", id, patientIdForPush, branchId: currentUser.BranchId);

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

        var appointmentsQuery = db.Appointments
            .Where(a => req.AppointmentIds.Contains(a.Id) && a.IsActive);

        // CORE-APPT-001: a restricted doctor batch-updating status must not be able to
        // touch appointments for patients outside their access set by simply supplying
        // more appointment ids — filter them out the same way GetToday/GetByRange do.
        var accessible = await GetAccessiblePatientIdsIfDoctorAsync();
        if (accessible != null)
            appointmentsQuery = appointmentsQuery.Where(a => accessible.Contains(a.PatientId));

        var appointments = await appointmentsQuery.ToListAsync();

        var updated = 0;
        var skipped = 0;
        foreach (var appointment in appointments)
        {
            // CORE-APPT-005: use the same rule the single-appointment endpoint uses.
            // This called IsValidTransition directly, which treats Cancelled/NoShow as
            // strictly terminal — so re-scheduling a no-show (allowed one at a time)
            // was silently counted as "skipped due to a status conflict" in bulk.
            if (AppointmentStatusTransitions.IsValidStatusChange(appointment.Status, targetStatus))
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

        // SignalR: best-effort push so daily-ops screens invalidate instantly.
        // Batch update touches many appointments; payload carries no ids (just the action)
        // so clients invalidate the whole daily-ops query.
        await PushJourneyUpdatedAsync("appointment-status-changed", branchId: currentUser.BranchId);

        return Ok(new { updated, skipped, message = $"تم تحديث {updated} موعد، تم تخطي {skipped} موعد بسبب تعارض في الحالة" });
    }

    // ─── GET /api/appointments/upcoming ──────────────────────────────────────
    /// <summary>Get upcoming appointments for the next N hours.</summary>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcoming([FromQuery] int hours = 2)
    {
        if (hours < 1) hours = 1;
        if (hours > 72) hours = 72;

        var now = ClinicTimeProvider.ClinicNow();
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

        var accessible = await GetAccessiblePatientIdsIfDoctorAsync();
        if (accessible != null)
            appointments = appointments.Where(a => accessible.Contains(a.PatientId)).ToList();

        var result = appointments.Select(a => new
        {
            a.Id,
            a.PatientId,
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

        var denied = await DenyIfDoctorCannotAccess(appointment.PatientId);
        if (denied != null) return denied;

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
        var today = ClinicTimeProvider.ClinicToday();
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

    // ─── GET /api/appointments/{id}/email-available ────────────────────────────
    /// <summary>
    /// Check if the patient for this appointment has an email on file.
    /// Used by frontend to conditionally show/disable the email reminder button.
    /// </summary>
    [HttpGet("{id:guid}/email-available")]
    public async Task<IActionResult> GetEmailAvailable(Guid id)
    {
        var appointment = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        var denied = await DenyIfDoctorCannotAccess(appointment.PatientId);
        if (denied != null) return denied;

        var patientEmail = await AppointmentReminderJob.GetPatientEmailAsync(db, appointment.PatientId, appointment.Id);

        return Ok(new { hasEmail = !string.IsNullOrWhiteSpace(patientEmail) });
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

        var denied = await DenyIfDoctorCannotAccess(appointment.PatientId);
        if (denied != null) return denied;

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

            appointment.WhatsAppReminderSentAt = DateTime.UtcNow;
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
    /// Tracks email reminder separately — does NOT mark WhatsApp reminder as sent.
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

        var denied = await DenyIfDoctorCannotAccess(appointment.PatientId);
        if (denied != null) return denied;

        if (appointment.Patient is null)
            return BadRequest(new { message = "بيانات المريض غير متوفرة" });

        // ── Resolve patient email using priority order (same as AppointmentReminderJob) ──
        var patientEmail = await AppointmentReminderJob.GetPatientEmailAsync(db, appointment.PatientId, appointment.Id);

        if (string.IsNullOrWhiteSpace(patientEmail))
            return BadRequest(new { message = "لا يوجد بريد إلكتروني مسجل لهذا المريض" });

        var patientName = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}".Trim();
        var doctorName = appointment.Doctor?.Name ?? "الطبيب";
        var dateStr = appointment.AppointmentDate.ToString("yyyy/MM/dd");
        var timeStr = appointment.StartTime.ToString("HH:mm");
        var clinicService = appointment.Service?.ArabicName;

        var identity = await FinanceClinicIdentity.ResolveAsync(db);
        var subject = $"تذكير بموعد {identity.Name}";
        var htmlBody = EmailService.BuildAppointmentReminderHtml(
            patientName, doctorName, dateStr, timeStr, clinicService, appointment.Notes,
            identity.Name, identity.Location);

        try
        {
            var sent = await emailService.SendAppointmentReminderAsync(
                patientEmail, subject, htmlBody, appointment.Id);

            if (!sent)
                return BadRequest(new { message = "تعذر إرسال التذكير، حاول مرة أخرى" });

            // ── Track email reminder separately — do NOT set ConfirmationSent ──
            appointment.EmailReminderSentAt = DateTime.UtcNow;
            appointment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Manual email reminder sent for appointment {AppointmentId} to {Email}",
                id, AppointmentReminderJob.MaskEmail(patientEmail));

            return Ok(new { message = "تم إرسال تذكير الموعد بنجاح" });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send email reminder for appointment {AppointmentId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إرسال التذكير" });
        }
    }

    // ─── POST /api/appointments/{id}/start-visit ──────────────────────────────
    /// <summary>
    /// Start a visit for an appointment.
    /// Sprint 1 FIX: Added transaction + advisory lock to prevent race condition
    /// that could create duplicate visits when concurrent requests hit this endpoint.
    /// Pattern matches ClinicQueueController.StartVisit.
    /// </summary>
    [HttpPost("{id:guid}/start-visit")]
    public async Task<IActionResult> StartVisit(Guid id)
    {
        // 1. Pre-flight validation (outside transaction for fast rejection)
        var appointment = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        var denied = await DenyIfDoctorCannotAccess(appointment.PatientId);
        if (denied != null) return denied;

        // 2. Validate patient exists and is active
        if (appointment.Patient == null || !appointment.Patient.IsActive)
            return BadRequest(new { message = "المريض غير موجود أو مؤرشف" });

        // 3. Validate appointment status transition using centralized rules
        var targetStatus = AppointmentStatus.InProgress;
        if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, targetStatus))
            return BadRequest(new { message = $"لا يمكن تغيير حالة الموعد من {appointment.Status} إلى {targetStatus}. يجب اتباع تسلسل الحالات الصحيح" });

        // Sprint 1 FIX: Wrap in transaction + advisory lock to prevent duplicate visits
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire advisory lock scoped to this appointment
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Re-check for existing visit INSIDE the lock (to prevent race condition)
            var existingVisit = await db.Visits
                .AnyAsync(v => v.AppointmentId == id && v.IsActive);

            if (existingVisit)
            {
                await tx.RollbackAsync();
                return Conflict(new { message = "تم إنشاء زيارة لهذا الموعد مسبقًا" });
            }

            // 4. Create visit linked to appointment
            var visit = new Visit
            {
                PatientId = appointment.PatientId,
                AppointmentId = appointment.Id,
                DoctorId = appointment.DoctorId,
                VisitDate = appointment.AppointmentDate,
                VisitType = "Consultation",
                Specialty = appointment.Specialty,
                ChiefComplaint = appointment.Notes,
                ServiceId = appointment.ServiceId,
            };

            db.Visits.Add(visit);

            // 5. Update appointment status to InProgress (transition already validated above)
            appointment.Status = targetStatus;
            appointment.UpdatedAt = DateTime.UtcNow;

            // Update linked ClinicQueueItem with the new VisitId and status
            var linkedQueueItem = await db.ClinicQueueItems
                .FirstOrDefaultAsync(q => q.AppointmentId == id && q.IsActive
                    && q.Status != ClinicQueueStatus.Completed
                    && q.Status != ClinicQueueStatus.Cancelled);
            if (linkedQueueItem != null)
            {
                linkedQueueItem.VisitId = visit.Id;
                linkedQueueItem.Status = ClinicQueueStatus.InProgress;
                linkedQueueItem.StartedAt = DateTime.UtcNow;
                linkedQueueItem.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

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
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
