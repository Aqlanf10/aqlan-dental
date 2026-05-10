using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.Exceptions;
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
    private const int SlotDurationMinutes = 30;

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
        // Race condition protection: re-check slot availability if date+time provided
        if (!string.IsNullOrWhiteSpace(dto.PreferredDate) && !string.IsNullOrWhiteSpace(dto.PreferredTime))
        {
            if (!await IsSlotAvailableAsync(dto.PreferredDate, dto.PreferredTime))
            {
                throw new SlotNotAvailableException("هذا الموعد لم يعد متاحًا، يرجى اختيار وقت آخر.");
            }
        }

        var entity = new BookingRequest
        {
            PatientName = dto.PatientName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Email = dto.Email?.Trim(),
            ServiceType = dto.ServiceType?.Trim(),
            PreferredDate = dto.PreferredDate?.Trim(),
            PreferredTime = dto.PreferredTime?.Trim(),
            Notes = dto.Notes?.Trim()
        };

        db.BookingRequests.Add(entity);
        await db.SaveChangesAsync();
        return ToDto(entity);
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
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return items.Select(ToDto).ToList();
    }

    public async Task<BookingRequestDto?> GetByIdAsync(Guid id)
    {
        var entity = await db.BookingRequests.FindAsync(id);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<BookingRequestDto?> UpdateStatusAsync(Guid id, UpdateBookingRequestStatusDto dto, Guid reviewedBy)
    {
        var entity = await db.BookingRequests.FindAsync(id);
        if (entity == null) return null;

        if (!Enum.TryParse<BookingRequestStatus>(dto.Status, ignoreCase: true, out var status))
            return null;

        entity.Status = status;
        entity.StaffNotes = dto.StaffNotes?.Trim();
        entity.ReviewedBy = reviewedBy;
        entity.ReviewedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<BookingAvailabilityResponseDto> GetAvailabilityAsync(string date, string? serviceType)
    {
        // Parse and validate date
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false, "صيغة التاريخ غير صحيحة");
        }

        // Check for past date
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (parsedDate < today)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false, "لا يمكن اختيار تاريخ سابق");
        }

        // Check for Friday (DayOfWeek.Friday = 5 in .NET)
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], true, "المركز مغلق يوم الجمعة");
        }

        // Generate all possible time slots
        var slots = GenerateTimeSlots();

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
                     && r.PreferredTime != null)
            .Select(r => r.PreferredTime!)
            .ToListAsync();

        // Mark unavailable slots
        var result = new List<BookingAvailabilitySlotDto>();
        foreach (var slot in slots)
        {
            var slotTime = TimeOnly.Parse(slot);
            var slotEnd = slotTime.AddMinutes(SlotDurationMinutes);

            // Check if any appointment overlaps this slot
            var isBlockedByAppointment = appointmentTimes.Any(a =>
                a.StartTime < slotEnd && a.EndTime > slotTime);

            // Check if any booking request occupies this slot
            // Booking requests store time as Arabic format (e.g., "09:00") or 24h format
            var isBlockedByBookingRequest = bookingRequestTimes.Any(brTime =>
                IsSameSlotTime(brTime, slot));

            if (isBlockedByAppointment || isBlockedByBookingRequest)
            {
                result.Add(new BookingAvailabilitySlotDto(slot, false, "محجوز"));
            }
            else
            {
                result.Add(new BookingAvailabilitySlotDto(slot, true));
            }
        }

        return new BookingAvailabilityResponseDto(date, serviceType, result);
    }

    public async Task<bool> IsSlotAvailableAsync(string date, string time)
    {
        // Validate date
        if (!DateOnly.TryParse(date, out var parsedDate))
            return false;

        // Past date
        if (parsedDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return false;

        // Friday
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
            return false;

        // Find the matching 24h slot format
        var slot24h = NormalizeTo24h(time);
        if (slot24h == null)
            return true; // If we can't parse the time, don't block (backward compat)

        var slotTime = TimeOnly.Parse(slot24h);
        var slotEnd = slotTime.AddMinutes(SlotDurationMinutes);

        // Check appointments
        var hasAppointmentConflict = await db.Appointments
            .AnyAsync(a => a.AppointmentDate == parsedDate
                        && BlockingAppointmentStatuses.Contains(a.Status)
                        && a.StartTime < slotEnd
                        && a.EndTime > slotTime);

        if (hasAppointmentConflict)
            return false;

        // Check booking requests
        var hasBookingConflict = await db.BookingRequests
            .AnyAsync(r => r.PreferredDate == date
                        && BlockingStatuses.Contains(r.Status)
                        && r.PreferredTime != null
                        && IsSameSlotTime(r.PreferredTime, slot24h));

        return !hasBookingConflict;
    }

    /// <summary>
    /// Generates 30-minute time slots from 08:00 to 19:30 (last slot starts at 19:30, ends at 20:00).
    /// </summary>
    private static List<string> GenerateTimeSlots()
    {
        var slots = new List<string>();
        var current = ClinicOpen;
        while (current < ClinicClose)
        {
            slots.Add(current.ToString("HH:mm"));
            current = current.AddMinutes(SlotDurationMinutes);
        }
        return slots;
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

    private static BookingRequestDto ToDto(BookingRequest r) => new(
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
        r.ReviewedAt
    );
}
