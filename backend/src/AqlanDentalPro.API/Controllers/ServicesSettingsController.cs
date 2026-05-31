using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Manage clinic services catalog (admin CRUD, staff read-active).
/// </summary>
[ApiController]
[Route("api/settings/services")]
public class ServicesSettingsController(AppDbContext db) : ControllerBase
{
    /// <summary>Get all services (including inactive). Admin only.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? category)
    {
        var query = db.ClinicServices
            .IgnoreQueryFilters()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.Trim().ToLower();
            query = query.Where(s =>
                s.ArabicName.ToLower().Contains(lowerSearch) ||
                s.EnglishName.ToLower().Contains(lowerSearch) ||
                s.Code.ToLower().Contains(lowerSearch));
        }

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ServiceCategory>(category, true, out var cat))
        {
            query = query.Where(s => s.Category == cat);
        }

        var services = await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ArabicName)
            .Select(s => MapToDto(s))
            .ToListAsync();

        return Ok(services);
    }

    /// <summary>Get active services only. Available to all staff for appointment/booking forms.</summary>
    [HttpGet("active")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetActive([FromQuery] string? category)
    {
        var query = db.ClinicServices.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ServiceCategory>(category, true, out var cat))
        {
            query = query.Where(s => s.Category == cat);
        }

        var services = await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ArabicName)
            .Select(s => MapToDto(s))
            .ToListAsync();

        return Ok(services);
    }

    /// <summary>Create a new service. Admin only.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateClinicServiceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ArabicName))
            return BadRequest(new { message = "اسم الخدمة بالعربية مطلوب" });

        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { message = "كود الخدمة مطلوب" });

        // Check duplicate code
        var codeExists = await db.ClinicServices
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Code == req.Code);
        if (codeExists)
            return Conflict(new { message = "كود الخدمة مستخدم بالفعل" });

        var service = new ClinicService
        {
            ArabicName = req.ArabicName.Trim(),
            EnglishName = req.EnglishName?.Trim() ?? "",
            Code = req.Code.Trim(),
            Department = req.Department?.Trim(),
            Category = req.Category ?? ServiceCategory.Other,
            Description = req.Description?.Trim(),
            DefaultDurationMinutes = req.DefaultDurationMinutes ?? 30,
            DefaultPrice = req.DefaultPrice ?? 0,
            RequiresDoctor = req.RequiresDoctor ?? true,
            RequiresConsultationFee = req.RequiresConsultationFee ?? false,
            ShowInBooking = req.ShowInBooking ?? true,
            ShowInReception = req.ShowInReception ?? true,
            ShowInTreatmentPlan = req.ShowInTreatmentPlan ?? true,
            SortOrder = req.SortOrder ?? 0,
            IsActive = true
        };

        db.ClinicServices.Add(service);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = service.Id }, MapToDto(service));
    }

    /// <summary>Update an existing service. Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClinicServiceRequest req)
    {
        var service = await db.ClinicServices.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
            return NotFound(new { message = "الخدمة غير موجودة" });

        if (req.ArabicName != null) service.ArabicName = req.ArabicName.Trim();
        if (req.EnglishName != null) service.EnglishName = req.EnglishName.Trim();
        if (req.Code != null)
        {
            var codeExists = await db.ClinicServices
                .IgnoreQueryFilters()
                .AnyAsync(s => s.Code == req.Code && s.Id != id);
            if (codeExists)
                return Conflict(new { message = "كود الخدمة مستخدم بالفعل" });
            service.Code = req.Code.Trim();
        }
        if (req.Department != null) service.Department = req.Department.Trim();
        if (req.Category != null) service.Category = req.Category.Value;
        if (req.Description != null) service.Description = req.Description.Trim();
        if (req.DefaultDurationMinutes != null) service.DefaultDurationMinutes = req.DefaultDurationMinutes.Value;
        if (req.DefaultPrice != null) service.DefaultPrice = req.DefaultPrice.Value;
        if (req.RequiresDoctor != null) service.RequiresDoctor = req.RequiresDoctor.Value;
        if (req.RequiresConsultationFee != null) service.RequiresConsultationFee = req.RequiresConsultationFee.Value;
        if (req.ShowInBooking != null) service.ShowInBooking = req.ShowInBooking.Value;
        if (req.ShowInReception != null) service.ShowInReception = req.ShowInReception.Value;
        if (req.ShowInTreatmentPlan != null) service.ShowInTreatmentPlan = req.ShowInTreatmentPlan.Value;
        if (req.SortOrder != null) service.SortOrder = req.SortOrder.Value;

        await db.SaveChangesAsync();
        return Ok(MapToDto(service));
    }

    /// <summary>Activate a service. Admin only.</summary>
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var service = await db.ClinicServices.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
            return NotFound(new { message = "الخدمة غير موجودة" });

        if (service.IsActive)
            return BadRequest(new { message = "الخدمة مفعلة بالفعل" });

        service.IsActive = true;
        service.DeletedAt = null;
        service.DeletedBy = null;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تفعيل الخدمة بنجاح" });
    }

    /// <summary>Deactivate a service (soft-delete). Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var service = await db.ClinicServices.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
            return NotFound(new { message = "الخدمة غير موجودة" });

        if (!service.IsActive)
            return BadRequest(new { message = "الخدمة معطلة بالفعل" });

        service.IsActive = false;
        service.DeletedAt = DateTime.UtcNow;
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        service.DeletedBy = Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تعطيل الخدمة بنجاح" });
    }

    private static object MapToDto(ClinicService s) => new
    {
        s.Id,
        s.ArabicName,
        s.EnglishName,
        s.Code,
        s.Department,
        Category = s.Category.ToString(),
        s.Description,
        s.DefaultDurationMinutes,
        s.DefaultPrice,
        s.RequiresDoctor,
        s.RequiresConsultationFee,
        s.ShowInBooking,
        s.ShowInReception,
        s.ShowInTreatmentPlan,
        s.IsActive,
        s.SortOrder,
        s.CreatedAt,
        s.UpdatedAt
    };
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class CreateClinicServiceRequest
{
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Department { get; set; }
    public ServiceCategory? Category { get; set; }
    public string? Description { get; set; }
    public int? DefaultDurationMinutes { get; set; }
    public decimal? DefaultPrice { get; set; }
    public bool? RequiresDoctor { get; set; }
    public bool? RequiresConsultationFee { get; set; }
    public bool? ShowInBooking { get; set; }
    public bool? ShowInReception { get; set; }
    public bool? ShowInTreatmentPlan { get; set; }
    public int? SortOrder { get; set; }
}

public class UpdateClinicServiceRequest
{
    public string? ArabicName { get; set; }
    public string? EnglishName { get; set; }
    public string? Code { get; set; }
    public string? Department { get; set; }
    public ServiceCategory? Category { get; set; }
    public string? Description { get; set; }
    public int? DefaultDurationMinutes { get; set; }
    public decimal? DefaultPrice { get; set; }
    public bool? RequiresDoctor { get; set; }
    public bool? RequiresConsultationFee { get; set; }
    public bool? ShowInBooking { get; set; }
    public bool? ShowInReception { get; set; }
    public bool? ShowInTreatmentPlan { get; set; }
    public int? SortOrder { get; set; }
}
