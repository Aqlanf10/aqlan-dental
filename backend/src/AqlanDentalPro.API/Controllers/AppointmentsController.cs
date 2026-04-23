using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController(AppointmentService service) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IActionResult> GetToday([FromQuery] Guid? doctorId)
    {
        var list = await service.GetTodayAsync(doctorId);
        return Ok(list);
    }

    [HttpGet]
    public async Task<IActionResult> GetByRange(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] Guid? doctorId)
    {
        var fromDate = from != null ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today);
        var toDate = to != null ? DateOnly.Parse(to) : fromDate;

        var list = await service.GetByDateRangeAsync(fromDate, toDate, doctorId);
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create([FromBody] CreateAppointmentRequest req)
    {
        var (result, error) = await service.CreateAsync(req);
        if (error != null)
            return Conflict(new { message = error });
        return CreatedAtAction(nameof(GetToday), result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppointmentStatusRequest req)
    {
        var (result, error) = await service.UpdateStatusAsync(id, req.Status);
        if (error != null) return BadRequest(new { message = error });
        return result == null ? NotFound() : Ok(result);
    }
}
