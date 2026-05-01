using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

public class AppointmentService(IAppointmentRepository repo, ICurrentUserService currentUser, INotificationService notifications)
{
    public async Task<IEnumerable<AppointmentDto>> GetTodayAsync(Guid? doctorId = null)
    {
        var branchId = currentUser.IsAdmin ? null : currentUser.BranchId;
        var list = await repo.GetTodayAsync(branchId, doctorId);
        return list.Select(ToDto);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, Guid? doctorId = null)
    {
        var branchId = currentUser.IsAdmin ? null : currentUser.BranchId;
        var list = await repo.GetByDateRangeAsync(from, to, branchId, doctorId);
        return list.Select(ToDto);
    }

    public async Task<(AppointmentDto? result, string? error)> CreateAsync(CreateAppointmentRequest req)
    {
        var date = DateOnly.Parse(req.AppointmentDate);
        var start = TimeOnly.Parse(req.StartTime);
        var end = start.AddMinutes(req.DurationMinutes);

        if (await repo.HasConflictAsync(req.DoctorId, date, start, end))
            return (null, "يوجد تعارض في المواعيد مع هذا الطبيب في هذا الوقت");

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
            CreatedBy = currentUser.UserId
        };

        await repo.AddAsync(appointment);
        await repo.SaveChangesAsync();

        // Notify the doctor
        var dto = ToDto(appointment);
        _ = Task.Run(async () =>
        {
            try
            {
                var patientName = dto.PatientName.Length > 0 ? dto.PatientName : "مريض";
                await notifications.NotifyDoctorAsync(
                    req.DoctorId,
                    "appointment",
                    "موعد جديد",
                    $"تم حجز موعد جديد لـ {patientName} بتاريخ {dto.AppointmentDate} الساعة {dto.StartTime}",
                    "Appointment",
                    appointment.Id);
            }
            catch { /* non-blocking */ }
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

        appointment.DoctorId        = req.DoctorId;
        appointment.AppointmentDate = date;
        appointment.StartTime       = start;
        appointment.EndTime         = end;
        appointment.DurationMinutes = req.DurationMinutes;
        appointment.AppointmentType = req.AppointmentType;
        appointment.Specialty       = req.Specialty != null ? Enum.Parse<Specialty>(req.Specialty, true) : null;
        appointment.Notes           = req.Notes;

        repo.Update(appointment);
        await repo.SaveChangesAsync();
        return (ToDto(appointment), null);
    }

    public async Task<(AppointmentDto? result, string? error)> UpdateStatusAsync(
        Guid id, string status)
    {
        var appointment = await repo.GetByIdAsync(id);
        if (appointment == null) return (null, "الموعد غير موجود");

        if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsed))
            return (null, "حالة الموعد غير صالحة");

        appointment.Status = parsed;
        repo.Update(appointment);
        await repo.SaveChangesAsync();
        return (ToDto(appointment), null);
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
        Notes = a.Notes
    };
}
