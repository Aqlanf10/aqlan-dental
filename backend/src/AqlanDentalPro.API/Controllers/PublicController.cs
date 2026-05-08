using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicController(AppDbContext db) => _db = db;

    [HttpPost("public/booking-requests")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBookingRequest([FromBody] CreateBookingRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PatientName) || string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return BadRequest(new { message = "الاسم ورقم الهاتف مطلوبان" });

        var request = new BookingRequest
        {
            PatientName = dto.PatientName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Email = dto.Email?.Trim(),
            ServiceType = dto.ServiceType,
            PreferredDate = dto.PreferredDate,
            PreferredTime = dto.PreferredTime,
            Notes = dto.Notes?.Trim(),
        };

        _db.BookingRequests.Add(request);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { id = request.Id, message = "تم استلام طلبك بنجاح" });
    }
}

public record CreateBookingRequestDto(
    string PatientName,
    string PhoneNumber,
    string? Email,
    string? ServiceType,
    DateOnly? PreferredDate,
    string? PreferredTime,
    string? Notes
);
