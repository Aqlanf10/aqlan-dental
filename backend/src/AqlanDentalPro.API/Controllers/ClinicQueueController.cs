using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.DTOs.Journey;
using AqlanDentalPro.Application.DTOs.Sms;
using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Constants;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Clinic queue management — today's waiting list, patient calling, and TV display.
/// Queue Enhancements: Priority, NoShow, Recall, SMS notification, Analytics, Reorder.
/// </summary>
[ApiController]
[Route("api/clinic-queue")]
[Authorize(Policy = "StaffOnly")]
public class ClinicQueueController(
    AppDbContext db,
    IRealTimePushService pushService,
    ISmsService smsService,
    IWhatsAppService whatsAppService,
    IAuditService audit,
    ICurrentUserService currentUser,
    IClinicClock clinicClock,
    IJourneyBusinessDatePolicy businessDatePolicy,
    ILogger<ClinicQueueController> logger) : ControllerBase
{
    private static readonly HashSet<ClinicQueueStatus> ActiveStatuses =
    [
        ClinicQueueStatus.Waiting,
        ClinicQueueStatus.Called,
        ClinicQueueStatus.InRoom,
        ClinicQueueStatus.InProgress
    ];

    // CON-01 FIX: Arabic labels now come from ClinicQueueStatusTransitions.GetArabicLabel()
    // The local StatusArabic dictionary has been removed to ensure single source of truth.

    // ─── GET /api/clinic-queue/today ─────────────────────────────────────────
    /// <summary>Returns today's clinic queue items. Optional doctorId filter.
    /// Ordered by: Priority desc (Emergency first), then SortOrder, then CreatedAt.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayQueue([FromQuery] Guid? doctorId)
    {
        try
        {
            var today = ClinicTimeProvider.ClinicToday();

            var query = db.ClinicQueueItems
                .Include(q => q.Patient)
                .Include(q => q.Doctor)
                .Include(q => q.Appointment)
                .Where(q => q.QueueDate == today && q.IsActive);

            if (doctorId.HasValue)
                query = query.Where(q => q.DoctorId == doctorId.Value);

            var items = await query
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.SortOrder)
                .ThenBy(q => q.CreatedAt)
                .ToListAsync();

            // Calculate average service time for per-patient estimated wait
            var completedItems = await db.ClinicQueueItems
                .Where(q => q.QueueDate == today
                         && q.Status == ClinicQueueStatus.Completed
                         && q.StartedAt.HasValue
                         && q.CompletedAt.HasValue
                         && q.IsActive)
                .Select(q => new { q.StartedAt, q.CompletedAt })
                .ToListAsync();

            double avgServiceTime = 15; // Default fallback
            if (completedItems.Count > 0)
            {
                avgServiceTime = Math.Max(1, completedItems
                    .Where(q => q.CompletedAt!.Value > q.StartedAt!.Value)
                    .Select(q => (q.CompletedAt!.Value - q.StartedAt!.Value).TotalMinutes)
                    .DefaultIfEmpty(15)
                    .Average());
            }

            // Count waiting patients for position calculation
            var waitingItems = items
                .Where(i => i.Status == ClinicQueueStatus.Waiting)
                .OrderByDescending(i => i.Priority)
                .ThenBy(i => i.SortOrder)
                .ThenBy(i => i.CreatedAt)
                .ToList();

            var result = items.Select(q =>
            {
                // Calculate position for waiting patients
                int? position = null;
                int? estimatedWaitMinutes = null;
                if (q.Status == ClinicQueueStatus.Waiting)
                {
                    position = waitingItems.FindIndex(w => w.Id == q.Id) + 1;
                    if (position > 0)
                        estimatedWaitMinutes = (int)Math.Ceiling(position.Value * avgServiceTime);
                }

                return new
                {
                    q.Id,
                    q.PatientId,
                    PatientName = BuildPatientDisplayName(q.Patient),
                    PatientNumber = q.Patient != null ? q.Patient.PatientNumber : "",
                    q.AppointmentId,
                    AppointmentTime = q.Appointment != null ? q.Appointment.StartTime.ToString("HH:mm") : (string?)null,
                    q.VisitId,
                    DoctorName = q.Doctor != null ? q.Doctor.Name : "",
                    q.DoctorId,
                    q.RoomName,
                    Status = q.Status.ToString(),
                    StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(q.Status),
                    Priority = q.Priority.ToString(),
                    PriorityArabic = ClinicQueueStatusTransitions.GetPriorityArabicLabel(q.Priority),
                    q.SortOrder,
                    q.RecallCount,
                    q.CalledAt,
                    q.InRoomAt,
                    q.StartedAt,
                    q.CompletedAt,
                    q.NoShowAt,
                    q.Notes,
                    Position = position,
                    EstimatedWaitMinutes = estimatedWaitMinutes,
                    q.CreatedAt
                };
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetTodayQueue failed");
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل قائمة الانتظار" });
        }
    }

    // ─── POST /api/clinic-queue ──────────────────────────────────────────────
    /// <summary>Adds a patient to today's clinic queue.</summary>
    /// <remarks>H2 FIX: Uses DB advisory lock to prevent duplicate queue entries under concurrent requests.</remarks>
    [HttpPost]
    public async Task<IActionResult> AddToQueue([FromBody] AddToQueueRequest req)
    {
        var today = clinicClock.Today();

        // Validate patient exists
        var patient = await db.Patients.FindAsync(req.PatientId);
        if (patient is null)
            return NotFound(new { message = "المريض غير موجود" });
        if (!patient.IsActive)
            return BadRequest(new { message = "المريض محذوف" });

        // H2 FIX: Use advisory lock to prevent race condition on duplicate check
        // Lock key: patient ID hash — ensures only one request per patient at a time
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = StableLockKeyHelper.StableGuidToLong(req.PatientId);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // The request may wait across clinic midnight while acquiring the lock.
            today = clinicClock.Today();

            // Check for duplicate active queue item today (now safe under lock)
            var existingActive = await db.ClinicQueueItems
                .AnyAsync(q => q.PatientId == req.PatientId
                            && q.QueueDate == today
                            && ActiveStatuses.Contains(q.Status)
                            && q.IsActive);

            if (existingActive)
                return Conflict(new { message = "يوجد عنصر نشط لهذا المريض في قائمة الانتظار اليوم بالفعل" });

            // Validate appointment if provided, including W05 business-date guard.
            if (req.AppointmentId.HasValue)
            {
                var appointment = await db.Appointments.FindAsync(req.AppointmentId.Value);
                if (appointment is null)
                    return NotFound(new { message = "الموعد غير موجود" });
                if (!appointment.IsActive)
                    return BadRequest(new { message = "الموعد محذوف" });
                if (appointment.PatientId != req.PatientId)
                    return BadRequest(new { message = "الموعد لا ينتمي لهذا المريض" });

                var dateError = ValidateJourneyBusinessDate(appointment, req, out var dateDecision);
                if (dateError != null) return dateError;
                today = dateDecision.BusinessDate;
                AddFutureAppointmentOverrideAudit(appointment, "LegacyAddToQueue", dateDecision);
            }

            // Validate doctor (now required)
            if (req.DoctorId == Guid.Empty)
                return BadRequest(new { message = "معرف الطبيب مطلوب" });

            var doctorExists = await db.Doctors.AnyAsync(d => d.Id == req.DoctorId && d.IsActive);
            if (!doctorExists)
                return NotFound(new { message = "الطبيب غير موجود" });

            // Validate room name if provided
            if (!string.IsNullOrWhiteSpace(req.RoomName))
            {
                var isValidRoom = await IsRoomValidAsync(req.RoomName);
                if (!isValidRoom)
                    return BadRequest(new { message = "اسم الغرفة غير صالح" });
            }

            // Look for existing visit to link
            Guid? visitId = req.VisitId;
            if (visitId == null && req.AppointmentId.HasValue)
            {
                var existingVisit = await db.Visits
                    .FirstOrDefaultAsync(v => v.AppointmentId == req.AppointmentId.Value && v.IsActive);
                visitId = existingVisit?.Id;
            }

            var item = new ClinicQueueItem
            {
                PatientId = req.PatientId,
                AppointmentId = req.AppointmentId,
                VisitId = visitId,
                DoctorId = req.DoctorId,
                ServiceId = req.ServiceId,
                ClinicRoomId = req.ClinicRoomId,
                RoomName = req.RoomName,
                Status = ClinicQueueStatus.Waiting,
                QueueDate = today,
                AddedByUserId = GetCurrentUserId(),
                Notes = req.Notes
            };

            db.ClinicQueueItems.Add(item);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // SignalR: Branch-scoped push — only clients in the same branch see queue updates
            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "added", item.Id, item.PatientId });
            else
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "added", item.Id, item.PatientId });

            return Created($"/api/clinic-queue/{item.Id}", new
            {
                item.Id,
                item.PatientId,
                item.AppointmentId,
                item.VisitId,
                item.DoctorId,
                item.RoomName,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.QueueDate,
                item.Notes,
                message = "تمت إضافة المريض إلى قائمة الانتظار بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/call ────────────────────────────────────
    /// <summary>Calls a patient and optionally assigns a room.</summary>
    [HttpPost("{id:guid}/call")]
    public async Task<IActionResult> CallPatient(Guid id, [FromBody] CallQueuePatientRequest? req = null)
    {
        // CON-03 FIX: Read inside transaction + advisory lock to prevent race condition
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // CON-03 FIX: Acquire advisory lock on queue item to prevent double-processing
            var lockKey = StableLockKeyHelper.StableGuidToLong(id);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems.FindAsync(id);
            if (item is null)
                return NotFound(new { message = "عنصر الانتظار غير موجود" });
            if (!item.IsActive)
                return BadRequest(new { message = "عنصر الانتظار محذوف" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.Called);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            // Validate and assign room if provided
            var roomName = req?.RoomName ?? item.RoomName;
            // Doctor room assignment ("تعيينات غرف الأطباء"): when neither the request nor
            // the queue item carries a room, fall back to the doctor's standing room so
            // reception doesn't re-pick the same room on every call. Explicit choices
            // above always win; the fetched name is valid by construction (comes from
            // ClinicRooms) and still passes IsRoomValidAsync below.
            if (string.IsNullOrWhiteSpace(roomName))
                roomName = await Services.DoctorRoomResolver.ResolveDefaultRoomNameAsync(db, item.DoctorId);
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                var isValidRoom = await IsRoomValidAsync(roomName);
                if (!isValidRoom)
                    return BadRequest(new { message = "اسم الغرفة غير صالح" });
            }

            item.Status = ClinicQueueStatus.Called;
            item.CalledAt = DateTime.UtcNow;
            item.CalledByUserId = GetCurrentUserId();
            item.RoomName = roomName;
            item.UpdatedAt = DateTime.UtcNow;

            // Also update the linked appointment if present
            await SyncAppointmentStatus(item, AppointmentStatus.Called);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // SignalR: Branch-scoped push — notify TV display and all staff in the same branch
            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
            {
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.PatientCalled, new { item.Id, item.PatientId, item.RoomName, item.CalledAt });
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "called", item.Id, item.PatientId });
            }
            else
            {
                await pushService.PushToAllAsync(MessagingHubEvents.PatientCalled, new { item.Id, item.PatientId, item.RoomName, item.CalledAt });
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "called", item.Id, item.PatientId });
            }

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.RoomName,
                item.CalledAt,
                message = "تم نداء المريض بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/enter-room ──────────────────────────────
    /// <summary>Marks patient as entered the room.</summary>
    [HttpPost("{id:guid}/enter-room")]
    public async Task<IActionResult> EnterRoom(Guid id)
    {
        // CON-03 FIX: Add advisory lock for concurrency protection against double-processing
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // CON-03 FIX: Acquire advisory lock on queue item to prevent concurrent EnterRoom/Complete
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems.FindAsync(id);
            if (item is null)
                return NotFound(new { message = "عنصر الانتظار غير موجود" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.InRoom);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            item.Status = ClinicQueueStatus.InRoom;
            item.InRoomAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            await SyncAppointmentStatus(item, AppointmentStatus.InRoom);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // SignalR: Branch-scoped push — notify queue update when patient enters room
            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "entered-room", item.Id, item.PatientId });
            else
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "entered-room", item.Id, item.PatientId });

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.RoomName,
                item.InRoomAt,
                message = "تم تسجيل دخول المريض إلى الغرفة بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/start ───────────────────────────────────
    /// <summary>Marks the visit as in progress. Creates a Visit if not linked.</summary>
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartVisit(Guid id, [FromBody] StartVisitRequest? req = null)
    {
        // CON-03 FIX: Add advisory lock for concurrency protection
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            // CON-03 FIX: Acquire stable advisory lock on queue item
            if (tx != null)
            {
                var lockKey = StableLockKeyHelper.StableGuidToLong(id);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            var item = await db.ClinicQueueItems
                .Include(q => q.Appointment)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (item is null)
                return NotFound(new { message = "عنصر الانتظار غير موجود" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.InProgress);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            // CORE-F-001 FIX: this route was only taking a lock keyed on the queue item's
            // own Id, while AppointmentsController.StartVisit and CheckoutService.StartVisitAsync
            // (used by PatientJourneyController) both lock on the Appointment's Id. Two
            // concurrent requests hitting different routes for the same appointment could
            // therefore acquire two different advisory locks, both pass the "existing visit"
            // check below, and create two Visit rows for the same AppointmentId. Take the
            // same appointment-scoped lock here too so all three routes serialize against
            // each other for a given appointment. Locks are additive (pg_advisory_xact_lock
            // does not release until the transaction ends), so holding both the queue-item
            // lock and the appointment lock in the same transaction is safe.
            if (tx != null && item.AppointmentId.HasValue)
            {
                var appointmentLockKey = StableLockKeyHelper.StableGuidToLong(item.AppointmentId.Value);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", appointmentLockKey);
            }

            var businessDate = clinicClock.Today();
            if (item.Appointment != null)
            {
                await db.Entry(item.Appointment).ReloadAsync();
                var dateError = ValidateJourneyBusinessDate(item.Appointment, req, out var dateDecision);
                if (dateError != null)
                {
                    if (tx != null) await tx.RollbackAsync();
                    return dateError;
                }
                businessDate = dateDecision.BusinessDate;
                AddFutureAppointmentOverrideAudit(item.Appointment, "LegacyQueueStartVisit", dateDecision);
            }

            // Create a Visit if not already linked
            if (item.VisitId == null)
            {
                // FIX: Check for existing visit for this appointment to prevent duplicates.
                // A visit may have been created through AppointmentsController.StartVisit
                // or PatientJourneyController.StartVisit without updating the queue item's VisitId.
                if (item.AppointmentId.HasValue)
                {
                    var existingVisit = await db.Visits
                        .FirstOrDefaultAsync(v => v.AppointmentId == item.AppointmentId.Value && v.IsActive);
                    if (existingVisit != null)
                    {
                        item.VisitId = existingVisit.Id;
                    }
                }

                if (item.VisitId == null)
                {
                    var visit = new Visit
                    {
                        PatientId = item.PatientId,
                        AppointmentId = item.AppointmentId,
                        // QA5-02: clinic-local date, NOT the UTC server date. Yemen is UTC+3,
                        // so visits started after 21:00 UTC were stamped with the previous
                        // day and disagreed with the appointment's AppointmentDate
                        // (confirmed live: visit 2026-07-05 vs appointment 2026-07-06).
                        // Prefer the linked appointment's own clinic date — matches
                        // AppointmentsController.StartVisit semantics.
                        VisitDate = businessDate,
                        DoctorId = item.DoctorId ?? item.Appointment?.DoctorId,
                        Specialty = item.Appointment?.Specialty,
                        ServiceId = item.ServiceId ?? item.Appointment?.ServiceId
                    };

                    db.Visits.Add(visit);
                    try
                    {
                        await db.SaveChangesAsync(); // Save to get the ID
                    }
                    catch (DbUpdateException ex)
                    {
                        logger.LogError(ex, "Failed to create visit for queue item {QueueItemId}", id);
                        throw;
                    }
                    item.VisitId = visit.Id;
                }
            }

            var eventUtc = clinicClock.UtcNow();
            item.Status = ClinicQueueStatus.InProgress;
            item.StartedAt = eventUtc;
            item.UpdatedAt = eventUtc;

            await SyncAppointmentStatus(item, AppointmentStatus.InProgress);

            await db.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();

            // SignalR: Branch-scoped push — notify queue update when visit starts
            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "started", item.Id, item.PatientId });
            else
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "started", item.Id, item.PatientId });

            return Ok(new
            {
                item.Id,
                item.VisitId,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.StartedAt,
                message = "تم بدء المعالجة بنجاح"
            });
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/complete ────────────────────────────────
    /// <summary>Marks the queue item as completed.</summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        // CON-03 FIX: Add advisory lock for concurrency protection against double-processing
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // CON-03 FIX: Acquire advisory lock on queue item to prevent concurrent Complete
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems.FindAsync(id);
            if (item is null)
                return NotFound(new { message = "عنصر الانتظار غير موجود" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.Completed);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            item.Status = ClinicQueueStatus.Completed;
            item.CompletedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            await SyncAppointmentStatus(item, AppointmentStatus.Completed);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // SignalR: Branch-scoped push — notify queue update on completion
            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "completed", item.Id, item.PatientId });
            else
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "completed", item.Id, item.PatientId });

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.CompletedAt,
                message = "تم إكمال عنصر الانتظار بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/cancel ──────────────────────────────────
    /// <summary>Cancels a queue item.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        // CON-03 FIX: Add transaction + advisory lock — Cancel was the only state-changing
        // endpoint without a transaction, making it vulnerable to race conditions.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // CON-03 FIX: Acquire advisory lock on queue item
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems.FindAsync(id);
            if (item is null)
                return NotFound(new { message = "عنصر الانتظار غير موجود" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.Cancelled);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            item.Status = ClinicQueueStatus.Cancelled;
            item.CancelledAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            // F1 FIX: Sync appointment status when cancelling queue item.
            // Previously, Cancel was the only state-changing endpoint that didn't sync,
            // causing appointments to remain in Waiting/Called/InRoom while the queue
            // item was Cancelled — a critical data consistency gap.
            await SyncAppointmentStatus(item, AppointmentStatus.Cancelled);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // SignalR: Branch-scoped push — notify queue update on cancellation
            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "cancelled", item.Id, item.PatientId });
            else
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "cancelled", item.Id, item.PatientId });

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.CancelledAt,
                message = "تم إلغاء عنصر الانتظار بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/no-show ─────────────────────────────────
    /// <summary>Marks a patient as NoShow — called but didn't respond.</summary>
    [HttpPost("{id:guid}/no-show")]
    public async Task<IActionResult> MarkNoShow(Guid id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems.FindAsync(id);
            if (item is null)
                return NotFound(new { message = "عنصر الانتظار غير موجود" });
            if (!item.IsActive)
                return BadRequest(new { message = "عنصر الانتظار محذوف" });

            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.NoShow);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            item.Status = ClinicQueueStatus.NoShow;
            item.NoShowAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            // Sync appointment to NoShow if linked
            if (item.AppointmentId.HasValue)
            {
                var appointment = await db.Appointments.FindAsync(item.AppointmentId.Value);
                if (appointment != null && appointment.IsActive)
                {
                    if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.NoShow))
                    {
                        appointment.Status = AppointmentStatus.NoShow;
                        appointment.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "no-show", item.Id, item.PatientId });
            else
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "no-show", item.Id, item.PatientId });

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.NoShowAt,
                item.RecallCount,
                message = "تم تسجيل عدم حضور المريض"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/recall ──────────────────────────────────
    /// <summary>Re-calls a patient. Increments RecallCount and updates CalledAt.</summary>
    [HttpPost("{id:guid}/recall")]
    public async Task<IActionResult> RecallPatient(Guid id, [FromBody] CallQueuePatientRequest? req = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems.FindAsync(id);
            if (item is null)
                return NotFound(new { message = "عنصر الانتظار غير موجود" });
            if (!item.IsActive)
                return BadRequest(new { message = "عنصر الانتظار محذوف" });

            // Can only recall patients in Called status (already called but not yet in room)
            if (item.Status != ClinicQueueStatus.Called && item.Status != ClinicQueueStatus.Waiting)
                return BadRequest(new { message = "لا يمكن إعادة نداء مريض بحالة " + ClinicQueueStatusTransitions.GetArabicLabel(item.Status) });

            // Validate and assign room if provided
            var roomName = req?.RoomName ?? item.RoomName;
            // Doctor room assignment ("تعيينات غرف الأطباء"): when neither the request nor
            // the queue item carries a room, fall back to the doctor's standing room so
            // reception doesn't re-pick the same room on every call. Explicit choices
            // above always win; the fetched name is valid by construction (comes from
            // ClinicRooms) and still passes IsRoomValidAsync below.
            if (string.IsNullOrWhiteSpace(roomName))
                roomName = await Services.DoctorRoomResolver.ResolveDefaultRoomNameAsync(db, item.DoctorId);
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                var isValidRoom = await IsRoomValidAsync(roomName);
                if (!isValidRoom)
                    return BadRequest(new { message = "اسم الغرفة غير صالح" });
            }

            item.Status = ClinicQueueStatus.Called;
            item.CalledAt = DateTime.UtcNow;
            item.CalledByUserId = GetCurrentUserId();
            item.RecallCount++;
            item.RoomName = roomName;
            item.UpdatedAt = DateTime.UtcNow;

            await SyncAppointmentStatus(item, AppointmentStatus.Called);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
            {
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.PatientCalled, new { item.Id, item.PatientId, item.RoomName, item.CalledAt, item.RecallCount });
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "recalled", item.Id, item.PatientId, item.RecallCount });
            }
            else
            {
                await pushService.PushToAllAsync(MessagingHubEvents.PatientCalled, new { item.Id, item.PatientId, item.RoomName, item.CalledAt, item.RecallCount });
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "recalled", item.Id, item.PatientId, item.RecallCount });
            }

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.RoomName,
                item.CalledAt,
                item.RecallCount,
                message = item.RecallCount >= 3
                    ? $"تم إعادة النداء ({item.RecallCount} مرات) — يُنصح بتسجيل عدم الحضور"
                    : $"تم إعادة النداء بنجاح (المرة {item.RecallCount})"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── PATCH /api/clinic-queue/{id}/priority ────────────────────────────────
    /// <summary>Changes the priority level of a queue item.</summary>
    [HttpPatch("{id:guid}/priority")]
    public async Task<IActionResult> ChangePriority(Guid id, [FromBody] ChangePriorityRequest req)
    {
        var item = await db.ClinicQueueItems.FindAsync(id);
        if (item is null)
            return NotFound(new { message = "عنصر الانتظار غير موجود" });

        // Can only change priority for active items
        if (!ActiveStatuses.Contains(item.Status))
            return BadRequest(new { message = "لا يمكن تغيير الأولوية لعنصر غير نشط" });

        // If priority is set to Emergency (حالة إسعافية), audit reason is required
        if (req.Priority == ClinicQueuePriority.Emergency)
        {
            if (string.IsNullOrWhiteSpace(req.Reason))
            {
                return BadRequest(new { message = "الرجاء إدخال سبب الحالة الإسعافية لتوثيق السجل" });
            }

            // Log formal audit record
            await audit.LogAsync(
                AuditAction.Update,
                "ClinicQueueItem",
                item.Id,
                oldData: new { Priority = item.Priority.ToString() },
                newData: new { Priority = req.Priority.ToString(), Reason = req.Reason },
                details: $"تم تغيير أولوية المريض إلى حالة إسعافية. السبب: {req.Reason}"
            );
        }

        item.Priority = req.Priority;
        item.UpdatedAt = DateTime.UtcNow;
        if (req.Priority == ClinicQueuePriority.Emergency && !string.IsNullOrWhiteSpace(req.Reason))
        {
            item.Notes = string.IsNullOrWhiteSpace(item.Notes) 
                ? $"[حالة إسعافية: {req.Reason}]" 
                : $"{item.Notes} | [حالة إسعافية: {req.Reason}]";
        }
        await db.SaveChangesAsync();

        var branchId = GetCurrentBranchId();
        if (branchId.HasValue)
            await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "priority-changed", item.Id, item.PatientId, item.Priority });
        else
            await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "priority-changed", item.Id, item.PatientId, item.Priority });

        return Ok(new
        {
            item.Id,
            Priority = item.Priority.ToString(),
            PriorityArabic = ClinicQueueStatusTransitions.GetPriorityArabicLabel(item.Priority),
            message = $"تم تغيير الأولوية إلى {ClinicQueueStatusTransitions.GetPriorityArabicLabel(item.Priority)}"
        });
    }

    // ─── POST /api/clinic-queue/reorder ───────────────────────────────────────
    /// <summary>Reorders queue items via drag-and-drop. Accepts array of {id, sortOrder}.</summary>
    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ReorderItemRequest> items)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { message = "قائمة الترتيب فارغة" });

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var today = ClinicTimeProvider.ClinicToday();

            foreach (var req in items)
            {
                var item = await db.ClinicQueueItems.FindAsync(req.Id);
                if (item == null || !item.IsActive || item.QueueDate != today) continue;
                if (!ActiveStatuses.Contains(item.Status)) continue;

                item.SortOrder = req.SortOrder;
                item.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var branchId = GetCurrentBranchId();
            if (branchId.HasValue)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.QueueUpdated, new { action = "reordered" });
            else
                await pushService.PushToAllAsync(MessagingHubEvents.QueueUpdated, new { action = "reordered" });

            return Ok(new { message = "تم إعادة ترتيب قائمة الانتظار بنجاح" });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── POST /api/clinic-queue/{id}/notify ──────────────────────────────────
    /// <summary>Sends SMS/WhatsApp notification to a patient when called.</summary>
    [HttpPost("{id:guid}/notify")]
    public async Task<IActionResult> NotifyPatient(Guid id, [FromBody] NotifyPatientRequest? req = null)
    {
        var item = await db.ClinicQueueItems
            .Include(q => q.Patient)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (item is null)
            return NotFound(new { message = "عنصر الانتظار غير موجود" });

        if (item.Patient is null)
            return BadRequest(new { message = "لا توجد بيانات مريض مرتبطة" });

        var patient = item.Patient;
        var channel = req?.Channel ?? "sms"; // sms or whatsapp
        var roomDisplay = !string.IsNullOrWhiteSpace(item.RoomName) ? item.RoomName : "الاستقبال";
        var message = $"مرحباً {BuildPatientDisplayName(patient)}، يرجى التوجه إلى {roomDisplay} — دورك الآن في العيادة.";

        var results = new List<object>();

        if (channel == "sms" || channel == "both")
        {
            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                try
                {
                    await smsService.SendSmsAsync(new SendSmsRequest
                    {
                        PatientId = patient.Id,
                        TemplateType = "custom",
                        CustomMessage = message
                    });
                    results.Add(new { channel = "sms", status = "sent", phone = patient.Phone });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send SMS to patient {PatientId}", patient.Id);
                    results.Add(new { channel = "sms", status = "failed", error = "تعذّر إرسال الرسالة النصية" });
                }
            }
            else
            {
                results.Add(new { channel = "sms", status = "skipped", reason = "لا يوجد رقم هاتف" });
            }
        }

        if (channel == "whatsapp" || channel == "both")
        {
            // CORE-PAT-016: WhatsAppService actually dials patient.WhatsApp ??
            // patient.Phone — gating on Phone alone skipped patients who have
            // only a WhatsApp number, and reported the wrong number back to
            // reception whenever the two differ.
            var whatsAppNumber = !string.IsNullOrWhiteSpace(patient.WhatsApp) ? patient.WhatsApp : patient.Phone;
            if (!string.IsNullOrWhiteSpace(whatsAppNumber))
            {
                try
                {
                    await whatsAppService.SendMessageAsync(new SendMessageRequest
                    {
                        PatientId = patient.Id,
                        TemplateType = "custom",
                        CustomMessage = message
                    });
                    results.Add(new { channel = "whatsapp", status = "sent", phone = whatsAppNumber });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send WhatsApp to patient {PatientId}", patient.Id);
                    results.Add(new { channel = "whatsapp", status = "failed", error = "تعذّر إرسال رسالة واتساب" });
                }
            }
            else
            {
                results.Add(new { channel = "whatsapp", status = "skipped", reason = "لا يوجد رقم هاتف" });
            }
        }

        return Ok(new { item.Id, notifications = results, message = "تم إرسال الإشعارات" });
    }

    // ─── GET /api/clinic-queue/analytics ─────────────────────────────────────
    /// <summary>Returns queue analytics for today or a date range.</summary>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var from = DateOnly.FromDateTime(fromDate ?? DateTime.UtcNow);
        var to = DateOnly.FromDateTime(toDate ?? DateTime.UtcNow);

        var queueItems = await db.ClinicQueueItems
            .Include(q => q.Doctor)
            .Where(q => q.QueueDate >= from && q.QueueDate <= to && q.IsActive)
            .ToListAsync();

        // Overall stats
        var totalItems = queueItems.Count;
        var completedItems = queueItems.Where(q => q.Status == ClinicQueueStatus.Completed).ToList();
        var noShowItems = queueItems.Where(q => q.Status == ClinicQueueStatus.NoShow).ToList();
        var cancelledItems = queueItems.Where(q => q.Status == ClinicQueueStatus.Cancelled).ToList();

        var completionRate = totalItems > 0 ? Math.Round((double)completedItems.Count / totalItems * 100, 1) : 0;
        var noShowRate = totalItems > 0 ? Math.Round((double)noShowItems.Count / totalItems * 100, 1) : 0;

        // Average service time (StartedAt → CompletedAt)
        var serviceTimes = completedItems
            .Where(q => q.StartedAt.HasValue && q.CompletedAt.HasValue && q.CompletedAt > q.StartedAt)
            .Select(q => (q.CompletedAt!.Value - q.StartedAt!.Value).TotalMinutes)
            .ToList();
        var avgServiceTime = serviceTimes.Count > 0 ? Math.Round(serviceTimes.Average(), 1) : 0;

        // Average wait time (CreatedAt → CalledAt) for called/completed items
        var waitTimes = queueItems
            .Where(q => q.CalledAt.HasValue && q.CalledAt.Value > q.CreatedAt)
            .Select(q => (q.CalledAt!.Value - q.CreatedAt).TotalMinutes)
            .ToList();
        var avgWaitTime = waitTimes.Count > 0 ? Math.Round(waitTimes.Average(), 1) : 0;

        // Time-in-status metrics
        var calledToInRoom = queueItems
            .Where(q => q.CalledAt.HasValue && q.InRoomAt.HasValue && q.InRoomAt > q.CalledAt)
            .Select(q => (q.InRoomAt!.Value - q.CalledAt!.Value).TotalMinutes)
            .ToList();
        var avgCalledToInRoom = calledToInRoom.Count > 0 ? Math.Round(calledToInRoom.Average(), 1) : 0;

        var inRoomToStart = queueItems
            .Where(q => q.InRoomAt.HasValue && q.StartedAt.HasValue && q.StartedAt > q.InRoomAt)
            .Select(q => (q.StartedAt!.Value - q.InRoomAt!.Value).TotalMinutes)
            .ToList();
        var avgInRoomToStart = inRoomToStart.Count > 0 ? Math.Round(inRoomToStart.Average(), 1) : 0;

        // Per-doctor stats
        var doctorStats = queueItems
            .Where(q => q.Doctor != null)
            .GroupBy(q => q.Doctor!.Name)
            .Select(g => new
            {
                DoctorName = g.Key,
                TotalPatients = g.Count(),
                Completed = g.Count(q => q.Status == ClinicQueueStatus.Completed),
                NoShow = g.Count(q => q.Status == ClinicQueueStatus.NoShow),
                AvgServiceTime = g.Where(q => q.Status == ClinicQueueStatus.Completed && q.StartedAt.HasValue && q.CompletedAt.HasValue && q.CompletedAt > q.StartedAt)
                    .Select(q => (q.CompletedAt!.Value - q.StartedAt!.Value).TotalMinutes)
                    .DefaultIfEmpty(0)
                    .Average() is double v ? Math.Round(v, 1) : 0
            })
            .OrderByDescending(d => d.TotalPatients)
            .ToList();

        // Peak hours analysis
        var hourlyDistribution = queueItems
            .Where(q => q.Status != ClinicQueueStatus.Cancelled)
            .GroupBy(q => q.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderBy(h => h.Hour)
            .ToList();

        // Priority distribution
        var priorityStats = queueItems
            .GroupBy(q => q.Priority)
            .Select(g => new
            {
                Priority = g.Key.ToString(),
                PriorityArabic = ClinicQueueStatusTransitions.GetPriorityArabicLabel(g.Key),
                Count = g.Count()
            })
            .OrderByDescending(p => p.Count)
            .ToList();

        return Ok(new
        {
            DateRange = new { From = from.ToString("yyyy-MM-dd"), To = to.ToString("yyyy-MM-dd") },
            TotalItems = totalItems,
            Completed = completedItems.Count,
            NoShow = noShowItems.Count,
            Cancelled = cancelledItems.Count,
            CompletionRate = completionRate,
            NoShowRate = noShowRate,
            AvgServiceTimeMinutes = avgServiceTime,
            AvgWaitTimeMinutes = avgWaitTime,
            AvgCalledToInRoomMinutes = avgCalledToInRoom,
            AvgInRoomToStartMinutes = avgInRoomToStart,
            DoctorStats = doctorStats,
            HourlyDistribution = hourlyDistribution,
            PriorityDistribution = priorityStats
        });
    }

    // ─── PATCH /api/clinic-queue/{id}/room ───────────────────────────────────
    /// <summary>Changes the room assignment for a queue item.</summary>
    [HttpPatch("{id:guid}/room")]
    public async Task<IActionResult> ChangeRoom(Guid id, [FromBody] ChangeRoomRequest req)
    {
        var item = await db.ClinicQueueItems.FindAsync(id);
        if (item is null)
            return NotFound(new { message = "عنصر الانتظار غير موجود" });

        if (!await IsRoomValidAsync(req.RoomName))
            return BadRequest(new { message = "اسم الغرفة غير صالح" });

        // Can only change room for active items
        if (!ActiveStatuses.Contains(item.Status))
            return BadRequest(new { message = "لا يمكن تغيير الغرفة لعنصر غير نشط" });

        // M2 FIX: Wrap multi-entity updates in a transaction
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            item.RoomName = req.RoomName;
            item.UpdatedAt = DateTime.UtcNow;

            // Also update linked appointment room
            if (item.AppointmentId.HasValue)
            {
                var appointment = await db.Appointments.FindAsync(item.AppointmentId.Value);
                if (appointment != null)
                {
                    appointment.RoomName = req.RoomName;
                    appointment.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return Ok(new
        {
            item.Id,
            item.RoomName,
            message = "تم تغيير الغرفة بنجاح"
        });
    }

    // ─── GET /api/clinic-queue/display ───────────────────────────────────────
    /// <summary>
    /// Returns data for the TV queue display screen. Anonymous access (no auth).
    /// Privacy-safe: only exposes patient display name, file number, room,
    /// doctor name, queue status, and called time.
    /// NEVER expose: phone, diagnosis, payment/balance, medical history,
    /// private notes, or full sensitive patient profile.
    /// </summary>
    [HttpGet("display")]
    [AllowAnonymous] // TV display should work without staff login
    public async Task<IActionResult> GetDisplay()
    {
        try
        {
            var today = ClinicTimeProvider.ClinicToday();

            var items = await db.ClinicQueueItems
                .Include(q => q.Patient)
                .Include(q => q.Doctor)
                .Where(q => q.QueueDate == today && q.IsActive
                    && (q.Status == ClinicQueueStatus.Waiting
                        || q.Status == ClinicQueueStatus.Called
                        || q.Status == ClinicQueueStatus.InRoom
                        || q.Status == ClinicQueueStatus.InProgress))
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.SortOrder)
                .ThenBy(q => q.CreatedAt)
                .ToListAsync();

            // Calculate avg service time for estimated wait
            double avgServiceTime = 15;
            try
            {
                var completedToday = await db.ClinicQueueItems
                    .Where(q => q.QueueDate == today && q.Status == ClinicQueueStatus.Completed
                             && q.StartedAt.HasValue && q.CompletedAt.HasValue && q.IsActive)
                    .Select(q => new { q.StartedAt, q.CompletedAt })
                    .ToListAsync();

                if (completedToday.Count > 0)
                {
                    avgServiceTime = Math.Max(1, completedToday
                        .Where(q => q.CompletedAt!.Value > q.StartedAt!.Value)
                        .Select(q => (q.CompletedAt!.Value - q.StartedAt!.Value).TotalMinutes)
                        .DefaultIfEmpty(15).Average());
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to calculate avg service time for display endpoint, using default");
                avgServiceTime = 15;
            }

            // Latest called patient (only Called status — for voice announcement trigger)
            var latestCalled = items
                .Where(q => q.Status == ClinicQueueStatus.Called && q.CalledAt != null)
                .OrderByDescending(q => q.CalledAt)
                .Select(q => new
                {
                    QueueItemId = q.Id,
                    PatientNumber = q.Patient?.PatientNumber ?? "",
                    PatientName = BuildPublicPatientDisplayName(q.Patient),
                    DoctorName = q.Doctor?.Name ?? "",
                    q.RoomName,
                    q.CalledAt,
                    q.RecallCount,
                    Priority = q.Priority.ToString(),
                    PriorityArabic = ClinicQueueStatusTransitions.GetPriorityArabicLabel(q.Priority)
                })
                .FirstOrDefault();

            // Waiting list with priority + position + estimated wait
            var waitingItems = items
                .Where(q => q.Status == ClinicQueueStatus.Waiting)
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.SortOrder)
                .ThenBy(q => q.CreatedAt)
                .ToList();

            var waitingList = waitingItems.Select((q, idx) => new
            {
                QueueItemId = q.Id,
                PatientNumber = q.Patient?.PatientNumber ?? "",
                PatientName = BuildPublicPatientDisplayName(q.Patient),
                DoctorName = q.Doctor?.Name ?? "",
                Status = "في الانتظار",
                Position = idx + 1,
                EstimatedWaitMinutes = (int)Math.Ceiling((idx + 1) * avgServiceTime),
                Priority = q.Priority.ToString(),
                PriorityArabic = ClinicQueueStatusTransitions.GetPriorityArabicLabel(q.Priority)
            }).ToList();

            // Recently called (Called + InRoom, most recent first)
            var recentlyCalled = items
                .Where(q => (q.Status == ClinicQueueStatus.Called || q.Status == ClinicQueueStatus.InRoom) && q.CalledAt != null)
                .OrderByDescending(q => q.CalledAt)
                .Take(5)
                .Select(q => new
                {
                    QueueItemId = q.Id,
                    PatientNumber = q.Patient?.PatientNumber ?? "",
                    PatientName = BuildPublicPatientDisplayName(q.Patient),
                    DoctorName = q.Doctor?.Name ?? "",
                    q.RoomName,
                    StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(q.Status),
                    Status = q.Status.ToString(),
                    q.CalledAt,
                    q.RecallCount
                })
                .ToList();

            // Now Serving — room-by-room view: which doctor is in which room with which patient
            // Filter out items with null Doctor/Patient before grouping to prevent .First() crashes
            var nowServing = items
                .Where(q => q.Status == ClinicQueueStatus.InProgress
                         && !string.IsNullOrWhiteSpace(q.RoomName)
                         && q.Doctor != null
                         && q.Patient != null)
                .GroupBy(q => q.RoomName!)
                .Select(g => new
                {
                    RoomName = g.Key,
                    DoctorName = g.First().Doctor?.Name ?? "",
                    PatientName = BuildPublicPatientDisplayName(g.First().Patient),
                    PatientNumber = g.First().Patient?.PatientNumber ?? "",
                    StartedAt = g.First().StartedAt
                })
                .OrderBy(r => r.RoomName)
                .ToList();

            return Ok(new
            {
                LatestCalled = latestCalled,
                WaitingCount = waitingList.Count,
                WaitingList = waitingList,
                RecentlyCalled = recentlyCalled,
                NowServing = nowServing,
                AverageServiceTimeMinutes = Math.Round(avgServiceTime, 1)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error in GetDisplay endpoint — returning safe empty data");
            return Ok(new
            {
                LatestCalled = (object?)null,
                WaitingCount = 0,
                WaitingList = Array.Empty<object>(),
                RecentlyCalled = Array.Empty<object>(),
                NowServing = Array.Empty<object>(),
                AverageServiceTimeMinutes = 15.0
            });
        }
    }

    // ─── GET /api/clinic-queue/rooms ─────────────────────────────────────────
    /// <summary>Returns available room names for the room selector. Queries from DB.</summary>
    [HttpGet("rooms")]
    [AllowAnonymous]
    [Obsolete("Use GetRoomsFromDb instead. This hardcoded endpoint returns static defaults.")]
    public async Task<IActionResult> GetRooms()
    {
        var rooms = await db.ClinicRooms
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.ArabicName)
            .Select(r => new { r.Id, r.ArabicName, r.EnglishName, r.Code, RoomType = r.RoomType.ToString() })
            .ToListAsync();

        if (rooms.Count == 0)
        {
            // Fallback to constants for backward compatibility
            return Ok(ClinicRoomNames.DefaultRooms.Select((name, i) => new
            {
                Id = Guid.Empty,
                ArabicName = name,
                EnglishName = (string?)null,
                Code = $"room-{i + 1}",
                RoomType = "Treatment"
            }));
        }

        return Ok(rooms);
    }

    // ─── GET /api/clinic-queue/estimated-wait ───────────────────────────────
    /// <summary>
    /// Returns estimated wait time for the queue.
    /// Calculated based on average service time of completed items today.
    /// </summary>
    [HttpGet("estimated-wait")]
    public async Task<IActionResult> GetEstimatedWait()
    {
        var today = ClinicTimeProvider.ClinicToday();

        // Calculate average service time from completed items today
        var completedItems = await db.ClinicQueueItems
            .Where(q => q.QueueDate == today
                     && q.Status == ClinicQueueStatus.Completed
                     && q.StartedAt.HasValue
                     && q.CompletedAt.HasValue
                     && q.IsActive)
            .Select(q => new { q.StartedAt, q.CompletedAt })
            .ToListAsync();

        double averageServiceTimeMinutes = 15; // Default fallback
        if (completedItems.Count > 0)
        {
            var avgTicks = completedItems
                .Where(q => q.CompletedAt!.Value > q.StartedAt!.Value)
                .Select(q => (q.CompletedAt!.Value - q.StartedAt!.Value).TotalMinutes)
                .DefaultIfEmpty(15)
                .Average();
            averageServiceTimeMinutes = Math.Max(1, avgTicks);
        }

        // Count waiting + called (not yet InRoom)
        var waitingCount = await db.ClinicQueueItems
            .CountAsync(q => q.QueueDate == today
                          && q.Status == ClinicQueueStatus.Waiting
                          && q.IsActive);

        var calledNotInRoomCount = await db.ClinicQueueItems
            .CountAsync(q => q.QueueDate == today
                          && q.Status == ClinicQueueStatus.Called
                          && q.IsActive);

        var itemsAhead = waitingCount + calledNotInRoomCount;
        var currentWaitMinutes = (int)Math.Ceiling(itemsAhead * averageServiceTimeMinutes);

        return Ok(new
        {
            averageServiceTimeMinutes = Math.Round(averageServiceTimeMinutes, 1),
            currentWaitMinutes,
            waitingCount = itemsAhead
        });
    }

    // ─── GET /api/clinic-queue/rooms/db ──────────────────────────────────────
    /// <summary>Returns active rooms from the ClinicRooms table (database-driven).
    /// Falls back to ClinicRoomNames constants if no rooms exist yet.
    /// Staff-only — room data includes internal IDs and codes not intended for public access.
    /// Public TV display uses GET /api/clinic-queue/rooms (hardcoded) instead.</summary>
    [HttpGet("rooms/db")]
    public async Task<IActionResult> GetRoomsFromDb()
    {
        var rooms = await db.ClinicRooms
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.ArabicName)
            .Select(r => new { r.Id, r.ArabicName, r.EnglishName, r.Code, RoomType = r.RoomType.ToString() })
            .ToListAsync();

        if (rooms.Count == 0)
        {
            // Fallback to constants for backward compatibility
            return Ok(ClinicRoomNames.DefaultRooms.Select((name, i) => new
            {
                Id = Guid.Empty,
                ArabicName = name,
                EnglishName = (string?)null,
                Code = $"room-{i + 1}",
                RoomType = "Treatment"
            }));
        }

        return Ok(rooms);
    }

    // ─── Legacy endpoints (backward compatibility) ───────────────────────────
    // CLIN-19: These legacy endpoints duplicate PatientJourneyController functionality
    // but lack SignalR push and proper transition validation. They should be removed
    // once the frontend is confirmed to use PatientJourney.Intake instead.

    /// <summary>Marks appointment as Waiting and sets ArrivedAt. Legacy endpoint — use POST /api/patient-journey/{id}/intake instead.</summary>
    [Obsolete("Use POST /api/patient-journey/{id}/intake instead. This legacy endpoint does not push SignalR updates.")]
    [HttpPost("arrive/{id:guid}")]
    public async Task<IActionResult> MarkArrived(Guid id, [FromBody] StartVisitRequest? req = null)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        var dateError = ValidateJourneyBusinessDate(appointment, req, out var dateDecision);
        if (dateError != null) return dateError;
        var today = dateDecision.BusinessDate;

        if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.NoShow)
            return BadRequest(new { message = "لا يمكن تسجيل وصول موعد ملغى أو لم يحضر" });

        // M2 FIX: Wrap multi-entity updates in a transaction with advisory lock
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = StableLockKeyHelper.StableGuidToLong(appointment.PatientId);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            await db.Entry(appointment).ReloadAsync();
            dateError = ValidateJourneyBusinessDate(appointment, req, out dateDecision);
            if (dateError != null)
            {
                await tx.RollbackAsync();
                return dateError;
            }
            today = dateDecision.BusinessDate;
            AddFutureAppointmentOverrideAudit(appointment, "LegacyMarkArrived", dateDecision);

            // Also add to clinic queue if not already there (re-check under lock)
            var existingQueueItem = await db.ClinicQueueItems
                .AnyAsync(q => q.AppointmentId == id && q.QueueDate == today
                    && ActiveStatuses.Contains(q.Status)
                    && q.IsActive);

            // SEC-01 FIX: Validate appointment status transition before applying
            if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Waiting))
                return BadRequest(new { message = $"لا يمكن تغيير حالة الموعد من {appointment.Status} إلى {AppointmentStatus.Waiting}" });

            var eventUtc = clinicClock.UtcNow();
            appointment.Status = AppointmentStatus.Waiting;
            appointment.ArrivedAt = eventUtc;
            appointment.UpdatedAt = eventUtc;

            if (!existingQueueItem)
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
                    AddedByUserId = GetCurrentUserId()
                };
                db.ClinicQueueItems.Add(queueItem);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return Ok(new
        {
            appointment.Id,
            Status = appointment.Status.ToString(),
            appointment.ArrivedAt,
            message = "تم تسجيل وصول المريض بنجاح"
        });
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

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
            NewData = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new
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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Gets the current user's branch ID for SignalR branch-scoped push.
    /// Returns null for Admin users (who see all branches).
    /// </summary>
    private Guid? GetCurrentBranchId()
    {
        var branchIdClaim = User.FindFirst("BranchId")?.Value;
        if (Guid.TryParse(branchIdClaim, out var branchId) && branchId != Guid.Empty)
            return branchId;

        // Fallback: check if user is admin — admins don't have branch scope
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role == "Admin") return null;

        // Non-admin without branch claim — try to resolve from DB
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            var user = db.Users.Find(userId.Value);
            if (user?.BranchId.HasValue == true && user.BranchId.Value != Guid.Empty)
                return user.BranchId.Value;
        }

        return null;
    }

    /// <summary>
    /// Validates a room name by checking the ClinicRooms DB table first,
    /// then falling back to the hardcoded ClinicRoomNames constants.
    /// </summary>
    private async Task<bool> IsRoomValidAsync(string? roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName)) return false;

        // Check DB rooms first
        var dbRoomExists = await db.ClinicRooms
            .AnyAsync(r => r.IsActive && (r.ArabicName == roomName || r.EnglishName == roomName || r.Code == roomName));

        if (dbRoomExists) return true;

        // Fallback to hardcoded constants for backward compatibility
        return ClinicRoomNames.IsValid(roomName);
    }

    private async Task SyncAppointmentStatus(ClinicQueueItem item, AppointmentStatus appointmentStatus)
    {
        if (item.AppointmentId.HasValue)
        {
            var appointment = await db.Appointments.FindAsync(item.AppointmentId.Value);
            if (appointment != null && appointment.IsActive)
            {
                // SEC-01 FIX: Validate appointment status transition before applying
                if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, appointmentStatus))
                {
                    // SEC-01 FIX: Log invalid transition for audit trail instead of silently returning
                    logger.LogWarning(
                        "SEC-01: Invalid appointment status transition blocked in SyncAppointmentStatus. " +
                        "AppointmentId={AppointmentId}, CurrentStatus={CurrentStatus}, RequestedStatus={RequestedStatus}, " +
                        "QueueItemId={QueueItemId}",
                        appointment.Id, appointment.Status, appointmentStatus, item.Id);
                    // Don't throw — queue and appointment can diverge
                    // The queue transition is already validated by ClinicQueueStatusTransitions
                    // This prevents corrupting appointment state while allowing queue to proceed
                    return;
                }

                appointment.Status = appointmentStatus;
                appointment.UpdatedAt = DateTime.UtcNow;

                // Sync room name and ClinicRoomId
                if (!string.IsNullOrWhiteSpace(item.RoomName))
                    appointment.RoomName = item.RoomName;
                if (item.ClinicRoomId.HasValue)
                    appointment.ClinicRoomId = item.ClinicRoomId;
            }
        }
    }

    /// <summary>
    /// Build a display-safe patient name: FirstName + MiddleName (if present) + LastName.
    /// Trims extra whitespace. Returns empty string if patient is null.
    /// </summary>
    private static string BuildPatientDisplayName(Patient? patient)
    {
        if (patient == null) return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(patient.FirstName))
            parts.Add(patient.FirstName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.MiddleName))
            parts.Add(patient.MiddleName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.LastName))
            parts.Add(patient.LastName.Trim());

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Public TV display must not expose full patient names. Show enough context
    /// for the patient to recognize their turn while avoiding full-name disclosure.
    /// </summary>
    private static string BuildPublicPatientDisplayName(Patient? patient)
    {
        if (patient == null) return "";

        var firstName = patient.FirstName?.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstName))
            return "مريض";

        var lastName = patient.LastName?.Trim();
        if (string.IsNullOrWhiteSpace(lastName))
            return firstName;

        return $"{firstName} {lastName[0]}.";
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class AddToQueueRequest : IFutureAppointmentOverrideRequest
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? ClinicRoomId { get; set; }
    public string? RoomName { get; set; }
    public string? Notes { get; set; }
    public bool OverrideFutureAppointment { get; set; }
    public string? OverrideReason { get; set; }
}

public class CallQueuePatientRequest
{
    public string? RoomName { get; set; }
}

public class ChangeRoomRequest
{
    public string RoomName { get; set; } = string.Empty;
}

public class ChangePriorityRequest
{
    public ClinicQueuePriority Priority { get; set; } = ClinicQueuePriority.Normal;
    public string? Reason { get; set; }
}

public class ReorderItemRequest
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
}

public class NotifyPatientRequest
{
    /// <summary>Notification channel: "sms", "whatsapp", or "both"</summary>
    public string Channel { get; set; } = "sms";
}
