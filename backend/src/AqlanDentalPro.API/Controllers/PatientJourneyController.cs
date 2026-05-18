using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Patient Journey Command Center — unified operational screen for reception
/// and clinical workflow: Appointment → Arrived → Queue → Visit → Checkout.
/// </summary>
[ApiController]
[Route("api/patient-journey")]
[Authorize(Policy = "StaffOnly")]
public class PatientJourneyController(AppDbContext db) : ControllerBase
{
    // ─── 1. GET /api/patient-journey/today ────────────────────────────────────
    /// <summary>Returns today's patient journey list combining appointments,
    /// queue status, visit data, and payment info.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday([FromQuery] string? date, [FromQuery] string? status,
        [FromQuery] Guid? doctorId, [FromQuery] Guid? serviceId, [FromQuery] Guid? roomId)
    {
        // Parse date - default to today
        DateOnly queryDate;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParse(date, out var parsed))
                return BadRequest(new { message = "صيغة التاريخ غير صالحة. استخدم YYYY-MM-DD" });
            queryDate = parsed;
        }
        else
        {
            queryDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        // Validate status filter if provided
        AppointmentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
                return BadRequest(new { message = "حالة غير صالحة" });
            statusFilter = parsedStatus;
        }

        // Build base query for today's appointments
        var query = db.Appointments
            .IgnoreQueryFilters()
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == queryDate && a.IsActive)
            .AsQueryable();

        // Apply filters
        if (statusFilter.HasValue)
            query = query.Where(a => a.Status == statusFilter.Value);
        if (doctorId.HasValue)
            query = query.Where(a => a.DoctorId == doctorId.Value);
        if (serviceId.HasValue)
            query = query.Where(a => a.ServiceId == serviceId.Value);
        if (roomId.HasValue)
            query = query.Where(a => a.ClinicRoomId == roomId.Value);

        var appointments = await query
            .OrderBy(a => a.StartTime)
            .ToListAsync();

        // Load related queue items for today
        var appointmentIds = appointments.Select(a => a.Id).ToList();
        var queueItems = await db.ClinicQueueItems
            .IgnoreQueryFilters()
            .Where(q => appointmentIds.Contains(q.AppointmentId ?? Guid.Empty) && q.QueueDate == queryDate && q.IsActive)
            .ToDictionaryAsync(q => q.AppointmentId ?? Guid.Empty);

        // Load visits for these appointments
        var visits = await db.Visits
            .IgnoreQueryFilters()
            .Where(v => v.AppointmentId != null && appointmentIds.Contains(v.AppointmentId.Value) && v.IsActive)
            .ToDictionaryAsync(v => v.AppointmentId!.Value);

        // Load service info for appointments that have ServiceId
        var serviceIds = appointments.Where(a => a.ServiceId.HasValue).Select(a => a.ServiceId!.Value).Distinct().ToList();
        var services = serviceIds.Count > 0
            ? await db.ClinicServices.IgnoreQueryFilters().Where(s => serviceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id)
            : new Dictionary<Guid, ClinicService>();

        // Check consultation fee payment status for today
        var patientIds = appointments.Select(a => a.PatientId).Distinct().ToList();
        var todayPayments = await db.Payments
            .Where(p => patientIds.Contains(p.PatientId) && p.PaymentDate == queryDate && p.IsActive)
            .GroupBy(p => p.PatientId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.Amount));

        // Build journey items
        var result = appointments.Select(a =>
        {
            queueItems.TryGetValue(a.Id, out var queueItem);
            visits.TryGetValue(a.Id, out var visit);
            var service = a.ServiceId.HasValue && services.TryGetValue(a.ServiceId.Value, out var s) ? s : null;

            var consultationFeeRequired = service?.RequiresConsultationFee ?? false;
            var consultationFeePaid = false;
            if (consultationFeeRequired && todayPayments.TryGetValue(a.PatientId, out var paidAmount))
            {
                consultationFeePaid = paidAmount >= (service?.DefaultPrice ?? 0);
            }

            string? checkoutStatus = visit?.CheckoutStatus;
            string nextAction = DetermineNextAction(a.Status, queueItem?.Status, checkoutStatus);

            return new
            {
                AppointmentId = a.Id,
                PatientId = a.PatientId,
                PatientName = BuildPatientDisplayName(a.Patient),
                PatientPhone = a.Patient?.Phone,
                AppointmentTime = a.StartTime.ToString("HH:mm"),
                AppointmentStatus = a.Status.ToString(),
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor?.Name ?? "",
                ServiceId = a.ServiceId,
                ServiceName = service?.ArabicName,
                RoomName = a.RoomName ?? queueItem?.RoomName,
                QueueItemId = queueItem?.Id,
                QueueStatus = queueItem?.Status.ToString(),
                VisitId = visit?.Id,
                VisitStatus = visit != null ? (checkoutStatus ?? "InProgress") : null,
                ConsultationFeeRequired = consultationFeeRequired,
                ConsultationFeePaid = consultationFeePaid,
                CheckoutStatus = checkoutStatus,
                NextAction = nextAction
            };
        }).ToList();

        return Ok(result);
    }

    // ─── 2. POST /api/patient-journey/{appointmentId}/intake ────────────────
    /// <summary>Reception confirms patient arrival and records intake info.</summary>
    [HttpPost("{appointmentId:guid}/intake")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Intake(Guid appointmentId, [FromBody] IntakeRequest req)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Validate transition: Scheduled/Confirmed → Arrived
        var targetStatus = AppointmentStatus.Arrived;
        if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, targetStatus))
            return BadRequest(new { message = $"لا يمكن تسجيل الوصول لموعد بحالة {appointment.Status}" });

        // Update appointment
        appointment.Status = targetStatus;
        appointment.ArrivedAt = DateTime.UtcNow;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Attach service if provided
        if (req.ServiceId.HasValue)
        {
            var serviceExists = await db.ClinicServices.AnyAsync(s => s.Id == req.ServiceId.Value && s.IsActive);
            if (serviceExists)
                appointment.ServiceId = req.ServiceId.Value;
        }

        // Attach room if provided
        if (req.RoomId.HasValue)
        {
            var roomExists = await db.ClinicRooms.AnyAsync(r => r.Id == req.RoomId.Value && r.IsActive);
            if (roomExists)
            {
                appointment.ClinicRoomId = req.RoomId.Value;
                var room = await db.ClinicRooms.FindAsync(req.RoomId.Value);
                if (room != null)
                    appointment.RoomName = room.ArabicName;
            }
        }

        // Store notes
        if (!string.IsNullOrWhiteSpace(req.Notes))
            appointment.Notes = string.IsNullOrWhiteSpace(appointment.Notes)
                ? req.Notes
                : $"{appointment.Notes}\n{req.Notes}";

        await db.SaveChangesAsync();

        return Ok(new
        {
            appointment.Id,
            Status = appointment.Status.ToString(),
            appointment.ArrivedAt,
            ServiceId = appointment.ServiceId,
            message = "تم تسجيل وصول المريض بنجاح"
        });
    }

    // ─── 3. POST /api/patient-journey/{appointmentId}/send-to-queue ─────────
    /// <summary>Create or reuse queue item for the appointment.</summary>
    [HttpPost("{appointmentId:guid}/send-to-queue")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SendToQueue(Guid appointmentId, [FromBody] SendToQueueRequest? req = null)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Must be Arrived or Waiting status
        if (appointment.Status != AppointmentStatus.Arrived && appointment.Status != AppointmentStatus.Waiting)
            return BadRequest(new { message = "يجب أن يكون المريض وصل أو في الانتظار قبل إضافته للطابور" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Check for existing active queue item for this appointment
        var existingQueueItem = await db.ClinicQueueItems
            .AnyAsync(q => q.AppointmentId == appointmentId && q.QueueDate == today
                && q.Status != ClinicQueueStatus.Completed
                && q.Status != ClinicQueueStatus.Cancelled
                && q.IsActive);

        if (existingQueueItem)
            return Conflict(new { message = "المريض موجود بالفعل في الطابور" });

        // Determine room name
        string? roomName = appointment.RoomName;
        if (req?.RoomId.HasValue == true)
        {
            var room = await db.ClinicRooms.FindAsync(req.RoomId.Value);
            if (room != null)
            {
                roomName = room.ArabicName;
                appointment.ClinicRoomId = room.Id;
                appointment.RoomName = roomName;
            }
        }

        // Create queue item
        var queueItem = new ClinicQueueItem
        {
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            DoctorId = appointment.DoctorId,
            RoomName = roomName,
            Status = ClinicQueueStatus.Waiting,
            QueueDate = today,
            AddedByUserId = GetCurrentUserId(),
            Notes = req?.Notes
        };

        db.ClinicQueueItems.Add(queueItem);

        // Update appointment status to Waiting
        if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Waiting))
        {
            appointment.Status = AppointmentStatus.Waiting;
            appointment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            queueItem.Id,
            QueueStatus = queueItem.Status.ToString(),
            StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(queueItem.Status),
            message = "تمت إضافة المريض إلى الطابور بنجاح"
        });
    }

    // ─── 4. POST /api/patient-journey/{appointmentId}/start-visit ───────────
    /// <summary>Doctor starts the visit. Reuses existing visit/queue logic.</summary>
    [HttpPost("{appointmentId:guid}/start-visit")]
    public async Task<IActionResult> StartVisit(Guid appointmentId)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Must be in InRoom or Called status
        if (appointment.Status != AppointmentStatus.InRoom && appointment.Status != AppointmentStatus.Called)
            return BadRequest(new { message = "يجب أن يكون المريض داخل الغرفة قبل بدء الزيارة" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find the queue item for this appointment
        var queueItem = await db.ClinicQueueItems
            .FirstOrDefaultAsync(q => q.AppointmentId == appointmentId && q.QueueDate == today
                && q.IsActive
                && q.Status != ClinicQueueStatus.Completed
                && q.Status != ClinicQueueStatus.Cancelled);

        if (queueItem == null)
            return BadRequest(new { message = "لا يوجد عنصر طابور نشط لهذا الموعد" });

        // Validate queue transition to InProgress
        var validationError = ClinicQueueStatusTransitions.GetValidationError(queueItem.Status, ClinicQueueStatus.InProgress);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        // Check for existing visit
        var existingVisit = await db.Visits
            .FirstOrDefaultAsync(v => v.AppointmentId == appointmentId && v.IsActive);

        if (existingVisit != null)
        {
            // Visit already exists, just update statuses
            queueItem.Status = ClinicQueueStatus.InProgress;
            queueItem.StartedAt = DateTime.UtcNow;
            queueItem.UpdatedAt = DateTime.UtcNow;

            if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.InProgress))
            {
                appointment.Status = AppointmentStatus.InProgress;
                appointment.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Ok(new
            {
                existingVisit.Id,
                QueueItemId = queueItem.Id,
                QueueStatus = queueItem.Status.ToString(),
                AppointmentStatus = appointment.Status.ToString(),
                message = "تم بدء الزيارة بنجاح (زيارة موجودة)"
            });
        }

        // Create new visit
        var visit = new Visit
        {
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            VisitDate = today,
            DoctorId = appointment.DoctorId,
            Specialty = appointment.Specialty,
            ServiceId = appointment.ServiceId
        };

        db.Visits.Add(visit);

        // Update queue item
        queueItem.VisitId = visit.Id;
        queueItem.Status = ClinicQueueStatus.InProgress;
        queueItem.StartedAt = DateTime.UtcNow;
        queueItem.UpdatedAt = DateTime.UtcNow;

        // Update appointment
        if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.InProgress))
        {
            appointment.Status = AppointmentStatus.InProgress;
            appointment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            visit.Id,
            QueueItemId = queueItem.Id,
            QueueStatus = queueItem.Status.ToString(),
            AppointmentStatus = appointment.Status.ToString(),
            message = "تم بدء الزيارة بنجاح"
        });
    }

    // ─── 5. POST /api/patient-journey/{visitId}/handoff-to-reception ────────
    /// <summary>Doctor finishes and sends patient to reception for checkout.</summary>
    [HttpPost("{visitId:guid}/handoff-to-reception")]
    public async Task<IActionResult> HandoffToReception(Guid visitId, [FromBody] HandoffRequest req)
    {
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });

        // Update visit clinical data
        if (!string.IsNullOrWhiteSpace(req.TreatmentDone))
            visit.TreatmentDone = req.TreatmentDone;
        if (!string.IsNullOrWhiteSpace(req.Diagnosis))
            visit.Diagnosis = req.Diagnosis;
        if (!string.IsNullOrWhiteSpace(req.NextVisitPlan))
            visit.NextVisitPlan = req.NextVisitPlan;
        if (!string.IsNullOrWhiteSpace(req.Instructions))
            visit.Instructions = req.Instructions;
        if (req.SuggestedServiceId.HasValue)
            visit.ServiceId = req.SuggestedServiceId;
        if (req.FollowUpDate.HasValue)
            visit.NextVisitDate = req.FollowUpDate;
        if (req.AmountDue.HasValue)
            visit.AmountDueReference = req.AmountDue;

        // Mark as ready for checkout
        visit.CheckoutStatus = "ReadyForCheckout";
        visit.ReadyForCheckoutAt = DateTime.UtcNow;
        visit.UpdatedAt = DateTime.UtcNow;

        // Update queue item if exists
        if (visit.AppointmentId.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var queueItem = await db.ClinicQueueItems
                .FirstOrDefaultAsync(q => q.AppointmentId == visit.AppointmentId && q.QueueDate == today && q.IsActive);

            if (queueItem != null && ClinicQueueStatusTransitions.IsValidTransition(queueItem.Status, ClinicQueueStatus.Completed))
            {
                queueItem.Status = ClinicQueueStatus.Completed;
                queueItem.CompletedAt = DateTime.UtcNow;
                queueItem.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            visit.Id,
            CheckoutStatus = visit.CheckoutStatus,
            ReadyForCheckoutAt = visit.ReadyForCheckoutAt,
            AmountDueReference = visit.AmountDueReference,
            message = "تم تسليم المريض للاستقبال بنجاح"
        });
    }

    // ─── 6. POST /api/patient-journey/{appointmentId}/checkout ──────────────
    /// <summary>Complete checkout for the patient journey.</summary>
    [HttpPost("{appointmentId:guid}/checkout")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Checkout(Guid appointmentId, [FromBody] CheckoutRequest req)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Find the visit for this appointment
        var visit = await db.Visits
            .FirstOrDefaultAsync(v => v.AppointmentId == appointmentId && v.IsActive);

        if (visit == null)
            return BadRequest(new { message = "لا توجد زيارة مرتبطة بهذا الموعد" });

        if (visit.CheckoutStatus != "ReadyForCheckout")
            return BadRequest(new { message = "الزيارة ليست جاهزة للحساب بعد" });

        // Record payment if amount provided — use existing Payment entity
        if (req.PaymentAmount.HasValue && req.PaymentAmount.Value > 0)
        {
            var payment = new Payment
            {
                PatientId = appointment.PatientId,
                Amount = req.PaymentAmount.Value,
                PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentMethod = req.PaymentMethod ?? "cash",
                DoctorId = appointment.DoctorId,
                BranchId = appointment.BranchId,
                ReceivedBy = GetCurrentUserId(),
                Notes = req.Notes ?? "دفع عبر رحلة المريض"
            };
            db.Payments.Add(payment);
        }

        // Mark visit as checked out
        visit.CheckoutStatus = "CheckedOut";
        visit.UpdatedAt = DateTime.UtcNow;

        // Complete appointment if valid
        if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Completed))
        {
            appointment.Status = AppointmentStatus.Completed;
            appointment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // Build recommended next actions
        var nextActions = new List<string>();
        if (req.NextAppointmentDate.HasValue)
            nextActions.Add("حجز موعد متابعة");
        if (req.PaymentAmount.HasValue && req.PaymentAmount.Value > 0)
            nextActions.Add("طباعة إيصال الدفع");

        return Ok(new
        {
            AppointmentId = appointment.Id,
            VisitId = visit.Id,
            CheckoutStatus = visit.CheckoutStatus,
            AppointmentStatus = appointment.Status.ToString(),
            NextActions = nextActions,
            message = "تم إنهاء الحساب بنجاح"
        });
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private static string DetermineNextAction(AppointmentStatus apptStatus, ClinicQueueStatus? queueStatus, string? checkoutStatus)
    {
        // Checkout flow takes priority
        if (checkoutStatus == "ReadyForCheckout")
            return "Checkout";
        if (checkoutStatus == "CheckedOut")
            return "None";

        return apptStatus switch
        {
            AppointmentStatus.Scheduled => "Intake",
            AppointmentStatus.Confirmed => "Intake",
            AppointmentStatus.Arrived => "SendToQueue",
            AppointmentStatus.Waiting => "CallPatient",
            AppointmentStatus.Called => "EnterRoom",
            AppointmentStatus.InRoom => "StartVisit",
            AppointmentStatus.InProgress => "InProgress",
            AppointmentStatus.Completed => "None",
            AppointmentStatus.Cancelled => "None",
            AppointmentStatus.NoShow => "None",
            _ => "None"
        };
    }

    private static string BuildPatientDisplayName(Patient? patient)
    {
        if (patient == null) return "";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(patient.FirstName)) parts.Add(patient.FirstName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.MiddleName)) parts.Add(patient.MiddleName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.LastName)) parts.Add(patient.LastName.Trim());
        return string.Join(" ", parts);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class IntakeRequest
{
    public Guid? ServiceId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? Notes { get; set; }
    public Guid? RoomId { get; set; }
    public bool RequiresConsultationFee { get; set; }
    public decimal? ConsultationFeeAmount { get; set; }
}

public class SendToQueueRequest
{
    public Guid? RoomId { get; set; }
    public string? Notes { get; set; }
}

public class HandoffRequest
{
    public string? TreatmentDone { get; set; }
    public string? Diagnosis { get; set; }
    public string? NextVisitPlan { get; set; }
    public string? Instructions { get; set; }
    public Guid? SuggestedServiceId { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public decimal? AmountDue { get; set; }
    public string? Notes { get; set; }
}

public class CheckoutRequest
{
    public decimal? PaymentAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateOnly? NextAppointmentDate { get; set; }
    public Guid? NextServiceId { get; set; }
}
