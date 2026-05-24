using AqlanDentalPro.Application.Interfaces.Services;
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
public class PatientJourneyController(AppDbContext db, ILogger<PatientJourneyController> logger, ICommissionService commissionService) : ControllerBase
{
    // ─── 1. GET /api/patient-journey/today ────────────────────────────────────
    /// <summary>Returns today's patient journey list combining appointments,
    /// queue status, visit data, and payment info.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday([FromQuery] string? date, [FromQuery] string? status,
        [FromQuery] Guid? doctorId, [FromQuery] Guid? serviceId, [FromQuery] Guid? roomId)
    {
        // Parse date - default to today (declared before try so catch can reference it)
        DateOnly queryDate = DateOnly.FromDateTime(DateTime.UtcNow);
        try
        {
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParse(date, out var parsed))
                return BadRequest(new { message = "صيغة التاريخ غير صالحة. استخدم YYYY-MM-DD" });
            queryDate = parsed;
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

        // No appointments for today — return empty list immediately
        if (appointments.Count == 0)
            return Ok(new List<object>());

        // Load related queue items for today
        var appointmentIds = appointments.Select(a => a.Id).ToList();
        var queueItems = await db.ClinicQueueItems
            .IgnoreQueryFilters()
            .Where(q => q.AppointmentId != null && appointmentIds.Contains(q.AppointmentId.Value) && q.QueueDate == queryDate && q.IsActive)
            .GroupBy(q => q.AppointmentId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.First()); // Handle duplicates safely

        // Load visits for these appointments
        var visits = await db.Visits
            .IgnoreQueryFilters()
            .Where(v => v.AppointmentId != null && appointmentIds.Contains(v.AppointmentId.Value) && v.IsActive)
            .GroupBy(v => v.AppointmentId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.First()); // Handle duplicates safely

        // Load service info for appointments that have ServiceId
        var serviceIds = appointments.Where(a => a.ServiceId.HasValue).Select(a => a.ServiceId!.Value).Distinct().ToList();
        var services = serviceIds.Count > 0
            ? await db.ClinicServices.IgnoreQueryFilters().Where(s => serviceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id)
            : new Dictionary<Guid, ClinicService>();

        // Check consultation fee payment status for today
        var patientIds = appointments.Select(a => a.PatientId).Distinct().ToList();
        var todayPayments = patientIds.Count > 0
            ? await db.Payments
                .Where(p => patientIds.Contains(p.PatientId) && p.PaymentDate == queryDate && p.IsActive)
                .GroupBy(p => p.PatientId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.Amount))
            : new Dictionary<Guid, decimal>();

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
                AmountDueReference = visit?.AmountDueReference,
                ConsultationFeeRequired = consultationFeeRequired,
                ConsultationFeePaid = consultationFeePaid,
                CheckoutStatus = checkoutStatus,
                NextAction = nextAction
            };
        }).ToList();

        return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientJourney.GetToday failed for date {Date}: {Message}", queryDate, ex.Message);
            return StatusCode(500, new { message = $"خطأ في رحلة المرضى: {ex.Message}", detail = ex.InnerException?.Message });
        }
    }

    // ─── 2. POST /api/patient-journey/{appointmentId}/intake ────────────────
    /// <summary>Reception confirms patient arrival and records intake info.</summary>
    [HttpPost("{appointmentId:guid}/intake")]
    [Authorize(Policy = "AdminOrReception")]
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
    [Authorize(Policy = "AdminOrReception")]
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

        try
        {
            await db.SaveChangesAsync(); // Save to get the visit ID
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to create visit for appointment {AppointmentId}. Inner: {InnerMessage}", appointmentId, ex.InnerException?.Message ?? ex.Message);
            return StatusCode(500, new { message = "فشل إنشاء الزيارة — يرجى المحاولة مرة أخرى" });
        }

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
    /// <summary>Complete checkout — workflow-status only. No payment is created;
    /// the frontend should redirect to the Payments module for actual payment processing.</summary>
    [HttpPost("{appointmentId:guid}/checkout")]
    [Authorize(Policy = "AdminOrReception")]
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

        // Checkout is workflow-status only — no direct Payment creation.
        // Actual payment processing should be done via the existing Payments module
        // (FinanceService.CreatePaymentAsync) to ensure receipt generation, notifications,
        // contract linking, and audit trail are handled correctly.
        // PaymentAmount/PaymentMethod are stored as reference in the response for
        // the frontend to guide the user to the Payments page.

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
            nextActions.Add("تسجيل الدفع عبر صفحة المالية");
        if (visit.AmountDueReference.HasValue && visit.AmountDueReference.Value > 0)
            nextActions.Add("المبلغ المستحق: " + visit.AmountDueReference.Value.ToString("N0") + " ر.ي");

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

    // ─── 7. POST /api/patient-journey/{visitId}/create-draft-invoice ────────
    /// <summary>Creates a Draft Invoice from a visit that is ready for checkout.
    /// Uses Visit.AmountDueReference and linked ServiceId for line item pricing.
    /// Does NOT create a Payment. Does NOT alter Contract or Patient balance.
    /// If a Draft invoice already exists for this Visit, returns the existing one.
    /// Uses advisory lock + unique constraint retry to prevent race condition on
    /// invoice number generation (same pattern as LabOrdersController).</summary>
    [HttpPost("{visitId:guid}/create-draft-invoice")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> CreateDraftInvoice(Guid visitId)
    {
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });

        // Duplicate prevention: check for existing active Draft invoice for this Visit
        var existingDraft = await db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

        if (existingDraft != null)
        {
            return Ok(new
            {
                existingDraft.Id,
                existingDraft.InvoiceNumber,
                Status = existingDraft.Status.ToString(),
                StatusArabic = "مسودة",
                existingDraft.TotalAmount,
                IsExisting = true,
                message = "توجد فاتورة مسودة لهذه الزيارة بالفعل"
            });
        }

        // CON-02: Advisory lock + unique constraint retry to prevent race condition
        // on invoice number generation. Strategy mirrors LabOrdersController.
        var userId = GetCurrentUserId();

        // Determine service and price (outside transaction — read-only lookup)
        ClinicService? service = null;
        var serviceId = visit.ServiceId ?? visit.Appointment?.ServiceId;
        if (serviceId.HasValue)
        {
            service = await db.ClinicServices.FindAsync(serviceId.Value);
        }

        string serviceName = service?.ArabicName ?? "خدمة علاجية";
        decimal unitPrice = visit.AmountDueReference
            ?? service?.DefaultPrice
            ?? 0;

        const int maxRetries = 3;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                // Acquire advisory lock for invoice number generation
                var lockKey = Math.Abs("InvoiceNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

                // Re-check for existing draft inside transaction (prevents TOCTOU race)
                var txExistingDraft = await db.Invoices
                    .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

                if (txExistingDraft != null)
                {
                    await tx.RollbackAsync();
                    return Ok(new
                    {
                        txExistingDraft.Id,
                        txExistingDraft.InvoiceNumber,
                        Status = txExistingDraft.Status.ToString(),
                        StatusArabic = "مسودة",
                        txExistingDraft.TotalAmount,
                        IsExisting = true,
                        message = "توجد فاتورة مسودة لهذه الزيارة بالفعل"
                    });
                }

                var invoiceNumber = await InvoicesController.GenerateInvoiceNumberAsync(db);

                var lineItem = new InvoiceLineItem
                {
                    ServiceId = service?.Id,
                    ServiceNameSnapshot = serviceName,
                    Description = serviceName,
                    Quantity = 1,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice,
                    RelatedVisitId = visitId,
                    SortOrder = 0
                };

                var invoice = new Invoice
                {
                    PatientId = visit.PatientId,
                    VisitId = visitId,
                    AppointmentId = visit.AppointmentId,
                    InvoiceNumber = invoiceNumber,
                    Status = InvoiceStatus.Draft,
                    Subtotal = unitPrice,
                    TotalAmount = unitPrice,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    LineItems = [lineItem]
                };

                db.Invoices.Add(invoice);

                // IMPORTANT: No Payment is created. No Contract is changed.
                // No patient balance is altered. Payments module remains source of truth.

                try
                {
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // Unique constraint on InvoiceNumber caught a duplicate.
                    // Roll back and retry with a fresh number.
                    await tx.RollbackAsync();
                    logger.LogWarning("CON-02: Invoice number collision on attempt {Attempt}, retrying", attempt + 1);
                    continue;
                }

                // Auto-fill commission defaults from service catalog
                if (lineItem.ServiceId.HasValue)
                {
                    try { await commissionService.AutoFillFromServiceAsync(lineItem.Id); }
                    catch (Exception ex) { logger.LogWarning(ex, "Commission auto-fill failed for line item {LineItemId}", lineItem.Id); }
                }

                return Ok(new
                {
                    invoice.Id,
                    invoice.InvoiceNumber,
                    Status = invoice.Status.ToString(),
                    StatusArabic = "مسودة",
                    invoice.Subtotal,
                    invoice.TotalAmount,
                    LineItemCount = invoice.LineItems.Count,
                    IsExisting = false,
                    message = "تم إنشاء الفاتورة المسودة بنجاح"
                });
            }
            catch (DbUpdateException)
            {
                // Re-throw if not a unique violation (already handled above)
                await tx.RollbackAsync();
                throw;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // All retries exhausted — this should never happen with advisory lock + unique index
        logger.LogError("CON-02: Failed to generate unique invoice number after {MaxRetries} attempts", maxRetries);
        return StatusCode(500, new { message = "فشل إنشاء رقم فاتورة فريد بعد عدة محاولات. يرجى المحاولة مرة أخرى." });
    }

    /// <summary>
    /// CON-02 FIX: Checks if a DbUpdateException is a PostgreSQL unique constraint violation (error code 23505).
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner != null)
        {
            if (inner.Message.Contains("23505") || inner.Message.Contains("duplicate key") ||
                inner.Message.Contains("unique constraint") || inner.Message.Contains("InvoiceNumber"))
                return true;
            inner = inner.InnerException;
        }
        return false;
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
