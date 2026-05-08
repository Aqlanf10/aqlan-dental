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
    [MaxLength(500)] string? Notes
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
    DateTime? ReviewedAt
);

public record UpdateBookingRequestStatusDto(
    [Required] string Status,
    string? StaffNotes
);
