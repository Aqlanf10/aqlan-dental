using System.ComponentModel.DataAnnotations;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.DTOs.BookingRequests;

public record CreateBookingRequestDto(
    [Required, MaxLength(100)] string PatientName,
    [Required, MaxLength(20)] string PhoneNumber,
    [MaxLength(150)] string? Email,
    [MaxLength(100)] string? ServiceType,
    [MaxLength(50)] string? PreferredDate,
    [MaxLength(50)] string? PreferredTime,
    [MaxLength(500)] string? Notes,
    Guid? DoctorId,
    /// <summary>Google reCAPTCHA v3 token (optional — required when configured)</summary>
    string? RecaptchaToken
);

public record BookingRequestDto(
    Guid Id,
    string PatientName,
    string PhoneNumber,
    string? Email,
    string? ServiceType,
    string? PreferredDate,
    string? PreferredTime,
    string? Notes,
    string Status,
    string? StaffNotes,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    Guid? DoctorId,
    string? DoctorName,
    Guid? ConvertedToAppointmentId
);

public record UpdateBookingRequestStatusDto(
    [Required] string Status,
    string? StaffNotes
);

// ── Public booking availability ──────────────────────────────────────────

public record BookingAvailabilitySlotDto(
    string Time,
    bool Available,
    string? Reason = null
);

public record BookingAvailabilityResponseDto(
    string Date,
    string? ServiceType,
    List<BookingAvailabilitySlotDto> Slots,
    bool IsClosed = false,
    string? Message = null,
    Guid? DoctorId = null,
    string? DoctorName = null
);

public record ConvertBookingRequestToAppointmentDto(
    [Required] Guid PatientId,
    [Required] Guid DoctorId,
    [Required] DateOnly AppointmentDate,
    [Required] TimeOnly StartTime,
    [Required] TimeOnly EndTime,
    string? AppointmentType,
    int DurationMinutes = 30
);

public record CancelPublicBookingRequest(
    [Required] string PhoneNumber
);
