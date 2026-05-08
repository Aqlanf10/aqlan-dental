using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/booking-requests")]
[Authorize]
public class BookingRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BookingRequestsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.BookingRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);
        query = query.OrderByDescending(r => r.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { data = items, total, page, pageSize });
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var req = await _db.BookingRequests.FindAsync(id);
        if (req is null) return NotFound();
        req.Status = dto.Status;
        req.StaffNotes = dto.StaffNotes;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(req);
    }
}

public record UpdateStatusDto(string Status, string? StaffNotes);
