using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
public class BookingRequestsController(IBookingRequestService service, ICurrentUserService currentUser, IRecaptchaService recaptcha) : ControllerBase
{
    // ── Public endpoints ─────────────────────────────────────────────────

    /// <summary>
    /// Get available time slots for a given date. Public, no auth required.
    /// </summary>
    [HttpGet("api/public/booking-availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability([FromQuery] string date, [FromQuery] string? serviceType, [FromQuery] Guid? doctorId)
    {
        if (string.IsNullOrWhiteSpace(date))
            return BadRequest(new { message = "التاريخ مطلوب" });

        var result = await service.GetAvailabilityAsync(date, serviceType, doctorId);

        // If there's a message and no slots (past date / invalid), return 400
        if (result.Message != null && result.Slots.Count == 0 && !result.IsClosed)
            return BadRequest(new { message = result.Message });

        return Ok(result);
    }

    /// <summary>
    /// Submit a public booking request. No auth required.
    /// Returns 409 Conflict if the selected time slot is no longer available.
    /// </summary>
    [HttpPost("api/public/booking-requests")]
    [AllowAnonymous]
    [EnableRateLimiting("BookingPolicy")]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequestDto dto)
    {
        // reCAPTCHA validation
        if (!string.IsNullOrWhiteSpace(dto.RecaptchaToken))
        {
            var (isValid, score, errorMessage) = await recaptcha.ValidateTokenAsync(dto.RecaptchaToken);
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage ?? "فشل التحقق الأمني" });
            }
        }

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await service.CreateAsync(dto);
            return Created(string.Empty, result);
        }
        catch (SlotNotAvailableException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DuplicateBookingRequestException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Staff endpoints ──────────────────────────────────────────────────

    [HttpGet("api/booking-requests")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var items = await service.GetAllAsync(status);
        return Ok(items);
    }

    [HttpGet("api/booking-requests/{id:guid}")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await service.GetByIdAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPatch("api/booking-requests/{id:guid}/status")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateBookingRequestStatusDto dto)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await service.UpdateStatusAsync(id, dto, userId.Value);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Convert a confirmed booking request to an appointment.
    /// </summary>
    [HttpPost("api/booking-requests/{id:guid}/convert-to-appointment")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> ConvertToAppointment(Guid id, [FromBody] ConvertBookingRequestToAppointmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await service.ConvertToAppointmentAsync(id, dto, userId.Value);
            if (result == null)
                return NotFound(new { message = "طلب الحجز غير موجود" });

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
