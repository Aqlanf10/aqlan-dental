using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Application.Services;

public class AppointmentService(IAppointmentRepository repo, ICurrentUserService currentUser, IServiceScopeFactory scopeFactory, ILogger<AppointmentService> logger)
{
    public async Task<IEnumerable<AppointmentDto>> GetTodayAsync(Guid? doctorId = null)
    {
        var branchId = currentUser.IsAdmin ? null : currentUser.BranchId;
        var list = await repo.GetTodayAsync(branchId, doctorId);
        return list.Select(ToDto);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, Guid? doctorId = null, Guid? patientId = null, AppointmentStatus? status = null)
    {
        var branchId = currentUser.IsAdmin ? null : currentUser.BranchId;
        // GAP-01 FIX: Pass status to repository for DB-level filtering
        var list = await repo.GetByDateRangeAsync(from, to, branchId, doctorId, patientId, status);
        return list.Select(ToDto);
    }

    public async Task<(AppointmentDto? result, string? error)> CreateAsync(CreateAppointmentRequest req)
    {
        var date = DateOnly.Parse(req.AppointmentDate);
        var start = TimeOnly.Parse(req.StartTime);
        var end = start.AddMinutes(req.DurationMinutes);

        var appointment = new Appointment
        {
            PatientId = req.PatientId,
            DoctorId = req.DoctorId,
            BranchId = currentUser.BranchId,
            AppointmentDate = date,
            StartTime = start,
            EndTime = end,
            DurationMinutes = req.DurationMinutes,
            AppointmentType = req.AppointmentType,
            Specialty = req.Specialty != null ? Enum.Parse<Specialty>(req.Specialty, true) : null,
            Notes = req.Notes,
            CreatedBy = currentUser.UserId,
            ServiceId = req.ServiceId,
            ClinicRoomId = req.ClinicRoomId,
            OrthoCaseId = req.OrthoCaseId,
            // YOLO-S1: companion + color + package (all nullable; existing callers see null)
            CompanionName = req.CompanionName,
            CompanionPhone = req.CompanionPhone,
            CompanionRelationship = req.CompanionRelationship,
            AppointmentColor = req.AppointmentColor,
            PackageId = req.PackageId,
        };

        // C-15: atomic conflict-check + insert (transaction + per-doctor advisory
        // lock) to prevent the double-booking race between check and save.
        if (!await repo.TryCreateWithConflictGuardAsync(appointment))
            return (null, "يوجد تعارض في المواعيد مع هذا الطبيب في هذا الوقت");

        // M1 FIX: Use IServiceScopeFactory for proper DI in fire-and-forget
        var dto = ToDto(appointment);
        var doctorId = req.DoctorId;
        var patientLabel = dto.PatientName.Length > 0 ? dto.PatientName : "مريض";
        var appointmentDate = dto.AppointmentDate;
        var startTime = dto.StartTime;
        var appointmentId = appointment.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notifications.NotifyDoctorAsync(
                    doctorId,
                    "appointment",
                    "موعد جديد",
                    $"تم حجز موعد جديد لـ {patientLabel} بتاريخ {appointmentDate} الساعة {startTime}",
                    "Appointment",
                    appointmentId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[AppointmentService] Doctor notification failed for appointment {AppointmentId}", appointmentId);
            }
        });

        return (dto, null);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByPatientAsync(Guid patientId)
    {
        var list = await repo.GetByPatientAsync(patientId);
        return list.Select(ToDto);
    }

    public async Task<AppointmentDto?> GetByIdAsync(Guid id)
    {
        var a = await repo.GetWithDetailAsync(id);
        return a == null ? null : ToDto(a);
    }

    public async Task<DailyAppointmentStatsDto> GetDailyStatsAsync(DateOnly date)
    {
        var branchId = currentUser.IsAdmin ? null : currentUser.BranchId;
        var appointments = await repo.GetByDateRangeAsync(date, date, branchId, null);

        var list = appointments.ToList();
        var total = list.Count;
        var completed = list.Count(a => a.Status == AppointmentStatus.Completed);
        var cancelled = list.Count(a => a.Status == AppointmentStatus.Cancelled);
        var noShow = list.Count(a => a.Status == AppointmentStatus.NoShow);
        var inProgress = list.Count(a => a.Status == AppointmentStatus.InProgress || a.Status == AppointmentStatus.Called || a.Status == AppointmentStatus.InRoom);
        var waiting = list.Count(a => a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Arrived || a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed);

        return new DailyAppointmentStatsDto
        {
            Total = total,
            Completed = completed,
            Cancelled = cancelled,
            NoShow = noShow,
            InProgress = inProgress,
            Waiting = waiting,
            CompletionRate = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0,
            NoShowRate = total > 0 ? Math.Round((double)noShow / total * 100, 1) : 0
        };
    }

    public async Task<(AppointmentDto? result, string? error)> UpdateAsync(
        Guid id, CreateAppointmentRequest req)
    {
        var appointment = await repo.GetWithDetailAsync(id);
        if (appointment == null) return (null, "الموعد غير موجود");

        var date  = DateOnly.Parse(req.AppointmentDate);
        var start = TimeOnly.Parse(req.StartTime);
        var end   = start.AddMinutes(req.DurationMinutes);

        if (await repo.HasConflictAsync(req.DoctorId, date, start, end, excludeId: id))
            return (null, "يوجد تعارض في المواعيد مع هذا الطبيب في هذا الوقت");

        var previousDoctorId = appointment.DoctorId;
        appointment.DoctorId        = req.DoctorId;
        appointment.AppointmentDate = date;
        appointment.StartTime       = start;
        appointment.EndTime         = end;
        appointment.DurationMinutes = req.DurationMinutes;
        appointment.AppointmentType = req.AppointmentType;
        appointment.Specialty       = req.Specialty != null ? Enum.Parse<Specialty>(req.Specialty, true) : null;
        appointment.Notes           = req.Notes;
        appointment.ServiceId       = req.ServiceId;
        appointment.ClinicRoomId    = req.ClinicRoomId;
        appointment.OrthoCaseId      = req.OrthoCaseId;
        // YOLO-S1: companion + color + package (all nullable; updating with null clears them, which is intended)
        appointment.CompanionName         = req.CompanionName;
        appointment.CompanionPhone        = req.CompanionPhone;
        appointment.CompanionRelationship = req.CompanionRelationship;
        appointment.AppointmentColor      = req.AppointmentColor;
        appointment.PackageId             = req.PackageId;

        repo.Update(appointment);
        await repo.SaveChangesAsync();

        var dto           = ToDto(appointment);
        var patientLabel  = dto.PatientName.Length > 0 ? dto.PatientName : "مريض";
        var appointmentId = appointment.Id;
        var newDoctorId   = req.DoctorId;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                if (previousDoctorId != newDoctorId)
                {
                    // Doctor transferred — notify the new doctor
                    await notifications.NotifyDoctorAsync(
                        newDoctorId,
                        "appointment_update",
                        "موعد محوّل إليك",
                        $"تم تحويل موعد {patientLabel} إليك بتاريخ {dto.AppointmentDate} الساعة {dto.StartTime}",
                        "Appointment",
                        appointmentId);
                }
                else
                {
                    // Same doctor — notify rescheduled
                    await notifications.NotifyDoctorAsync(
                        newDoctorId,
                        "appointment_update",
                        "تم تعديل موعد",
                        $"تم تعديل موعد {patientLabel} إلى {dto.AppointmentDate} الساعة {dto.StartTime}",
                        "Appointment",
                        appointmentId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[AppointmentService] Update notification failed for appointment {Id}", appointmentId);
            }
        });

        return (dto, null);
    }

    public async Task<(AppointmentDto? result, string? error)> UpdateStatusAsync(
        Guid id, string status)
    {
        var appointment = await repo.GetWithDetailAsync(id);
        if (appointment == null) return (null, "الموعد غير موجود");

        if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsed))
            return (null, "حالة الموعد غير صالحة");

        // Validate status transition using centralized rules
        // Terminal states (NoShow/Cancelled) can only transition to Scheduled (re-scheduling)
        if (appointment.Status == AppointmentStatus.NoShow ||
            appointment.Status == AppointmentStatus.Cancelled)
        {
            // SEC-01 FIX: Only allow re-scheduling from terminal states, not arbitrary transitions
            if (parsed != AppointmentStatus.Scheduled)
            {
                return (null, $"لا يمكن تغيير حالة الموعد من {appointment.Status} إلى {parsed}. يمكن فقط إعادة جدولة المواعيد الملغية أو التي لم تحضر");
            }
        }
        else if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, parsed))
        {
            return (null, $"لا يمكن تغيير حالة الموعد من {appointment.Status} إلى {parsed}");
        }

        appointment.Status = parsed;
        repo.Update(appointment);
        await repo.SaveChangesAsync();

        var dto           = ToDto(appointment);
        var doctorId      = appointment.DoctorId;
        var patientLabel  = dto.PatientName.Length > 0 ? dto.PatientName : "مريض";
        var appointmentId = appointment.Id;

        // Notify doctor on status changes that affect their schedule awareness
        if (parsed is AppointmentStatus.Confirmed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    var (notifType, title, body) = parsed switch
                    {
                        AppointmentStatus.Confirmed => (
                            "appointment_confirmed",
                            "تأكيد موعد",
                            $"أكّد {patientLabel} حضوره بتاريخ {dto.AppointmentDate} الساعة {dto.StartTime}"),
                        AppointmentStatus.Cancelled => (
                            "appointment_cancelled",
                            "إلغاء موعد",
                            $"تم إلغاء موعد {patientLabel} بتاريخ {dto.AppointmentDate} الساعة {dto.StartTime}"),
                        AppointmentStatus.NoShow => (
                            "appointment_noshow",
                            "غياب مريض",
                            $"لم يحضر {patientLabel} لموعده بتاريخ {dto.AppointmentDate}"),
                        _ => ("appointment_status", "تحديث موعد", $"تم تحديث حالة موعد {patientLabel}")
                    };

                    await notifications.NotifyDoctorAsync(doctorId, notifType, title, body, "Appointment", appointmentId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[AppointmentService] Status notification failed for appointment {Id}", appointmentId);
                }
            });
        }

        return (dto, null);
    }

    public async Task<bool> CheckConflictAsync(Guid doctorId, string date, string startTime, int durationMinutes, Guid? excludeId)
    {
        if (!DateOnly.TryParse(date, out var appointmentDate))
            throw new ArgumentException("Invalid date format");

        if (!TimeOnly.TryParse(startTime, out var start))
            throw new ArgumentException("Invalid time format");

        var endTime = start.AddMinutes(durationMinutes);
        return await repo.HasConflictAsync(doctorId, appointmentDate, start, endTime, excludeId);
    }

    private static AppointmentDto ToDto(Appointment a) => new()
    {
        Id = a.Id,
        PatientId = a.PatientId,
        PatientName = a.Patient != null
            ? $"{a.Patient.FirstName} {a.Patient.LastName}".Trim()
            : string.Empty,
        PatientNumber = a.Patient?.PatientNumber ?? string.Empty,
        DoctorId = a.DoctorId,
        DoctorName = a.Doctor?.Name ?? string.Empty,
        DoctorColor = a.Doctor?.Color,
        AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
        StartTime = a.StartTime.ToString("HH:mm"),
        EndTime = a.EndTime.ToString("HH:mm"),
        DurationMinutes = a.DurationMinutes,
        AppointmentType = a.AppointmentType,
        Specialty = a.Specialty?.ToString(),
        Status = a.Status.ToString(),
        Notes = a.Notes,
        RoomName = a.RoomName,
        ArrivedAt = a.ArrivedAt,
        CalledAt = a.CalledAt,
        InRoomAt = a.InRoomAt,
        ServiceId = a.ServiceId,
        ClinicRoomId = a.ClinicRoomId,
        OrthoCaseId = a.OrthoCaseId,
        // YOLO-S1
        CompanionName = a.CompanionName,
        CompanionPhone = a.CompanionPhone,
        CompanionRelationship = a.CompanionRelationship,
        AppointmentColor = a.AppointmentColor,
        PackageId = a.PackageId,
        PackageName = a.Package?.Name,
        PackageColor = a.Package?.Color,
    };
}
