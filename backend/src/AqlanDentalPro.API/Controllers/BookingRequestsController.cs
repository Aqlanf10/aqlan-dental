using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
public class BookingRequestsController(IBookingRequestService service, ICurrentUserService currentUser) : ControllerBase
{
    // ── Public endpoints ─────────────────────────────────────────────────

    /// <summary>
    /// Get available time slots for a given date. Public, no auth required.
    /// </summary>
    [HttpGet("api/public/booking-availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability([FromQuery] string date, [FromQuery] string? serviceType)
    {
        if (string.IsNullOrWhiteSpace(date))
            return BadRequest(new { message = "التاريخ مطلوب" });

        var result = await service.GetAvailabilityAsync(date, serviceType);

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
    public async Task<IActionResult> Create([FromBody] CreateBookingRequestDto dto)
    {
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
}
