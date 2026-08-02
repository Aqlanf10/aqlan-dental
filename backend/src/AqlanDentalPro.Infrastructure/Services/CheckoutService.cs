using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.DTOs.Journey;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using static AqlanDentalPro.Infrastructure.Services.MvcResults;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// WRITE-side service for the Patient Journey Command Center.
/// Hosts every mutating endpoint: Intake → SendToQueue → StartVisit →
/// HandoffToReception → Checkout, plus CreateDraftInvoice,
/// ValidateFinancialClosure, and MarkLeftWithoutCompletion. Each write path
/// preserves the original transaction + Postgres advisory-lock pattern.
/// Extracted from PatientJourneyController (CLIN-22).
/// </summary>
public class CheckoutService(
    AppDbContext db,
    ICommissionService commissionService,
    ICurrentUserService currentUser,
    IClinicClock clinicClock,
    IJourneyBusinessDatePolicy businessDatePolicy,
    IPatientAccessService patientAccessService,
    IRealTimePushService pushService,
    ILogger<CheckoutService> logger)
{
    /// <summary>
    /// Backward-compatible constructor for existing unit/integration fixtures.
    /// Production DI selects the primary constructor above and injects the
    /// canonical clock and policy explicitly.
    /// </summary>
    public CheckoutService(
        AppDbContext db,
        ICommissionService commissionService,
        ICurrentUserService currentUser,
        IPatientAccessService patientAccessService,
        IRealTimePushService pushService,
        ILogger<CheckoutService> logger)
        : this(
            db,
            commissionService,
            currentUser,
            new ClinicClock(),
            new JourneyBusinessDatePolicy(new ClinicClock()),
            patientAccessService,
            pushService,
            logger)
    {
    }

    // IAuditService is injected per CLIN-22 spec for future audit-log migration.
    // The two existing AuditLog writes (MarkLeftWithoutCompletion, ValidateFinancialClosure)
    // intentionally keep direct db.AuditLogs.Add(...) so they remain in the same
    // SaveChangesAsync transaction as the visit/queue mutation — switching to
    // IAuditService.LogAsync would persist the audit row in a separate
    // SaveChangesAsync call and alter the transactional boundary.
    //
    // IPatientAccessService is injected (in addition to the CLIN-22 spec list) because
    // ValidateFinancialClosureAsync uses IsDoctor / HasFullAccess / IsReception to
    // gate the outstandingAmount field — preserved verbatim from the controller.
    //
    // IRealTimePushService is injected so successful journey mutations can push a
    // "JourneyUpdated" SignalR event to all connected daily-ops screens. The push is
    // best-effort: any failure is logged and swallowed so it never fails the HTTP
    // response. Push happens AFTER SaveChangesAsync + CommitAsync so clients always
    // re-fetch the committed state.

    /// <summary>
    /// Best-effort push of JourneyUpdated. Scoped to the caller's branch when
    /// resolvable (so TV displays in other branches don't get spammed); falls
    /// back to PushToAllAsync for admin callers without a branch. Never throws.
    /// </summary>
    private async Task PushJourneyUpdatedAsync(string action, Guid? appointmentId = null, Guid? patientId = null, Guid? visitId = null, Guid? branchId = null)
    {
        try
        {
            var payload = new { action, appointmentId, patientId, visitId, branchId };
            if (branchId.HasValue && branchId.Value != Guid.Empty)
                await pushService.PushToBranchAsync(branchId.Value, RealTimeEvents.JourneyUpdated, payload);
            else
                await pushService.PushToAllAsync(RealTimeEvents.JourneyUpdated, payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push JourneyUpdated ({Action})", action);
        }
    }

    private IActionResult? ValidateJourneyBusinessDate(
        Appointment appointment,
        IFutureAppointmentOverrideRequest? request,
        out JourneyBusinessDateDecision decision)
    {
        decision = businessDatePolicy.Evaluate(
            appointment.AppointmentDate,
            request?.OverrideFutureAppointment ?? false,
            request?.OverrideReason,
            currentUser.IsAdmin);

        if (decision.Allowed) return null;

        var payload = new
        {
            code = decision.ErrorCode,
            message = decision.Message,
            appointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
            businessDate = decision.BusinessDate.ToString("yyyy-MM-dd"),
            timeZone = clinicClock.TimeZoneId
        };

        return decision.ErrorCode == "future_appointment_override_forbidden"
            ? StatusCode(403, payload)
            : BadRequest(payload);
    }

    private void AddFutureAppointmentOverrideAudit(
        Appointment appointment,
        string operation,
        JourneyBusinessDateDecision decision)
    {
        if (!decision.Overridden) return;

        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            Action = AuditAction.Approve,
            Resource = "FutureAppointmentJourneyOverride",
            ResourceId = appointment.Id,
            NewData = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                operation,
                appointment.PatientId,
                appointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
                businessDate = decision.BusinessDate.ToString("yyyy-MM-dd"),
                reason = decision.OverrideReason,
                timeZone = clinicClock.TimeZoneId
            })),
            CreatedAt = clinicClock.UtcNow()
        });
    }

    // ─── 2. POST /api/patient-journey/{appointmentId}/intake ────────────────
    /// <summary>Reception confirms patient arrival and records intake info.</summary>
    /// <remarks>
    /// Sprint 2 FIX: Added transaction + advisory lock to prevent race condition
    /// that could allow double-intake when concurrent requests hit this endpoint.
    /// Pattern matches Sprint 1 SendToQueue/StartVisit fixes.
    /// </remarks>
    public async Task<IActionResult> IntakeAsync(Guid appointmentId, IntakeRequest req)
    {
        // Pre-flight validation (outside transaction for fast rejection)
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        var dateError = ValidateJourneyBusinessDate(appointment, req, out var dateDecision);
        if (dateError != null) return dateError;

        // Validate transition: Scheduled/Confirmed → Arrived
        var targetStatus = AppointmentStatus.Arrived;
        if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, targetStatus))
            return BadRequest(new { message = $"لا يمكن تسجيل الوصول لموعد بحالة {appointment.Status}" });

        // Relational production providers use a transaction + advisory lock to
        // serialize concurrent intake requests. EF InMemory has neither real
        // transactions nor raw SQL, so tests exercise the same state machine
        // without the cross-process lock.
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            if (tx != null)
            {
                // Acquire advisory lock scoped to this appointment
                var lockKey = StableLockKeyHelper.StableGuidToLong(appointmentId);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            // Re-check appointment status INSIDE the lock (to prevent race condition)
            await db.Entry(appointment).ReloadAsync();
            dateError = ValidateJourneyBusinessDate(appointment, req, out dateDecision);
            if (dateError != null)
            {
                if (tx != null) await tx.RollbackAsync();
                return dateError;
            }
            if (appointment.Status == AppointmentStatus.Arrived)
            {
                if (tx != null) await tx.RollbackAsync();
                return Conflict(new { message = "تم تسجيل وصول هذا المريض مسبقاً" });
            }
            if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, targetStatus))
            {
                if (tx != null) await tx.RollbackAsync();
                return BadRequest(new { message = $"لا يمكن تسجيل الوصول لموعد بحالة {appointment.Status}" });
            }

            // Update appointment
            var eventUtc = clinicClock.UtcNow();
            appointment.Status = targetStatus;
            appointment.ArrivedAt = eventUtc;
            appointment.UpdatedAt = eventUtc;

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

            AddFutureAppointmentOverrideAudit(appointment, "Intake", dateDecision);
            await db.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();

            // SignalR: best-effort push so daily-ops screens invalidate instantly.
            await PushJourneyUpdatedAsync("intake", appointmentId, appointment.PatientId, branchId: currentUser.BranchId);

            return Ok(new
            {
                appointment.Id,
                Status = appointment.Status.ToString(),
                appointment.ArrivedAt,
                ServiceId = appointment.ServiceId,
                message = "تم تسجيل وصول المريض بنجاح"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientJourney.Intake failed for appointment {AppointmentId}", appointmentId);
            return StatusCode(500, new { message = "حدث خطأ أثناء تسجيل الوصول" });
        }
    }

    // ─── 3. POST /api/patient-journey/{appointmentId}/send-to-queue ─────────
    /// <summary>Create or reuse queue item for the appointment.</summary>
    /// <remarks>
    /// Sprint 1 FIX: Added transaction + advisory lock to prevent race condition
    /// that could create duplicate queue items when concurrent requests hit this endpoint.
    /// Pattern matches ClinicQueueController.AddToQueue.
    /// </remarks>
    public async Task<IActionResult> SendToQueueAsync(Guid appointmentId, SendToQueueRequest? req = null)
    {
        // Pre-flight validation (outside transaction for fast rejection)
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        var dateError = ValidateJourneyBusinessDate(appointment, req, out var dateDecision);
        if (dateError != null) return dateError;

        // Must be Arrived or Waiting status
        if (appointment.Status != AppointmentStatus.Arrived && appointment.Status != AppointmentStatus.Waiting)
            return BadRequest(new { message = "يجب أن يكون المريض وصل أو في الانتظار قبل إضافته لقائمة الانتظار" });

        var today = dateDecision.BusinessDate;

        // Relational production providers use a transaction + advisory lock to
        // serialize concurrent clicks. InMemory tests have no transactions/raw SQL,
        // so they exercise the same state machine without the cross-process lock.
        var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            if (tx != null)
            {
                // Acquire advisory lock scoped to this patient (same as ClinicQueueController.AddToQueue)
                var lockKey = StableLockKeyHelper.StableGuidToLong(appointment.PatientId);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            await db.Entry(appointment).ReloadAsync();
            dateError = ValidateJourneyBusinessDate(appointment, req, out dateDecision);
            if (dateError != null)
            {
                if (tx != null) await tx.RollbackAsync();
                return dateError;
            }
            // The request may have waited on the advisory lock across clinic midnight.
            // Always use the business date from the decision made under the lock.
            today = dateDecision.BusinessDate;
            // Re-check for existing active queue item for this appointment (inside lock)
            var existingQueueItem = await db.ClinicQueueItems
                .FirstOrDefaultAsync(q => q.AppointmentId == appointmentId && q.QueueDate == today
                    && q.Status != ClinicQueueStatus.Completed
                    && q.Status != ClinicQueueStatus.Cancelled
                    && q.IsActive);

            if (existingQueueItem != null)
            {
                // Idempotent repair path for legacy same-day booking conversions
                // that pre-created a Waiting queue row before reception recorded
                // arrival. Reuse the row and complete Arrived -> Waiting instead
                // of trapping the appointment in Arrived with a duplicate error.
                existingQueueItem.DoctorId = appointment.DoctorId;
                existingQueueItem.ServiceId = appointment.ServiceId;
                existingQueueItem.ClinicRoomId = appointment.ClinicRoomId;
                existingQueueItem.RoomName = appointment.RoomName;
                if (!string.IsNullOrWhiteSpace(req?.Notes))
                    existingQueueItem.Notes = req.Notes;

                if (AppointmentStatusTransitions.IsValidTransition(
                        appointment.Status, AppointmentStatus.Waiting))
                {
                    appointment.Status = AppointmentStatus.Waiting;
                    appointment.UpdatedAt = DateTime.UtcNow;
                }

                AddFutureAppointmentOverrideAudit(appointment, "SendToQueue", dateDecision);
                await db.SaveChangesAsync();
                if (tx != null) await tx.CommitAsync();

                await PushJourneyUpdatedAsync(
                    "send-to-queue", appointment.Id, appointment.PatientId,
                    visitId: existingQueueItem.Id, branchId: currentUser.BranchId);

                return Ok(new
                {
                    existingQueueItem.Id,
                    QueueStatus = existingQueueItem.Status.ToString(),
                    StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(existingQueueItem.Status),
                    message = "المريض موجود في قائمة الانتظار وتم تحديث حالته بنجاح"
                });
            }

            // Also check for duplicate queue item by patient for today (like ClinicQueueController.AddToQueue)
            var activeStatuses = new HashSet<ClinicQueueStatus>
            {
                ClinicQueueStatus.Waiting,
                ClinicQueueStatus.Called,
                ClinicQueueStatus.InRoom,
                ClinicQueueStatus.InProgress
            };
            var duplicatePatientQueue = await db.ClinicQueueItems
                .AnyAsync(q => q.PatientId == appointment.PatientId && q.QueueDate == today
                    && activeStatuses.Contains(q.Status)
                    && q.IsActive);

            if (duplicatePatientQueue)
            {
                if (tx != null) await tx.RollbackAsync();
                return Conflict(new { message = "يوجد عنصر نشط لهذا المريض في قائمة الانتظار اليوم بالفعل" });
            }

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
                ServiceId = appointment.ServiceId,
                ClinicRoomId = appointment.ClinicRoomId,
                RoomName = roomName,
                Status = ClinicQueueStatus.Waiting,
                QueueDate = today,
                AddedByUserId = currentUser.UserId,
                Notes = req?.Notes
            };

            db.ClinicQueueItems.Add(queueItem);

            // Update appointment status to Waiting
            if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Waiting))
            {
                appointment.Status = AppointmentStatus.Waiting;
                appointment.UpdatedAt = DateTime.UtcNow;
            }

            AddFutureAppointmentOverrideAudit(appointment, "SendToQueue", dateDecision);
            await db.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();

            // SignalR: best-effort push so daily-ops screens invalidate instantly.
            await PushJourneyUpdatedAsync("send-to-queue", appointment.Id, appointment.PatientId, visitId: queueItem.Id, branchId: currentUser.BranchId);

            return Ok(new
            {
                queueItem.Id,
                QueueStatus = queueItem.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(queueItem.Status),
                message = "تمت إضافة المريض إلى قائمة الانتظار بنجاح"
            });
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }

    // ─── 4. POST /api/patient-journey/{appointmentId}/start-visit ───────────
    /// <summary>Doctor starts the visit. Reuses existing visit/queue logic.</summary>
    /// <remarks>
    /// Sprint 1 FIX: Added transaction + advisory lock to prevent race condition
    /// that could create duplicate visits when concurrent requests hit this endpoint.
    /// Pattern matches ClinicQueueController.StartVisit.
    /// </remarks>
    public async Task<IActionResult> StartVisitAsync(Guid appointmentId, StartVisitRequest? req = null)
    {
        // Pre-flight validation (outside transaction for fast rejection)
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        var dateError = ValidateJourneyBusinessDate(appointment, req, out var dateDecision);
        if (dateError != null) return dateError;

        // Must be in InRoom, Called, or Arrived (with queue in InRoom/Called) status
        // FIX: Accept Arrived status because SyncAppointmentStatus can be blocked by transition rules,
        // leaving appointment.Status=Arrived while queueStatus=InRoom/Called (divergence is expected per SEC-01).
        var queueItemCheck = await db.ClinicQueueItems
            .FirstOrDefaultAsync(q => q.AppointmentId == appointmentId
                && q.IsActive
                && q.Status != ClinicQueueStatus.Completed
                && q.Status != ClinicQueueStatus.Cancelled);

        var isInRoomOrCalled = appointment.Status == AppointmentStatus.InRoom || appointment.Status == AppointmentStatus.Called;
        var isArrivedWithQueueInRoom = appointment.Status == AppointmentStatus.Arrived
            && queueItemCheck != null
            && (queueItemCheck.Status == ClinicQueueStatus.InRoom || queueItemCheck.Status == ClinicQueueStatus.Called);

        if (!isInRoomOrCalled && !isArrivedWithQueueInRoom)
            return BadRequest(new { message = "يجب أن يكون المريض داخل الغرفة قبل بدء الزيارة" });

        var today = dateDecision.BusinessDate;

        // Sprint 1 FIX: Wrap in transaction + advisory lock to prevent duplicate visits
        var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            if (tx != null)
            {
                // Acquire the same stable cross-process lock on every visit-start route.
                var lockKey = StableLockKeyHelper.StableGuidToLong(appointmentId);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            await db.Entry(appointment).ReloadAsync();
            dateError = ValidateJourneyBusinessDate(appointment, req, out dateDecision);
            if (dateError != null)
            {
                if (tx != null) await tx.RollbackAsync();
                return dateError;
            }
            // Rebind after acquiring the lock so queue/visit dates cannot retain
            // the previous clinic day when the request crosses midnight.
            today = dateDecision.BusinessDate;
            // Re-check for existing visit INSIDE the lock (to prevent race condition)
            var existingVisit = await db.Visits
                .FirstOrDefaultAsync(v => v.AppointmentId == appointmentId && v.IsActive);

            // Find the queue item for this appointment (inside transaction for consistency)
            var queueItem = await db.ClinicQueueItems
                .FirstOrDefaultAsync(q => q.AppointmentId == appointmentId && q.QueueDate == today
                    && q.IsActive
                    && q.Status != ClinicQueueStatus.Completed
                    && q.Status != ClinicQueueStatus.Cancelled);

            if (queueItem == null)
            {
                if (tx != null) await tx.RollbackAsync();
                return BadRequest(new { message = "لا يوجد عنصر انتظار نشط لهذا الموعد" });
            }

            // Validate queue transition to InProgress
            var validationError = ClinicQueueStatusTransitions.GetValidationError(queueItem.Status, ClinicQueueStatus.InProgress);
            if (validationError != null)
            {
                if (tx != null) await tx.RollbackAsync();
                return BadRequest(new { message = validationError });
            }

            // Normalize inside the lock, after ReloadAsync. Moving this transition
            // before ReloadAsync loses it and leaves the appointment stuck at Arrived.
            if (appointment.Status is AppointmentStatus.Arrived or AppointmentStatus.Called
                && AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.InRoom))
            {
                appointment.Status = AppointmentStatus.InRoom;
                appointment.UpdatedAt = clinicClock.UtcNow();
            }

            // All rejection gates have passed; only successful operations are audited.
            AddFutureAppointmentOverrideAudit(appointment, "StartVisit", dateDecision);

            if (existingVisit != null)
            {
                // Visit already exists, just update statuses
                var eventUtc = clinicClock.UtcNow();
                queueItem.Status = ClinicQueueStatus.InProgress;
                queueItem.StartedAt = eventUtc;
                queueItem.UpdatedAt = eventUtc;

                if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.InProgress))
                {
                    appointment.Status = AppointmentStatus.InProgress;
                    appointment.UpdatedAt = eventUtc;
                }

                await db.SaveChangesAsync();
                if (tx != null) await tx.CommitAsync();

                // SignalR: best-effort push so daily-ops screens invalidate instantly.
                await PushJourneyUpdatedAsync("start-visit", appointment.Id, appointment.PatientId, visitId: existingVisit.Id, branchId: currentUser.BranchId);

                return Ok(new
                {
                    existingVisit.Id,
                    QueueItemId = queueItem.Id,
                    QueueStatus = queueItem.Status.ToString(),
                    AppointmentStatus = appointment.Status.ToString(),
                    VisitStatus = existingVisit.CheckoutStatus ?? "InProgress",
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
                ServiceId = appointment.ServiceId,
                OrthoCaseId = appointment.OrthoCaseId
            };

            // FIX: Populate AmountDueReference from the service catalog price
            if (appointment.ServiceId.HasValue)
            {
                var service = await db.ClinicServices.FindAsync(appointment.ServiceId.Value);
                if (service != null && service.DefaultPrice > 0)
                    visit.AmountDueReference = service.DefaultPrice;
            }

            db.Visits.Add(visit);

            try
            {
                await db.SaveChangesAsync(); // Save to get the visit ID
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to create visit for appointment {AppointmentId}", appointmentId);
                if (tx != null) await tx.RollbackAsync();
                return StatusCode(500, new { message = "فشل إنشاء الزيارة — يرجى المحاولة مرة أخرى" });
            }

            // Update queue item
            var startedAt = clinicClock.UtcNow();
            queueItem.VisitId = visit.Id;
            queueItem.Status = ClinicQueueStatus.InProgress;
            queueItem.StartedAt = startedAt;
            queueItem.UpdatedAt = startedAt;

            // Update appointment
            if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.InProgress))
            {
                appointment.Status = AppointmentStatus.InProgress;
                appointment.UpdatedAt = startedAt;
            }

            await db.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();

            // SignalR: best-effort push so daily-ops screens invalidate instantly.
            await PushJourneyUpdatedAsync("start-visit", appointment.Id, appointment.PatientId, visitId: visit.Id, branchId: currentUser.BranchId);

            return Ok(new
            {
                visit.Id,
                QueueItemId = queueItem.Id,
                QueueStatus = queueItem.Status.ToString(),
                AppointmentStatus = appointment.Status.ToString(),
                VisitStatus = visit.CheckoutStatus ?? "InProgress",
                AmountDueReference = visit.AmountDueReference,
                message = "تم بدء الزيارة بنجاح"
            });
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }

    // ─── 5. POST /api/patient-journey/{visitId}/handoff-to-reception ────────
    /// <summary>Doctor finishes and sends patient to reception for checkout.</summary>
    /// <remarks>
    /// Sprint 2 FIX: Added transaction + advisory lock to prevent race condition
    /// that could allow double-handoff when concurrent requests hit this endpoint.
    /// Also restricted to Doctor+Admin only (was StaffOnly) and added InProgress validation.
    /// </remarks>
    public async Task<IActionResult> HandoffToReceptionAsync(Guid visitId, HandoffRequest req)
    {
        // Pre-flight validation (outside transaction for fast rejection)
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });

        // FIX: Guard against overwriting terminal checkout states
        if (visit.CheckoutStatus == "CheckedOut")
            return BadRequest(new { message = "لا يمكن تسليم زيارة مكتملة الفاتورة للاستقبال" });
        if (PatientJourneyService.IsLeftWithoutCompletionCheckoutStatus(visit.CheckoutStatus))
            return BadRequest(new { message = "لا يمكن تسليم زيارة مغادرة بدون إكمال" });

        // Sprint 2 FIX: Validate visit is actually InProgress before allowing handoff.
        // Only visits with null CheckoutStatus (actively being treated) or "InProgress" can be handed off.
        if (visit.CheckoutStatus == "ReadyForCheckout")
            return Conflict(new { message = "الزيارة جاهزة للتحصيل بالفعل — تم تسليمها للاستقبال مسبقاً" });

        // Sprint 2 FIX: Wrap in transaction + advisory lock to prevent double-handoff
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire advisory lock scoped to this visit
            var lockKey = (int)(visitId.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Re-check CheckoutStatus INSIDE the lock (to prevent race condition)
            await db.Entry(visit).ReloadAsync();
            if (visit.CheckoutStatus == "ReadyForCheckout")
            {
                await tx.RollbackAsync();
                return Conflict(new { message = "الزيارة جاهزة للتحصيل بالفعل — تم تسليمها للاستقبال مسبقاً" });
            }
            if (visit.CheckoutStatus == "CheckedOut")
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "لا يمكن تسليم زيارة مكتملة الفاتورة للاستقبال" });
            }
            if (PatientJourneyService.IsLeftWithoutCompletionCheckoutStatus(visit.CheckoutStatus))
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "لا يمكن تسليم زيارة مغادرة بدون إكمال" });
            }

            // Update visit clinical data (F3: map structured fields to existing Visit fields)
            if (!string.IsNullOrWhiteSpace(req.ChiefComplaint))
                visit.ChiefComplaint = req.ChiefComplaint;
            if (!string.IsNullOrWhiteSpace(req.TreatmentDone))
                visit.TreatmentDone = req.TreatmentDone;
            if (!string.IsNullOrWhiteSpace(req.Diagnosis))
                visit.Diagnosis = req.Diagnosis;
            if (!string.IsNullOrWhiteSpace(req.NextVisitPlan))
                visit.NextVisitPlan = req.NextVisitPlan;
            if (!string.IsNullOrWhiteSpace(req.Instructions))
                visit.Instructions = req.Instructions;

            // F3: Append extraoral/intraoral examination notes into ClinicalNotes with Arabic labels
            var clinicalNotesParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(req.ExtraoralExamination))
                clinicalNotesParts.Add($"[فحص خارج الفم] {req.ExtraoralExamination}");
            if (!string.IsNullOrWhiteSpace(req.IntraoralExamination))
                clinicalNotesParts.Add($"[فحص داخل الفم] {req.IntraoralExamination}");

            if (clinicalNotesParts.Count > 0)
            {
                var appendedNotes = string.Join(" | ", clinicalNotesParts);
                visit.ClinicalNotes = string.IsNullOrWhiteSpace(visit.ClinicalNotes)
                    ? appendedNotes
                    : $"{visit.ClinicalNotes} | {appendedNotes}";
            }

            // F1: Append handoff notes to ClinicalNotes with Arabic label (don't overwrite)
            if (!string.IsNullOrWhiteSpace(req.Notes))
            {
                var handoffLabel = $"[ملاحظات التسليم] {req.Notes}";
                visit.ClinicalNotes = string.IsNullOrWhiteSpace(visit.ClinicalNotes)
                    ? handoffLabel
                    : $"{visit.ClinicalNotes} | {handoffLabel}";
            }

            // F6: First service goes to Visit.ServiceId; additional services text appended to ClinicalNotes
            if (req.SuggestedServiceId.HasValue)
                visit.ServiceId = req.SuggestedServiceId;
            if (!string.IsNullOrWhiteSpace(req.AdditionalServicesText))
            {
                var additionalLabel = $"[خدمات إضافية] {req.AdditionalServicesText}";
                visit.ClinicalNotes = string.IsNullOrWhiteSpace(visit.ClinicalNotes)
                    ? additionalLabel
                    : $"{visit.ClinicalNotes} | {additionalLabel}";
            }

            if (req.FollowUpDate.HasValue)
                visit.NextVisitDate = req.FollowUpDate;
            if (req.AmountDue.HasValue)
                visit.AmountDueReference = req.AmountDue;
            // FIX: Fallback — if AmountDueReference is still null, look up service default price
            else if (!visit.AmountDueReference.HasValue)
            {
                var serviceId = visit.ServiceId ?? visit.Appointment?.ServiceId;
                if (serviceId.HasValue)
                {
                    var svc = await db.ClinicServices.FindAsync(serviceId.Value);
                    if (svc != null && svc.DefaultPrice > 0)
                        visit.AmountDueReference = svc.DefaultPrice;
                }
            }
            if (!string.IsNullOrWhiteSpace(req.ProposedProcedure))
                visit.ProposedProcedure = req.ProposedProcedure;

            // Mark as ready for checkout
            visit.CheckoutStatus = "ReadyForCheckout";
            visit.ReadyForCheckoutAt = DateTime.UtcNow;
            visit.UpdatedAt = DateTime.UtcNow;

            // Update queue item if exists
            if (visit.AppointmentId.HasValue)
            {
                var today = ClinicTimeProvider.ClinicToday();
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
            await tx.CommitAsync();

            // SignalR: best-effort push so daily-ops screens invalidate instantly.
            await PushJourneyUpdatedAsync("handoff", visit.AppointmentId, visit.PatientId, visitId: visit.Id, branchId: currentUser.BranchId);

            return Ok(new
            {
                visit.Id,
                AppointmentId = visit.AppointmentId,
                VisitStatus = visit.CheckoutStatus ?? "InProgress",
                CheckoutStatus = visit.CheckoutStatus,
                ReadyForCheckoutAt = visit.ReadyForCheckoutAt,
                AmountDueReference = visit.AmountDueReference,
                ProposedProcedure = visit.ProposedProcedure,
                message = "تم تسليم المريض للاستقبال بنجاح"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientJourney.HandoffToReception failed for visit {VisitId}", visitId);
            return StatusCode(500, new { message = "حدث خطأ أثناء تسليم المريض للاستقبال" });
        }
    }

    // ─── 6. POST /api/patient-journey/{id}/checkout ──────────────
    /// <summary>Complete checkout by appointmentId or visitId.
    /// First attempts to find by appointmentId; if not found, tries by visitId.
    /// This supports both appointment-based and walk-in patients.</summary>
    /// <remarks>
    /// Sprint 2 FIX: Added transaction + advisory lock to prevent race condition
    /// that could allow double-checkout when concurrent requests hit this endpoint.
    /// Pattern matches Sprint 1 SendToQueue/StartVisit and Sprint 2 Intake/Handoff fixes.
    /// </remarks>
    public async Task<IActionResult> CheckoutAsync(Guid id, CheckoutRequest? req)
    {
        req ??= new CheckoutRequest();

        // Pre-flight validation (outside transaction for fast rejection)
        // Try appointment-based lookup first
        var appointment = await db.Appointments.FindAsync(id);
        Visit? visit = null;

        if (appointment != null && appointment.IsActive)
        {
            visit = await db.Visits
                .Include(v => v.Appointment)
                .FirstOrDefaultAsync(v => v.AppointmentId == id && v.IsActive);
        }
        else
        {
            // Fallback: lookup by visitId (walk-in patients)
            visit = await db.Visits
                .Include(v => v.Appointment)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (visit != null)
                appointment = visit.Appointment;
        }

        if (visit == null)
            return BadRequest(new { message = "لا توجد زيارة مرتبطة" });

        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });

        if (visit.CheckoutStatus == "CheckedOut")
            return Conflict(new { message = "تم إنهاء حساب هذه الزيارة مسبقاً" });

        if (visit.CheckoutStatus != "ReadyForCheckout")
            return BadRequest(new { message = "الزيارة ليست جاهزة للحساب بعد" });

        // Sprint 2 FIX: Wrap in transaction + advisory lock to prevent double-checkout
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire advisory lock scoped to this visit
            var lockKey = (int)(visit.Id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Re-check CheckoutStatus INSIDE the lock (to prevent race condition)
            await db.Entry(visit).ReloadAsync();
            if (visit.CheckoutStatus == "CheckedOut")
            {
                await tx.RollbackAsync();
                return Conflict(new { message = "تم إنهاء حساب هذه الزيارة مسبقاً" });
            }
            if (visit.CheckoutStatus != "ReadyForCheckout")
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "الزيارة ليست جاهزة للحساب بعد" });
            }

            // Mark visit as checked out
            visit.CheckoutStatus = "CheckedOut";
            visit.UpdatedAt = DateTime.UtcNow;

            // Complete appointment if linked and valid
            if (appointment != null && appointment.IsActive)
            {
                // Reload appointment inside transaction for consistency
                await db.Entry(appointment).ReloadAsync();
                if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Completed))
                {
                    appointment.Status = AppointmentStatus.Completed;
                    appointment.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Complete queue item if still active
            if (visit.AppointmentId.HasValue)
            {
                var today = ClinicTimeProvider.ClinicToday();
                var queueItem = await db.ClinicQueueItems
                    .FirstOrDefaultAsync(q => q.AppointmentId == visit.AppointmentId && q.QueueDate == today && q.IsActive
                        && q.Status != ClinicQueueStatus.Completed && q.Status != ClinicQueueStatus.Cancelled);
                if (queueItem != null && ClinicQueueStatusTransitions.IsValidTransition(queueItem.Status, ClinicQueueStatus.Completed))
                {
                    queueItem.Status = ClinicQueueStatus.Completed;
                    queueItem.CompletedAt = DateTime.UtcNow;
                    queueItem.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // SignalR: best-effort push so daily-ops screens invalidate instantly.
            await PushJourneyUpdatedAsync("checkout", appointment?.Id, visit.PatientId, visitId: visit.Id, branchId: currentUser.BranchId);

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
                AppointmentId = appointment?.Id,
                VisitId = visit.Id,
                CheckoutStatus = visit.CheckoutStatus,
                AppointmentStatus = appointment?.Status.ToString(),
                NextActions = nextActions,
                message = "تم إنهاء الحساب بنجاح"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientJourney.Checkout failed for id {Id}", id);
            return StatusCode(500, new { message = "حدث خطأ أثناء إنهاء الحساب" });
        }
    }

    // ─── 7. POST /api/patient-journey/{visitId}/left-without-completion ─────
    /// <summary>Marks a visit as an explicit terminal operational state when
    /// the patient leaves after arrival without completing care. This does not
    /// create payments, alter invoices, or change financial balances.</summary>
    public async Task<IActionResult> MarkLeftWithoutCompletionAsync(Guid visitId, LeftWithoutCompletionRequest req)
    {
        var reason = req.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "سبب الخروج بدون إكمال مطلوب" });

        var status = string.IsNullOrWhiteSpace(req.Status)
            ? "LeftWithoutCompletion"
            : req.Status.Trim();

        if (!PatientJourneyService.IsLeftWithoutCompletionCheckoutStatus(status))
            return BadRequest(new { message = "حالة الخروج بدون إكمال غير صالحة" });

        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });
        if (visit.CheckoutStatus == "CheckedOut")
            return BadRequest(new { message = "لا يمكن تعليم زيارة مكتملة كمغادرة بدون إكمال" });

        var previousCheckoutStatus = visit.CheckoutStatus;
        var previousAppointmentStatus = visit.Appointment?.Status.ToString();
        var now = DateTime.UtcNow;

        visit.CheckoutStatus = status;
        visit.UpdatedAt = now;

        ClinicQueueItem? queueItem = null;
        if (visit.AppointmentId.HasValue)
        {
            queueItem = await db.ClinicQueueItems
                .Where(q => q.AppointmentId == visit.AppointmentId.Value
                    && q.IsActive
                    && q.Status != ClinicQueueStatus.Completed
                    && q.Status != ClinicQueueStatus.Cancelled
                    && q.Status != ClinicQueueStatus.NoShow)
                .OrderByDescending(q => q.UpdatedAt)
                .ThenByDescending(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (queueItem != null)
            {
                queueItem.Status = ClinicQueueStatus.Cancelled;
                queueItem.CancelledAt = now;
                queueItem.UpdatedAt = now;
            }

            // CLIN-06 FIX: Transition the appointment to Cancelled so it doesn't stay InProgress
            // forever (clogging the doctor's calendar + recall worklists). The queue item was cancelled
            // above but the appointment itself was never touched. AppointmentStatusTransitions allows
            // InProgress → Cancelled; other non-terminal statuses (Arrived, Waiting, Called, InRoom) also
            // allow → Cancelled. Terminal states (Completed, Cancelled, NoShow) are left as-is.
            if (visit.Appointment != null)
            {
                var appt = visit.Appointment;
                if (appt.Status != AppointmentStatus.Completed
                    && appt.Status != AppointmentStatus.Cancelled
                    && appt.Status != AppointmentStatus.NoShow)
                {
                    if (AppointmentStatusTransitions.IsValidTransition(appt.Status, AppointmentStatus.Cancelled))
                    {
                        appt.Status = AppointmentStatus.Cancelled;
                        appt.UpdatedAt = now;
                    }
                }
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            Resource = "Visit.LeftWithoutCompletion",
            ResourceId = visit.Id,
            Action = AuditAction.Update,
            UserId = currentUser.UserId,
            OldData = JsonSerializer.SerializeToDocument(new
            {
                checkoutStatus = previousCheckoutStatus,
                appointmentStatus = previousAppointmentStatus
            }),
            NewData = JsonSerializer.SerializeToDocument(new
            {
                checkoutStatus = status,
                reason,
                visit.PatientId,
                visit.AppointmentId,
                queueItemId = queueItem?.Id
            })
        });

        await db.SaveChangesAsync();

        // SignalR: best-effort push so daily-ops screens invalidate instantly.
        await PushJourneyUpdatedAsync("left-without-completion", visit.AppointmentId, visit.PatientId, visitId: visit.Id, branchId: currentUser.BranchId);

        return Ok(new
        {
            VisitId = visit.Id,
            visit.AppointmentId,
            CheckoutStatus = visit.CheckoutStatus,
            Reason = reason,
            QueueStatus = queueItem?.Status.ToString(),
            message = "تم تسجيل خروج المريض بدون إكمال"
        });
    }

    /// <summary>Creates a Draft Invoice from a visit that is ready for checkout.
    /// Uses Visit.AmountDueReference and linked ServiceId for line item pricing.
    /// Does NOT create a Payment. Does NOT alter Contract or Patient balance.
    /// If a Draft invoice already exists for this Visit, returns the existing one.
    /// Uses transaction + advisory lock + GenerateInvoiceNumberAsync
    /// + commissionService.AutoFillFromServiceAsync to match main branch safe behavior.
    /// Re-checks for duplicate draft inside the transaction to prevent race conditions.</summary>
    public async Task<IActionResult> CreateDraftInvoiceAsync(Guid visitId)
    {
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });

        // Fast-path duplicate prevention: check before acquiring lock
        var existingDraft = await db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

        if (existingDraft != null)
        {
            return Ok(new
            {
                IsExisting = true,
                existingDraft.Id,
                existingDraft.InvoiceNumber,
                Status = existingDraft.Status.ToString(),
                StatusArabic = GetInvoiceStatusArabic(existingDraft.Status),
                existingDraft.Subtotal,
                existingDraft.TotalAmount,
                LineItemCount = existingDraft.LineItems.Count,
                message = "فاتورة مسودة موجودة مسبقاً"
            });
        }

        // Determine amounts
        var lineAmount = visit.AmountDueReference ?? visit.Cost ?? 0;
        if (lineAmount <= 0)
            return BadRequest(new { message = "لا يمكن إنشاء فاتورة بمبلغ صفر — حدد المبلغ المستحق أولاً" });

        // Get service name for line item description + ServiceId fallback from appointment
        string lineDescription = "زيارة طبية";
        Guid? lineServiceId = visit.ServiceId;

        // Fallback: if visit has no ServiceId, try appointment's ServiceId
        if (!lineServiceId.HasValue && visit.Appointment?.ServiceId.HasValue == true)
            lineServiceId = visit.Appointment.ServiceId;

        if (lineServiceId.HasValue)
        {
            var svc = await db.ClinicServices.FindAsync(lineServiceId.Value);
            if (svc != null)
                lineDescription = svc.ArabicName ?? svc.Code ?? lineDescription;
        }

        // Use transaction + advisory lock (matching main branch InvoicesController.Create)
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = (int)(DateTime.UtcNow.ToString("yyyyMMdd").GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Fix-1: Re-check for existing draft INSIDE the transaction after advisory lock
            // to prevent two concurrent requests from creating duplicate drafts
            var inTxExistingDraft = await db.Invoices
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

            if (inTxExistingDraft != null)
            {
                await tx.RollbackAsync();
                return Ok(new
                {
                    IsExisting = true,
                    inTxExistingDraft.Id,
                    inTxExistingDraft.InvoiceNumber,
                    Status = inTxExistingDraft.Status.ToString(),
                    StatusArabic = GetInvoiceStatusArabic(inTxExistingDraft.Status),
                    inTxExistingDraft.Subtotal,
                    inTxExistingDraft.TotalAmount,
                    LineItemCount = inTxExistingDraft.LineItems.Count,
                    message = "فاتورة مسودة موجودة مسبقاً"
                });
            }

            // Use local GenerateInvoiceNumberAsync (same logic as InvoicesController.GenerateInvoiceNumberAsync)
            var invoiceNumber = await GenerateInvoiceNumberAsync(db);
            var userId = currentUser.UserId;

            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                PatientId = visit.PatientId,
                VisitId = visitId,
                AppointmentId = visit.AppointmentId,
                Status = InvoiceStatus.Draft,
                Notes = $"فاتورة مسودة من زيارة يوم {visit.VisitDate:yyyy-MM-dd}",
                CreatedBy = userId,
                UpdatedBy = userId
            };

            db.Invoices.Add(invoice);

            // Add line item
            var lineItem = new InvoiceLineItem
            {
                InvoiceId = invoice.Id,
                ServiceId = lineServiceId,
                Description = lineDescription,
                ServiceNameSnapshot = lineDescription,
                Quantity = 1,
                UnitPrice = lineAmount,
                TotalPrice = lineAmount,
                RelatedVisitId = visitId,
                // Attribute the service to the visit doctor so its calculated
                // commission is included in doctor commission reports.
                DoctorId = visit.DoctorId ?? visit.Appointment?.DoctorId,
                SortOrder = 0
            };
            db.InvoiceLineItems.Add(lineItem);

            await db.SaveChangesAsync();

            // Auto-fill commission defaults from service catalog entry (matching main branch)
            try { await commissionService.AutoFillFromServiceAsync(lineItem.Id); }
            catch (Exception ex) { logger.LogWarning(ex, "Commission auto-fill failed for line item {LineItemId}", lineItem.Id); }

            // Recalculate Subtotal from line items (matching main branch)
            var allLineItems = await db.InvoiceLineItems
                .Where(l => l.InvoiceId == invoice.Id && l.IsActive)
                .ToListAsync();
            invoice.Subtotal = allLineItems.Sum(l => l.TotalPrice);
            invoice.TotalAmount = invoice.Subtotal; // No discount/tax in draft from visit

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // SignalR: best-effort push so daily-ops + finance screens invalidate instantly.
            await PushJourneyUpdatedAsync("create-draft-invoice", visit.AppointmentId, visit.PatientId, visitId: visit.Id, branchId: currentUser.BranchId);

            // Preserve response fields: IsExisting, StatusArabic, Subtotal, TotalAmount, LineItemCount
            return Ok(new
            {
                IsExisting = false,
                invoice.Id,
                invoice.InvoiceNumber,
                Status = invoice.Status.ToString(),
                StatusArabic = GetInvoiceStatusArabic(invoice.Status),
                invoice.Subtotal,
                invoice.TotalAmount,
                LineItemCount = allLineItems.Count,
                message = "تم إنشاء الفاتورة المسودة بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── 8. POST /api/patient-journey/{patientId}/validate-financial-closure ─
    /// <summary>
    /// Validates whether a visit can be financially closed.
    /// Checks outstanding balance and treatment plan status.
    /// </summary>
    public async Task<IActionResult> ValidateFinancialClosureAsync(
        Guid patientId,
        ValidateFinancialClosureRequest req)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

        // Sprint Patient-Finance-Ledger: Hide financial details from doctors
        // Admin, Accountant (HasFullAccess) and Reception (handles checkout) may see amounts
        var canViewFinanceAmounts = !patientAccessService.IsDoctor &&
            (patientAccessService.HasFullAccess || patientAccessService.IsReception);

        // Get the latest active visit
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.PatientId == patientId && v.IsActive && v.CheckoutStatus != "CheckedOut");

        if (visit == null) return Ok(new { canClose = true, reason = "" });

        // Get outstanding balance for this patient
        var invoices = await db.Invoices
            .Include(i => i.Payments.Where(p => p.IsActive))
            .Include(i => i.LineItems.Where(l => l.IsActive))
            .Where(i => i.PatientId == patientId && i.IsActive && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync();

        // DAILY-OPS-AUDIT FIX: A Draft invoice is a work-in-progress document with no
        // financial commitment yet (mirrors the Lab module's "sent = commitment, draft = no
        // commitment" rule, and matches how FinanceReadService/PatientSegmentsController already
        // exclude Draft invoices from debt totals). Only Issued/Paid invoices represent a real
        // obligation. Previously ALL non-cancelled invoices (including Draft) counted toward
        // totalInvoiced here, so an in-progress draft could block a patient's checkout with a
        // phantom "outstanding balance" for an amount nobody has actually been billed yet.
        var committedInvoices = invoices.Where(i => i.Status != InvoiceStatus.Draft).ToList();

        var totalInvoiced = committedInvoices.Sum(i => i.TotalAmount);
        var totalPaid = committedInvoices.Sum(i => i.Payments.Sum(p => p.Amount));
        // QA-594: also include performed-but-unbilled visits. Without this, a
        // patient who had a 50k root-canal session and paid 20k (no invoice)
        // would pass closure with "الرصيد متساوي" while still owing 30k.
        // NOTE: billedVisitIdsSet must also come from committedInvoices only — if a visit is
        // only covered by a Draft invoice (no real commitment), it must still fall through to
        // the unbilledVisitsAmount branch below via AmountDueReference, otherwise the draft
        // would silently hide real outstanding debt from the closure check.
        var billedVisitIdsSet = committedInvoices
            .Where(i => i.VisitId.HasValue)
            .Select(i => i.VisitId!.Value)
            .ToHashSet();
        var unbilledVisitRows = await db.Visits
            .Where(v => v.PatientId == patientId && v.IsActive
                     && v.AmountDueReference.HasValue && v.AmountDueReference > 0)
            .ToListAsync();
        var unbilledVisitsAmount = unbilledVisitRows
            .Where(v => !billedVisitIdsSet.Contains(v.Id))
            .Sum(v => v.AmountDueReference ?? 0m);
        var outstanding = totalInvoiced - totalPaid + unbilledVisitsAmount;

        if (outstanding <= 0)
        {
            return Ok(new { canClose = true, reason = "الرصيد متساوي", outstandingAmount = canViewFinanceAmounts ? 0 : (decimal?)null, unbilledVisitsAmount = canViewFinanceAmounts ? unbilledVisitsAmount : (decimal?)null });
        }

        // Check if there's a multi-session treatment plan (active ortho case or in-progress general treatment items)
        var hasActiveOrthoCase = await db.OrthoCases
            .AnyAsync(o => o.PatientId == patientId && o.IsActive && o.Status == OrthoCaseStatus.Active);

        var hasActiveGeneralPlan = await db.GeneralTreatmentPlanItems
            .AnyAsync(g => g.PatientId == patientId && g.IsActive && g.Status == "in_progress");

        if (hasActiveOrthoCase || hasActiveGeneralPlan)
        {
            return Ok(new
            {
                canClose = true,
                reason = "خطة علاج متعددة الجلسات",
                reasonCode = "MULTI_SESSION_PLAN",
                outstandingAmount = canViewFinanceAmounts ? outstanding : (decimal?)null,
                unbilledVisitsAmount = canViewFinanceAmounts ? unbilledVisitsAmount : (decimal?)null,
                requiresAuditLog = true
            });
        }

        // If completed visit with outstanding and no plan
        if (req.ManagerOverride)
        {
            // Reception may validate the balance, but cannot approve its own
            // exception. Overrides are an accounting/management decision.
            if (!currentUser.IsAdmin && currentUser.Role != UserRole.Accountant)
            {
                return new ObjectResult(new
                {
                    message = "لا يحق لك اعتماد تجاوز الرصيد المتبقي. يلزم مدير أو محاسب."
                })
                {
                    StatusCode = 403
                };
            }

            // Log audit for manager override
            var userId = currentUser.UserId;
            db.AuditLogs.Add(new AuditLog
            {
                Resource = "Visit.FinancialClosure",
                ResourceId = visit.Id,
                Action = AuditAction.Approve,
                UserId = userId,
                NewData = JsonSerializer.SerializeToDocument(new
                {
                    action = "ManagerOverrideFinancialClosure",
                    outstandingAmount = outstanding,
                    reason = req.ClosureReason ?? "موافقة مدير على الدين"
                })
            });
            await db.SaveChangesAsync();

            return Ok(new
            {
                canClose = true,
                reason = "موافقة مدير على الدين",
                reasonCode = "MANAGER_OVERRIDE",
                outstandingAmount = canViewFinanceAmounts ? outstanding : (decimal?)null,
                unbilledVisitsAmount = canViewFinanceAmounts ? unbilledVisitsAmount : (decimal?)null,
                requiresAuditLog = true
            });
        }

        // Cannot close without manager approval
        return Ok(new
        {
            canClose = false,
            reason = "يوجد مبلغ متبقي. يلزم دفع كامل أو موافقة مدير أو تحويل لخطة أقساط.",
            reasonCode = "OUTSTANDING_BALANCE",
            outstandingAmount = canViewFinanceAmounts ? outstanding : (decimal?)null,
            unbilledVisitsAmount = canViewFinanceAmounts ? unbilledVisitsAmount : (decimal?)null
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string GetInvoiceStatusArabic(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "مسودة",
        InvoiceStatus.Issued => "صادرة",
        InvoiceStatus.Paid => "مدفوعة",
        InvoiceStatus.Cancelled => "ملغاة",
        _ => status.ToString()
    };

    /// <summary>
    /// Generates the next invoice number for today (format: INV-yyyyMMdd-NNN).
    /// Local copy of <c>InvoicesController.GenerateInvoiceNumberAsync</c> so this
    /// service doesn't take a hard dependency on the API layer. CLIN-22.
    /// </summary>
    private static async Task<string> GenerateInvoiceNumberAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"INV-{datePart}-";

        var lastNumber = await db.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastNumber) && lastNumber.Length > prefix.Length)
        {
            var seqPart = lastNumber[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }

}
