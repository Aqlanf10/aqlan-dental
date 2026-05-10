using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.Exceptions;
using System.Globalization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public class BookingRequestService(AppDbContext db) : IBookingRequestService
{
    // Clinic working hours: Saturday-Thursday 08:00-20:00, Friday closed
    private static readonly TimeOnly ClinicOpen = new(8, 0);
    private static readonly TimeOnly ClinicClose = new(20, 0);
    private const int DefaultSlotDurationMinutes = 30;
    private const string ClinicIanaTimeZoneId = "Asia/Aden";
    private const string ClinicWindowsTimeZoneId = "Arab Standard Time";

    // Booking request statuses that block a time slot
    private static readonly HashSet<BookingRequestStatus> BlockingStatuses =
    [
        BookingRequestStatus.Pending,
        BookingRequestStatus.Reviewed,
        BookingRequestStatus.Confirmed
    ];

    // Appointment statuses that block a time slot (Cancelled & NoShow do NOT block)
    private static readonly HashSet<AppointmentStatus> BlockingAppointmentStatuses =
    [
        AppointmentStatus.Scheduled,
        AppointmentStatus.Confirmed,
        AppointmentStatus.Arrived,
        AppointmentStatus.Waiting,
        AppointmentStatus.Called,
        AppointmentStatus.InRoom,
        AppointmentStatus.InProgress,
        AppointmentStatus.Completed
    ];

    public async Task<BookingRequestDto> CreateAsync(CreateBookingRequestDto dto)
    {
        // ── Strong validation ──────────────────────────────────────────────

        // PreferredDate is required
        if (string.IsNullOrWhiteSpace(dto.PreferredDate))
            throw new ArgumentException("التاريخ المفضل مطلوب");

        // PreferredTime is required
        if (string.IsNullOrWhiteSpace(dto.PreferredTime))
            throw new ArgumentException("الوقت المفضل مطلوب");

        // Parse and validate date
        if (!DateOnly.TryParse(dto.PreferredDate, out var parsedDate))
            throw new ArgumentException("صيغة التاريخ غير صحيحة");

        var clinicNow = GetClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);

        // PreferredDate cannot be past
        if (parsedDate < today)
            throw new ArgumentException("لا يمكن اختيار تاريخ سابق");

        // Friday blocked
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
            throw new ArgumentException("المركز مغلق يوم الجمعة");

        // Validate email format if provided
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(dto.Email);
                if (addr.Address != dto.Email.Trim())
                    throw new ArgumentException("صيغة البريد الإلكتروني غير صحيحة");
            }
            catch (FormatException)
            {
                throw new ArgumentException("صيغة البريد الإلكتروني غير صحيحة");
            }
        }

        // DoctorId validation: if provided, verify the doctor exists and is active
        if (dto.DoctorId.HasValue)
        {
            var doctorExists = await db.Doctors.AnyAsync(d => d.Id == dto.DoctorId.Value && d.IsActive);
            if (!doctorExists)
                throw new ArgumentException("الطبيب المحدد غير موجود أو غير نشط");
        }

        // PreferredTime must be a valid available slot
        if (!await IsSlotAvailableAsync(dto.PreferredDate, dto.PreferredTime, dto.DoctorId))
            throw new SlotNotAvailableException("هذا الوقت لم يعد متاحًا، يرجى اختيار وقت آخر.");

        // ── Same-day duplicate prevention ──────────────────────────────────
        var normalizedPhone = NormalizePhone(dto.PhoneNumber);
        var normalizedName = NormalizeName(dto.PatientName);
        var targetDate = dto.PreferredDate.Trim();

        // Fetch matching-date active requests to client-evaluate name normalization
        var sameDateRequests = await db.BookingRequests
            .Where(r => r.IsActive
                     && r.PreferredDate == targetDate
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null)
            .Select(r => new { r.PhoneNumber, r.PatientName })
            .ToListAsync();

        var duplicateOnSameDate = sameDateRequests.Any(r =>
            NormalizePhone(r.PhoneNumber) == normalizedPhone
            || NormalizeName(r.PatientName) == normalizedName);

        if (duplicateOnSameDate)
        {
            throw new DuplicateBookingRequestException(
                "لديك طلب حجز سابق لنفس اليوم قيد المراجعة، سيتم التواصل معك قريبًا. لا داعي لإرسال طلب جديد.");
        }

        // ── Create entity ──────────────────────────────────────────────────
        var entity = new BookingRequest
        {
            PatientName = dto.PatientName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Email = dto.Email?.Trim(),
            ServiceType = dto.ServiceType?.Trim(),
            PreferredDate = dto.PreferredDate?.Trim(),
            PreferredTime = dto.PreferredTime?.Trim(),
            Notes = dto.Notes?.Trim(),
            DoctorId = dto.DoctorId
        };

        db.BookingRequests.Add(entity);
        await db.SaveChangesAsync();

        // Look up doctor name for the response DTO
        string? doctorName = null;
        if (dto.DoctorId.HasValue)
        {
            doctorName = await db.Doctors
                .Where(d => d.Id == dto.DoctorId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();
        }

        return ToDto(entity, doctorName);
    }

    public async Task<List<BookingRequestDto>> GetAllAsync(string? statusFilter)
    {
        var query = db.BookingRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<BookingRequestStatus>(statusFilter, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var items = await query
            .Include(r => r.Doctor)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return items.Select(r => ToDto(r, r.Doctor?.Name)).ToList();
    }

    public async Task<BookingRequestDto?> GetByIdAsync(Guid id)
    {
        var entity = await db.BookingRequests
            .Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.Id == id);
        return entity == null ? null : ToDto(entity, entity.Doctor?.Name);
    }

    public async Task<BookingRequestDto?> UpdateStatusAsync(Guid id, UpdateBookingRequestStatusDto dto, Guid reviewedBy)
    {
        var entity = await db.BookingRequests
            .Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return null;

        if (!Enum.TryParse<BookingRequestStatus>(dto.Status, ignoreCase: true, out var status))
            return null;

        entity.Status = status;
        entity.StaffNotes = dto.StaffNotes?.Trim();
        entity.ReviewedBy = reviewedBy;
        entity.ReviewedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(entity, entity.Doctor?.Name);
    }

    public async Task<BookingAvailabilityResponseDto> GetAvailabilityAsync(string date, string? serviceType, Guid? doctorId = null)
    {
        // Parse and validate date
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false, "صيغة التاريخ غير صحيحة");
        }

        var clinicNow = GetClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);

        // Check for past date
        if (parsedDate < today)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false, "لا يمكن اختيار تاريخ سابق");
        }

        // Check for Friday
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], true, "المركز مغلق يوم الجمعة");
        }

        // If doctorId is provided, use doctor-specific logic
        if (doctorId.HasValue)
        {
            return await GetDoctorAvailabilityAsync(date, serviceType, parsedDate, today, clinicNow, doctorId.Value);
        }

        // ── Clinic-wide availability (existing logic) ──────────────────────
        var slots = GenerateTimeSlots(DefaultSlotDurationMinutes, ClinicOpen, ClinicClose);

        // Get existing appointments for this date that block slots
        var appointmentTimes = await db.Appointments
            .Where(a => a.AppointmentDate == parsedDate
                     && BlockingAppointmentStatuses.Contains(a.Status))
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        // Get existing booking requests for this date that block slots
        var bookingRequestTimes = await db.BookingRequests
            .Where(r => r.PreferredDate == date
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null
                     && r.PreferredTime != null)
            .Select(r => r.PreferredTime!)
            .ToListAsync();

        // Mark unavailable slots
        var result = new List<BookingAvailabilitySlotDto>();
        foreach (var slot in slots)
        {
            var slotTime = TimeOnly.Parse(slot);
            var slotEnd = slotTime.AddMinutes(DefaultSlotDurationMinutes);

            var isPastSlotToday = parsedDate == today && slotTime <= TimeOnly.FromDateTime(clinicNow);
            var isBlockedByAppointment = appointmentTimes.Any(a =>
                a.StartTime < slotEnd && a.EndTime > slotTime);
            var isBlockedByBookingRequest = bookingRequestTimes.Any(brTime =>
                IsSameSlotTime(brTime, slot));

            if (isPastSlotToday)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "انتهى الوقت"));
            else if (isBlockedByAppointment || isBlockedByBookingRequest)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "محجوز"));
            else
                result.Add(new BookingAvailabilitySlotDto(slot, true));
        }

        return new BookingAvailabilityResponseDto(date, serviceType, result);
    }

    public async Task<bool> IsSlotAvailableAsync(string date, string time, Guid? doctorId = null)
    {
        // Validate date
        if (!DateOnly.TryParse(date, out var parsedDate))
            return false;

        var clinicNow = GetClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);

        // Past date
        if (parsedDate < today)
            return false;

        // Friday
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
            return false;

        // Find the matching 24h slot format
        var slot24h = NormalizeTo24h(time);
        if (slot24h == null)
            return false;

        var slotTime = TimeOnly.Parse(slot24h);

        // If doctorId is provided, use doctor-specific slot duration
        int slotDuration = DefaultSlotDurationMinutes;
        if (doctorId.HasValue)
        {
            var dayOfWeek = (int)parsedDate.DayOfWeek; // 0=Sunday ... 6=Saturday
            var schedule = await db.DoctorSchedules
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId.Value && s.DayOfWeek == dayOfWeek && s.IsWorking);
            if (schedule == null)
                return false; // Doctor not working this day
            slotDuration = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : DefaultSlotDurationMinutes;
        }

        var slotEnd = slotTime.AddMinutes(slotDuration);

        // Reject same-day slots that have already started or passed in clinic local time.
        if (parsedDate == today && slotTime <= TimeOnly.FromDateTime(clinicNow))
            return false;

        // Check appointments — filter by doctor if doctorId provided
        var appointmentQuery = db.Appointments
            .Where(a => a.AppointmentDate == parsedDate
                     && BlockingAppointmentStatuses.Contains(a.Status)
                     && a.StartTime < slotEnd
                     && a.EndTime > slotTime);
        if (doctorId.HasValue)
            appointmentQuery = appointmentQuery.Where(a => a.DoctorId == doctorId.Value);

        var hasAppointmentConflict = await appointmentQuery.AnyAsync();
        if (hasAppointmentConflict)
            return false;

        // Check booking requests (fetch to client first — IsSameSlotTime cannot be translated to SQL)
        var bookingQuery = db.BookingRequests
            .Where(r => r.PreferredDate == date
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null
                     && r.PreferredTime != null);
        if (doctorId.HasValue)
            bookingQuery = bookingQuery.Where(r => r.DoctorId == doctorId.Value);

        var conflictingBookingTimes = await bookingQuery
            .Select(r => r.PreferredTime!)
            .ToListAsync();

        var hasBookingConflict = conflictingBookingTimes.Any(t => IsSameSlotTime(t, slot24h));

        return !hasBookingConflict;
    }

    public async Task<BookingRequestDto?> ConvertToAppointmentAsync(Guid bookingRequestId, ConvertBookingRequestToAppointmentDto dto, Guid convertedBy)
    {
        // 1. Find the booking request
        var bookingRequest = await db.BookingRequests
            .Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.Id == bookingRequestId);

        if (bookingRequest == null)
            return null;

        // 2. Verify it's in Confirmed status
        if (bookingRequest.Status != BookingRequestStatus.Confirmed)
            throw new ArgumentException("يجب أن يكون الطلب في حالة مؤكد لتحويله إلى موعد");

        // 3. Verify it hasn't been converted already
        if (bookingRequest.ConvertedToAppointmentId.HasValue)
            throw new ArgumentException("تم تحويل هذا الطلب بالفعل إلى موعد");

        // 4. Check for appointment conflicts
        var hasConflict = await db.Appointments
            .AnyAsync(a => a.DoctorId == dto.DoctorId
                        && a.AppointmentDate == dto.AppointmentDate
                        && BlockingAppointmentStatuses.Contains(a.Status)
                        && a.StartTime < dto.EndTime
                        && a.EndTime > dto.StartTime);

        if (hasConflict)
            throw new ArgumentException("يوجد تعارض في المواعيد مع هذا الطبيب في هذا الوقت");

        // 5. Create the Appointment
        var appointment = new Appointment
        {
            PatientId = dto.PatientId,
            DoctorId = dto.DoctorId,
            AppointmentDate = dto.AppointmentDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            DurationMinutes = dto.DurationMinutes,
            AppointmentType = dto.AppointmentType ?? bookingRequest.ServiceType ?? "عام",
            Notes = bookingRequest.Notes,
            CreatedBy = convertedBy
        };

        db.Appointments.Add(appointment);

        // 6. Set BookingRequest.ConvertedToAppointmentId
        bookingRequest.ConvertedToAppointmentId = appointment.Id;

        // 7. Update StaffNotes
        var existingNotes = string.IsNullOrWhiteSpace(bookingRequest.StaffNotes)
            ? ""
            : bookingRequest.StaffNotes + " | ";
        bookingRequest.StaffNotes = existingNotes + "تم تحويل الطلب إلى موعد";

        await db.SaveChangesAsync();

        return ToDto(bookingRequest, bookingRequest.Doctor?.Name);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Doctor-specific availability: checks DoctorSchedule, uses doctor's SlotDurationMinutes,
    /// checks against doctor's existing appointments and booking requests, skips break times.
    /// </summary>
    private async Task<BookingAvailabilityResponseDto> GetDoctorAvailabilityAsync(
        string date, string? serviceType, DateOnly parsedDate, DateOnly today, DateTime clinicNow, Guid doctorId)
    {
        // Check if doctor exists and is active
        var doctor = await db.Doctors
            .Where(d => d.Id == doctorId && d.IsActive)
            .Select(d => new { d.Name })
            .FirstOrDefaultAsync();

        if (doctor == null)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false,
                "الطبيب المحدد غير موجود أو غير نشط", doctorId, null);
        }

        // Check if the doctor has a DoctorSchedule for that day of week
        var dayOfWeek = (int)parsedDate.DayOfWeek; // 0=Sunday ... 6=Saturday
        var schedule = await db.DoctorSchedules
            .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek && s.IsWorking);

        if (schedule == null)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false,
                "لا توجد أوقات متاحة لهذا الطبيب في هذا اليوم.", doctorId, doctor.Name);
        }

        var slotDuration = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : DefaultSlotDurationMinutes;
        var slots = GenerateTimeSlots(slotDuration, schedule.StartTime, schedule.EndTime);

        // Get existing appointments for this doctor on this date that block slots
        var appointmentTimes = await db.Appointments
            .Where(a => a.AppointmentDate == parsedDate
                     && a.DoctorId == doctorId
                     && BlockingAppointmentStatuses.Contains(a.Status))
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        // Get existing booking requests for this doctor on this date that block slots
        var bookingRequestTimes = await db.BookingRequests
            .Where(r => r.PreferredDate == date
                     && r.DoctorId == doctorId
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null
                     && r.PreferredTime != null)
            .Select(r => r.PreferredTime!)
            .ToListAsync();

        // Mark unavailable slots
        var result = new List<BookingAvailabilitySlotDto>();
        foreach (var slot in slots)
        {
            var slotTime = TimeOnly.Parse(slot);
            var slotEnd = slotTime.AddMinutes(slotDuration);

            // Same-day past slots
            var isPastSlotToday = parsedDate == today && slotTime <= TimeOnly.FromDateTime(clinicNow);

            // Break time check
            var isInBreak = schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue
                && slotTime < schedule.BreakEnd && slotEnd > schedule.BreakStart;

            // Appointment conflict
            var isBlockedByAppointment = appointmentTimes.Any(a =>
                a.StartTime < slotEnd && a.EndTime > slotTime);

            // Booking request conflict
            var isBlockedByBookingRequest = bookingRequestTimes.Any(brTime =>
                IsSameSlotTime(brTime, slot));

            if (isPastSlotToday)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "انتهى الوقت"));
            else if (isInBreak)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "استراحة"));
            else if (isBlockedByAppointment || isBlockedByBookingRequest)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "محجوز"));
            else
                result.Add(new BookingAvailabilitySlotDto(slot, true));
        }

        return new BookingAvailabilityResponseDto(date, serviceType, result, false, null, doctorId, doctor.Name);
    }

    /// <summary>
    /// Generates time slots with the given duration between start and end times.
    /// </summary>
    private static List<string> GenerateTimeSlots(int durationMinutes, TimeOnly start, TimeOnly end)
    {
        var slots = new List<string>();
        var current = start;
        while (current < end)
        {
            var slotEnd = current.AddMinutes(durationMinutes);
            if (slotEnd > end) break;
            slots.Add(current.ToString("HH:mm"));
            current = slotEnd;
        }
        return slots;
    }

    /// <summary>
    /// Returns the current clinic-local time. Railway/Linux supports the IANA ID; the Windows ID is kept for local dev fallback.
    /// </summary>
    private static DateTime GetClinicNow()
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ClinicIanaTimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ClinicWindowsTimeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                return DateTime.UtcNow.AddHours(3);
            }
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.UtcNow.AddHours(3);
        }
    }

    /// <summary>
    /// Normalizes an Arabic AM/PM time string (e.g., "9:00 ص") to 24h format (e.g., "09:00").
    /// Also handles already-24h format strings (e.g., "09:00").
    /// </summary>
    private static string? NormalizeTo24h(string time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return null;

        time = time.Trim();

        // Already in 24h format like "09:00" or "14:30"
        if (TimeOnly.TryParseExact(time, "HH:mm", out var t24))
            return t24.ToString("HH:mm");

        // Arabic AM/PM format: "9:00 ص", "2:30 م", "12:00 م"
        var isPM = time.Contains('م');
        var isAM = time.Contains('ص');

        if (!isPM && !isAM)
        {
            // Try general parse as fallback
            if (TimeOnly.TryParse(time, out var tGeneral))
                return tGeneral.ToString("HH:mm");
            return null;
        }

        // Remove Arabic markers and parse
        var cleanTime = time.Replace("ص", "").Replace("م", "").Trim();
        if (!TimeOnly.TryParse(cleanTime, out var parsed))
            return null;

        // Convert to 24h
        if (isPM && parsed.Hour < 12)
            parsed = parsed.AddHours(12);
        else if (isAM && parsed.Hour == 12)
            parsed = parsed.AddHours(-12);

        return parsed.ToString("HH:mm");
    }

    /// <summary>
    /// Checks if a booking request's PreferredTime matches a 24h slot format.
    /// Handles both Arabic ("9:00 ص") and 24h ("09:00") formats.
    /// </summary>
    private static bool IsSameSlotTime(string preferredTime, string slot24h)
    {
        var normalized = NormalizeTo24h(preferredTime);
        return normalized == slot24h;
    }

    /// <summary>
    /// Normalizes a phone number by removing spaces, dashes, parentheses, and plus signs.
    /// </summary>
    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        return phone.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "");
    }

    /// <summary>
    /// Normalizes a patient name by trimming, collapsing whitespace, and lowercasing
    /// so that minor formatting differences don't bypass duplicate detection.
    /// </summary>
    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        // Collapse multiple spaces/tabs into a single space, trim, and lowercase
        return CultureInfo.CurrentCulture.TextInfo.ToLower(name.Trim())
            .Replace("  ", " ").Replace("  ", " ").Trim();
    }

    private static BookingRequestDto ToDto(BookingRequest r, string? doctorName = null) => new(
        r.Id,
        r.PatientName,
        r.PhoneNumber,
        r.Email,
        r.ServiceType,
        r.PreferredDate,
        r.PreferredTime,
        r.Notes,
        r.Status.ToString(),
        r.StaffNotes,
        r.CreatedAt,
        r.ReviewedAt,
        r.DoctorId,
        doctorName,
        r.ConvertedToAppointmentId
    );
}
