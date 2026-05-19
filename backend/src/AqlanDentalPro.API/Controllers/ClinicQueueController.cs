using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Constants;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Clinic queue management — today's waiting list, patient calling, and TV display.
/// Sprint 7: Full clinic queue with dedicated ClinicQueueItem entity,
/// room assignment, patient calling, visit integration, and voice calling.
/// </summary>
[ApiController]
[Route("api/clinic-queue")]
[Authorize(Policy = "StaffOnly")]
public class ClinicQueueController(AppDbContext db, ILogger<ClinicQueueController> logger) : ControllerBase
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
    /// <summary>Returns today's clinic queue items. Optional doctorId filter.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayQueue([FromQuery] Guid? doctorId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = db.ClinicQueueItems
            .Include(q => q.Patient)
            .Include(q => q.Doctor)
            .Include(q => q.Appointment)
            .Where(q => q.QueueDate == today && q.IsActive);

        if (doctorId.HasValue)
            query = query.Where(q => q.DoctorId == doctorId.Value);

        var items = await query
            .OrderBy(q => q.CreatedAt)
            .ToListAsync();

        var result = items.Select(q => new
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
            q.CalledAt,
            q.InRoomAt,
            q.StartedAt,
            q.CompletedAt,
            q.Notes,
            q.CreatedAt
        });

        return Ok(result);
    }

    // ─── POST /api/clinic-queue ──────────────────────────────────────────────
    /// <summary>Adds a patient to today's clinic queue.</summary>
    /// <remarks>H2 FIX: Uses DB advisory lock to prevent duplicate queue entries under concurrent requests.</remarks>
    [HttpPost]
    public async Task<IActionResult> AddToQueue([FromBody] AddToQueueRequest req)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
            var lockKey = (int)(req.PatientId.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Check for duplicate active queue item today (now safe under lock)
            var existingActive = await db.ClinicQueueItems
                .AnyAsync(q => q.PatientId == req.PatientId
                            && q.QueueDate == today
                            && ActiveStatuses.Contains(q.Status)
                            && q.IsActive);

            if (existingActive)
                return Conflict(new { message = "يوجد عنصر نشط لهذا المريض في طابور اليوم بالفعل" });

            // Validate appointment if provided
            if (req.AppointmentId.HasValue)
            {
                var appointment = await db.Appointments.FindAsync(req.AppointmentId.Value);
                if (appointment is null)
                    return NotFound(new { message = "الموعد غير موجود" });
                if (appointment.PatientId != req.PatientId)
                    return BadRequest(new { message = "الموعد لا ينتمي لهذا المريض" });
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
                message = "تمت إضافة المريض إلى الطابور بنجاح"
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
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems.FindAsync(id);
            if (item is null)
                return NotFound(new { message = "عنصر الطابور غير موجود" });
            if (!item.IsActive)
                return BadRequest(new { message = "عنصر الطابور محذوف" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.Called);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            // Validate and assign room if provided
            var roomName = req?.RoomName ?? item.RoomName;
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
                return NotFound(new { message = "عنصر الطابور غير موجود" });

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
    public async Task<IActionResult> StartVisit(Guid id)
    {
        // CON-03 FIX: Add advisory lock for concurrency protection
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // CON-03 FIX: Acquire advisory lock on queue item
            var lockKey = (int)(id.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var item = await db.ClinicQueueItems
                .Include(q => q.Appointment)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (item is null)
                return NotFound(new { message = "عنصر الطابور غير موجود" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.InProgress);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            // Create a Visit if not already linked
            if (item.VisitId == null)
            {
                var visit = new Visit
                {
                    PatientId = item.PatientId,
                    AppointmentId = item.AppointmentId,
                    VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    DoctorId = item.DoctorId ?? item.Appointment?.DoctorId,
                    Specialty = item.Appointment?.Specialty
                };

                db.Visits.Add(visit);
                await db.SaveChangesAsync(); // Save to get the ID
                item.VisitId = visit.Id;
            }

            item.Status = ClinicQueueStatus.InProgress;
            item.StartedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            await SyncAppointmentStatus(item, AppointmentStatus.InProgress);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

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
            await tx.RollbackAsync();
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
                return NotFound(new { message = "عنصر الطابور غير موجود" });

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

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.CompletedAt,
                message = "تم إكمال عنصر الطابور بنجاح"
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
                return NotFound(new { message = "عنصر الطابور غير موجود" });

            // CON-01 FIX: Use centralized transition validation with Arabic error message
            var validationError = ClinicQueueStatusTransitions.GetValidationError(item.Status, ClinicQueueStatus.Cancelled);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            item.Status = ClinicQueueStatus.Cancelled;
            item.CancelledAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                item.Id,
                Status = item.Status.ToString(),
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(item.Status),
                item.CancelledAt,
                message = "تم إلغاء عنصر الطابور بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── PATCH /api/clinic-queue/{id}/room ───────────────────────────────────
    /// <summary>Changes the room assignment for a queue item.</summary>
    [HttpPatch("{id:guid}/room")]
    public async Task<IActionResult> ChangeRoom(Guid id, [FromBody] ChangeRoomRequest req)
    {
        var item = await db.ClinicQueueItems.FindAsync(id);
        if (item is null)
            return NotFound(new { message = "عنصر الطابور غير موجود" });

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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = await db.ClinicQueueItems
            .Include(q => q.Patient)
            .Include(q => q.Doctor)
            .Where(q => q.QueueDate == today && q.IsActive
                && (q.Status == ClinicQueueStatus.Waiting
                    || q.Status == ClinicQueueStatus.Called
                    || q.Status == ClinicQueueStatus.InRoom
                    || q.Status == ClinicQueueStatus.InProgress))
            .OrderByDescending(q => q.CalledAt ?? q.CreatedAt)
            .ToListAsync();

        // Latest called patient (only Called status — for voice announcement trigger)
        var latestCalled = items
            .Where(q => q.Status == ClinicQueueStatus.Called && q.CalledAt != null)
            .OrderByDescending(q => q.CalledAt)
            .Select(q => new
            {
                QueueItemId = q.Id,
                PatientNumber = q.Patient?.PatientNumber ?? "",
                PatientName = BuildPatientDisplayName(q.Patient),
                DoctorName = q.Doctor != null ? q.Doctor.Name : "",
                q.RoomName,
                q.CalledAt
            })
            .FirstOrDefault();

        // Waiting list
        var waitingList = items
            .Where(q => q.Status == ClinicQueueStatus.Waiting)
            .OrderBy(q => q.CreatedAt)
            .Select(q => new
            {
                QueueItemId = q.Id,
                PatientNumber = q.Patient?.PatientNumber ?? "",
                PatientName = BuildPatientDisplayName(q.Patient),
                DoctorName = q.Doctor != null ? q.Doctor.Name : "",
                Status = "في الانتظار"
            })
            .ToList();

        // Recently called (Called + InRoom, most recent first)
        var recentlyCalled = items
            .Where(q => (q.Status == ClinicQueueStatus.Called || q.Status == ClinicQueueStatus.InRoom) && q.CalledAt != null)
            .OrderByDescending(q => q.CalledAt)
            .Take(5)
            .Select(q => new
            {
                QueueItemId = q.Id,
                PatientNumber = q.Patient?.PatientNumber ?? "",
                PatientName = BuildPatientDisplayName(q.Patient),
                DoctorName = q.Doctor != null ? q.Doctor.Name : "",
                q.RoomName,
                StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(q.Status),
                Status = q.Status.ToString(),
                q.CalledAt
            })
            .ToList();

        return Ok(new
        {
            LatestCalled = latestCalled,
            WaitingCount = waitingList.Count,
            WaitingList = waitingList,
            RecentlyCalled = recentlyCalled
        });
    }

    // ─── GET /api/clinic-queue/rooms ─────────────────────────────────────────
    /// <summary>Returns available room names for the room selector. Queries from DB.</summary>
    [HttpGet("rooms")]
    [AllowAnonymous]
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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

    /// <summary>Marks appointment as Waiting and sets ArrivedAt. Legacy endpoint.</summary>
    [HttpPost("arrive/{id:guid}")]
    public async Task<IActionResult> MarkArrived(Guid id)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (appointment.AppointmentDate != today)
            return BadRequest(new { message = "لا يمكن تسجيل الوصول إلا لمواعيد اليوم" });

        if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.NoShow)
            return BadRequest(new { message = "لا يمكن تسجيل وصول موعد ملغى أو لم يحضر" });

        // M2 FIX: Wrap multi-entity updates in a transaction with advisory lock
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = (int)(appointment.PatientId.GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Also add to clinic queue if not already there (re-check under lock)
            var existingQueueItem = await db.ClinicQueueItems
                .AnyAsync(q => q.AppointmentId == id && q.QueueDate == today
                    && ActiveStatuses.Contains(q.Status)
                    && q.IsActive);

            // SEC-01 FIX: Validate appointment status transition before applying
            if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Waiting))
                return BadRequest(new { message = $"لا يمكن تغيير حالة الموعد من {appointment.Status} إلى {AppointmentStatus.Waiting}" });

            appointment.Status = AppointmentStatus.Waiting;
            appointment.ArrivedAt = DateTime.UtcNow;
            appointment.UpdatedAt = DateTime.UtcNow;

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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
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
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class AddToQueueRequest
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? ClinicRoomId { get; set; }
    public string? RoomName { get; set; }
    public string? Notes { get; set; }
}

public class CallQueuePatientRequest
{
    public string? RoomName { get; set; }
}

public class ChangeRoomRequest
{
    public string RoomName { get; set; } = string.Empty;
}
