using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Manage clinic rooms (admin CRUD, staff read-active for queue/appointments).
/// </summary>
[ApiController]
[Route("api/settings/rooms")]
public class RoomsSettingsController(AppDbContext db) : ControllerBase
{
    /// <summary>Get all rooms (including inactive). Admin only.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll()
    {
        var rooms = await db.ClinicRooms
            .IgnoreQueryFilters()
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.ArabicName)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return Ok(rooms);
    }

    /// <summary>Get active rooms only. Available to all staff for queue/appointment forms.</summary>
    [HttpGet("active")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetActive()
    {
        var rooms = await db.ClinicRooms
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.ArabicName)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return Ok(rooms);
    }

    /// <summary>Create a new room. Admin only.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateClinicRoomRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ArabicName))
            return BadRequest(new { message = "اسم الغرفة بالعربية مطلوب" });

        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { message = "كود الغرفة مطلوب" });

        var codeExists = await db.ClinicRooms
            .IgnoreQueryFilters()
            .AnyAsync(r => r.Code == req.Code);
        if (codeExists)
            return Conflict(new { message = "كود الغرفة مستخدم بالفعل" });

        var room = new ClinicRoom
        {
            ArabicName = req.ArabicName.Trim(),
            EnglishName = req.EnglishName?.Trim() ?? "",
            Code = req.Code.Trim(),
            RoomType = req.RoomType ?? RoomType.Treatment,
            SortOrder = req.SortOrder ?? 0,
            IsActive = true
        };

        db.ClinicRooms.Add(room);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = room.Id }, MapToDto(room));
    }

    /// <summary>Update an existing room. Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClinicRoomRequest req)
    {
        var room = await db.ClinicRooms.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
        if (room == null)
            return NotFound(new { message = "الغرفة غير موجودة" });

        if (req.ArabicName != null) room.ArabicName = req.ArabicName.Trim();
        if (req.EnglishName != null) room.EnglishName = req.EnglishName.Trim();
        if (req.Code != null)
        {
            var codeExists = await db.ClinicRooms
                .IgnoreQueryFilters()
                .AnyAsync(r => r.Code == req.Code && r.Id != id);
            if (codeExists)
                return Conflict(new { message = "كود الغرفة مستخدم بالفعل" });
            room.Code = req.Code.Trim();
        }
        if (req.RoomType != null) room.RoomType = req.RoomType.Value;
        if (req.SortOrder != null) room.SortOrder = req.SortOrder.Value;

        await db.SaveChangesAsync();
        return Ok(MapToDto(room));
    }

    /// <summary>Activate a room. Admin only.</summary>
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var room = await db.ClinicRooms.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
        if (room == null)
            return NotFound(new { message = "الغرفة غير موجودة" });

        if (room.IsActive)
            return BadRequest(new { message = "الغرفة مفعلة بالفعل" });

        room.IsActive = true;
        room.DeletedAt = null;
        room.DeletedBy = null;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تفعيل الغرفة بنجاح" });
    }

    /// <summary>Deactivate a room (soft-delete). Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var room = await db.ClinicRooms.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
        if (room == null)
            return NotFound(new { message = "الغرفة غير موجودة" });

        if (!room.IsActive)
            return BadRequest(new { message = "الغرفة معطلة بالفعل" });

        room.IsActive = false;
        room.DeletedAt = DateTime.UtcNow;
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        room.DeletedBy = Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تعطيل الغرفة بنجاح" });
    }

    private static object MapToDto(ClinicRoom r) => new
    {
        r.Id,
        Name = !string.IsNullOrWhiteSpace(r.ArabicName) ? r.ArabicName : r.EnglishName,
        r.ArabicName,
        r.EnglishName,
        r.Code,
        RoomType = r.RoomType.ToString(),
        r.IsActive,
        r.SortOrder,
        r.CreatedAt,
        r.UpdatedAt
    };
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class CreateClinicRoomRequest
{
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string Code { get; set; } = string.Empty;
    public RoomType? RoomType { get; set; }
    public int? SortOrder { get; set; }
}

public class UpdateClinicRoomRequest
{
    public string? ArabicName { get; set; }
    public string? EnglishName { get; set; }
    public string? Code { get; set; }
    public RoomType? RoomType { get; set; }
    public int? SortOrder { get; set; }
}
