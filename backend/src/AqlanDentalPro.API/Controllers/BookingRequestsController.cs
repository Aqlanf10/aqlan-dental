using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
public class BookingRequestsController(IBookingRequestService service, ICurrentUserService currentUser) : ControllerBase
{
    // Public endpoint — no authentication required
    [HttpPost("api/public/booking-requests")]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await service.CreateAsync(dto);
        return Created(string.Empty, result);
    }

    // Staff endpoints
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
