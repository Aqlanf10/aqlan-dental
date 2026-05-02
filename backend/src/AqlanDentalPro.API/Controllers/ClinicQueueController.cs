using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Domain.Constants;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Clinic queue management — today's waiting list, patient calling, and TV display.
/// Sprint 4.5: Simple clinic queue and patient calling screen.
/// </summary>
[ApiController]
[Route("api/clinic-queue")]
[Authorize]
public class ClinicQueueController(AppDbContext db) : ControllerBase
{
    // ─── GET /api/clinic-queue/today ─────────────────────────────────────────
    /// <summary>Returns today's appointments with queue information.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayQueue()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var appointments = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == today && a.IsActive)
            .OrderBy(a => a.StartTime)
            .Select(a => new
            {
                AppointmentId = a.Id,
                a.PatientId,
                PatientName = a.Patient != null
                    ? (a.Patient.FirstName + " " + a.Patient.LastName).Trim()
                    : "",
                PatientNumber = a.Patient != null ? a.Patient.PatientNumber : "",
                DoctorName = a.Doctor != null ? a.Doctor.Name : "",
                AppointmentTime = a.StartTime.ToString("HH:mm"),
                Status = a.Status.ToString(),
                a.RoomName,
                a.ArrivedAt,
                a.CalledAt,
                a.InRoomAt
            })
            .ToListAsync();

        return Ok(appointments);
    }

    // ─── POST /api/clinic-queue/arrive/{id} ─────────────────────────────────
    /// <summary>Marks appointment as Waiting and sets ArrivedAt.</summary>
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

        // Cannot arrive cancelled or no-show appointments
        if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.NoShow)
            return BadRequest(new { message = "لا يمكن تسجيل وصول موعد ملغى أو لم يحضر" });

        // Already in a later stage
        if (appointment.Status == AppointmentStatus.Waiting ||
            appointment.Status == AppointmentStatus.Called ||
            appointment.Status == AppointmentStatus.InRoom ||
            appointment.Status == AppointmentStatus.InProgress ||
            appointment.Status == AppointmentStatus.Completed)
            return BadRequest(new { message = "المريض مسجل بالفعل في الطابور" });

        appointment.Status = AppointmentStatus.Waiting;
        appointment.ArrivedAt = DateTime.UtcNow;
        appointment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new
        {
            appointment.Id,
            Status = appointment.Status.ToString(),
            appointment.ArrivedAt,
            message = "تم تسجيل وصول المريض بنجاح"
        });
    }

    // ─── POST /api/clinic-queue/call/{id} ────────────────────────────────────
    /// <summary>Marks appointment as Called, sets CalledAt, saves RoomName.</summary>
    [HttpPost("call/{id:guid}")]
    public async Task<IActionResult> CallPatient(Guid id, [FromBody] CallPatientRequest req)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (appointment.AppointmentDate != today)
            return BadRequest(new { message = "لا يمكن نداء المريض إلا لمواعيد اليوم" });

        // Must be Waiting or Arrived to be called
        if (appointment.Status != AppointmentStatus.Waiting &&
            appointment.Status != AppointmentStatus.Arrived &&
            appointment.Status != AppointmentStatus.Scheduled &&
            appointment.Status != AppointmentStatus.Confirmed)
        {
            return BadRequest(new { message = "لا يمكن نداء المريض في الحالة الحالية" });
        }

        // Validate room name
        if (!ClinicRoomNames.IsValid(req.RoomName))
            return BadRequest(new { message = "اسم الغرفة غير صالح. يجب أن يكون: غرفة 1 أو غرفة 2 أو غرفة 3" });

        appointment.Status = AppointmentStatus.Called;
        appointment.CalledAt = DateTime.UtcNow;
        appointment.RoomName = req.RoomName;
        appointment.UpdatedAt = DateTime.UtcNow;

        // If ArrivedAt was never set (patient called directly without arrive), set it now
        appointment.ArrivedAt ??= DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new
        {
            appointment.Id,
            Status = appointment.Status.ToString(),
            appointment.RoomName,
            appointment.CalledAt,
            message = "تم نداء المريض بنجاح"
        });
    }

    // ─── POST /api/clinic-queue/send-to-room/{id} ───────────────────────────
    /// <summary>Marks appointment as InRoom and sets InRoomAt.</summary>
    [HttpPost("send-to-room/{id:guid}")]
    public async Task<IActionResult> SendToRoom(Guid id)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null)
            return NotFound(new { message = "الموعد غير موجود" });

        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (appointment.AppointmentDate != today)
            return BadRequest(new { message = "لا يمكن إرسال المريض للغرفة إلا لمواعيد اليوم" });

        // Must be Called to send to room
        if (appointment.Status != AppointmentStatus.Called)
            return BadRequest(new { message = "يجب نداء المريض أولاً قبل إرساله للغرفة" });

        appointment.Status = AppointmentStatus.InRoom;
        appointment.InRoomAt = DateTime.UtcNow;
        appointment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new
        {
            appointment.Id,
            Status = appointment.Status.ToString(),
            appointment.RoomName,
            appointment.InRoomAt,
            message = "تم إرسال المريض إلى الغرفة بنجاح"
        });
    }

    // ─── GET /api/clinic-queue/display ───────────────────────────────────────
    /// <summary>Returns data for the TV queue display screen.</summary>
    [HttpGet("display")]
    [AllowAnonymous] // TV display should work without staff login
    public async Task<IActionResult> GetDisplay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var appointments = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == today && a.IsActive)
            .OrderBy(a => a.StartTime)
            .ToListAsync();

        // Latest called patient (most recent CalledAt)
        var latestCalled = appointments
            .Where(a => a.Status == AppointmentStatus.Called && a.CalledAt != null)
            .OrderByDescending(a => a.CalledAt)
            .Select(a => new
            {
                a.PatientId,
                PatientNumber = a.Patient?.PatientNumber ?? "",
                PatientName = a.Patient != null
                    ? (a.Patient.FirstName + " " + a.Patient.LastName).Trim()
                    : "",
                DoctorName = a.Doctor != null ? a.Doctor.Name : "",
                a.RoomName,
                a.CalledAt
            })
            .FirstOrDefault();

        // Waiting list summary
        var waitingList = appointments
            .Where(a => a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Arrived)
            .OrderBy(a => a.ArrivedAt)
            .Select(a => new
            {
                a.PatientId,
                PatientNumber = a.Patient?.PatientNumber ?? "",
                PatientName = a.Patient != null
                    ? (a.Patient.FirstName + " " + a.Patient.LastName).Trim()
                    : "",
                AppointmentTime = a.StartTime.ToString("HH:mm"),
                DoctorName = a.Doctor != null ? a.Doctor.Name : "",
                Status = a.Status.ToString()
            })
            .ToList();

        // Recently called list (Called + InRoom, most recent first)
        var recentlyCalled = appointments
            .Where(a => (a.Status == AppointmentStatus.Called || a.Status == AppointmentStatus.InRoom) && a.CalledAt != null)
            .OrderByDescending(a => a.CalledAt)
            .Take(5)
            .Select(a => new
            {
                a.PatientId,
                PatientNumber = a.Patient?.PatientNumber ?? "",
                PatientName = a.Patient != null
                    ? (a.Patient.FirstName + " " + a.Patient.LastName).Trim()
                    : "",
                DoctorName = a.Doctor != null ? a.Doctor.Name : "",
                a.RoomName,
                Status = a.Status.ToString(),
                a.CalledAt
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
    /// <summary>Returns available room names for the room selector.</summary>
    [HttpGet("rooms")]
    [AllowAnonymous]
    public IActionResult GetRooms()
    {
        return Ok(ClinicRoomNames.DefaultRooms);
    }
}
