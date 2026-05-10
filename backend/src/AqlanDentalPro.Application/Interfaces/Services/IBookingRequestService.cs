using AqlanDentalPro.Application.DTOs.BookingRequests;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IBookingRequestService
{
    Task<BookingRequestDto> CreateAsync(CreateBookingRequestDto dto);
    Task<List<BookingRequestDto>> GetAllAsync(string? statusFilter);
    Task<BookingRequestDto?> GetByIdAsync(Guid id);
    Task<BookingRequestDto?> UpdateStatusAsync(Guid id, UpdateBookingRequestStatusDto dto, Guid reviewedBy);
    Task<BookingAvailabilityResponseDto> GetAvailabilityAsync(string date, string? serviceType, Guid? doctorId = null);
    Task<bool> IsSlotAvailableAsync(string date, string time, Guid? doctorId = null);
    Task<BookingRequestDto?> ConvertToAppointmentAsync(Guid bookingRequestId, ConvertBookingRequestToAppointmentDto dto, Guid convertedBy);
}
