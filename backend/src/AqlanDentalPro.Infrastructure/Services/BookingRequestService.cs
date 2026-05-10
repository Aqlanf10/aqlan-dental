using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public class BookingRequestService(AppDbContext db) : IBookingRequestService
{
    public async Task<BookingRequestDto> CreateAsync(CreateBookingRequestDto dto)
    {
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
