using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.API.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
public class BookingRequestsController(IBookingRequestService service, ICurrentUserService currentUser, IRecaptchaService recaptcha, IServiceScopeFactory scopeFactory, ILogger<BookingRequestsController> logger) : ControllerBase
{
    // ── Public endpoints ─────────────────────────────────────────────────

    /// <summary>
    /// Get available time slots for a given date. Public, no auth required.
    /// </summary>
    [HttpGet("api/public/booking-availability")]
    [AllowAnonymous]
    [EnableCors("AllowPublicApi")]
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
    [EnableCors("AllowPublicApi")]
    [EnableRateLimiting("BookingPolicy")]
    [Honeypot]
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
            logger.LogWarning(ex, "Booking slot no longer available");
            return Conflict(new { message = ex.Message });
        }
        catch (DuplicateBookingRequestException ex)
        {
            logger.LogWarning(ex, "Duplicate booking request detected");
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid booking request argument");
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Staff endpoints ──────────────────────────────────────────────────

    [HttpGet("api/booking-requests")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var result = await service.GetAllPaginatedAsync(status, page, pageSize);
        return Ok(result);
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
        if (result == null) return NotFound();

        // Send WhatsApp notification for Confirmed/Rejected statuses
        if (dto.Status is "Confirmed" or "Rejected")
        {
            var phoneNumber = result.PhoneNumber;
            var patientName = result.PatientName;
            var statusArabic = dto.Status == "Confirmed" ? "تم تأكيد" : "تم رفض";
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var whatsappService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                    await whatsappService.SendMessageAsync(new SendMessageRequest
                    {
                        PatientId = Guid.Empty, // Booking request may not have a patient ID
                        TemplateType = dto.Status == "Confirmed" ? "booking_confirmed" : "booking_rejected",
                        CustomMessage = $"عزيزي/عزيزتي {patientName}، {statusArabic} طلب الحجز الخاص بك",
                        Parameters = new Dictionary<string, string>
                        {
                            ["patientName"] = patientName,
                            ["status"] = statusArabic
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send WhatsApp notification for booking request {BookingRequestId} status change to {Status}", id, dto.Status);
                }
            });
        }

        return Ok(result);
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
            logger.LogWarning(ex, "Invalid argument converting booking request {BookingRequestId} to appointment", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a booking request from the public booking page.
    /// Requires phoneNumber for verification. Only works for Pending or Reviewed bookings.
    /// </summary>
    [HttpPost("api/public/booking-requests/{id:guid}/cancel")]
    [AllowAnonymous]
    [EnableCors("AllowPublicApi")]
    [EnableRateLimiting("BookingPolicy")]
    public async Task<IActionResult> CancelPublicBooking(Guid id, [FromBody] CancelPublicBookingRequest dto)
    {
        var booking = await service.GetByIdAsync(id);
        if (booking == null)
            return NotFound(new { message = "طلب الحجز غير موجود" });

        // Verify phone number matches
        var normalizedRequestPhone = dto.PhoneNumber?.Trim() ?? "";
        var normalizedBookingPhone = booking.PhoneNumber?.Trim() ?? "";
        if (!string.Equals(normalizedRequestPhone, normalizedBookingPhone, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "رقم الهاتف غير متطابق" });

        // Only Pending or Reviewed can be cancelled by patient
        if (booking.Status is not "Pending" and not "Reviewed")
            return BadRequest(new { message = "لا يمكن إلغاء هذا الطلب. حالة الطلب: " + booking.Status });

        var updateDto = new UpdateBookingRequestStatusDto("Rejected", "إلغاء من قبل المريض");

        // Use a system user ID for public cancellations (no authenticated user)
        var systemUserId = Guid.Empty;
        var result = await service.UpdateStatusAsync(id, updateDto, systemUserId);
        if (result == null)
            return NotFound(new { message = "طلب الحجز غير موجود" });

        return Ok(new { message = "تم إلغاء طلب الحجز بنجاح" });
    }
}
