using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Manage clinic services catalog (admin CRUD, staff read-active).
/// </summary>
[ApiController]
[Route("api/settings/services")]
public class ServicesSettingsController(AppDbContext db, FinanceSettingsReader financeSettings) : ControllerBase
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

        // FIN-SETTINGS: when the caller doesn't specify a price/commission field,
        // default it from the finance.* settings (clinic-owner-configurable). When
        // the caller DOES specify a value, that wins — existing behavior preserved.
        var defaultPrice = req.DefaultPrice
            ?? await financeSettings.GetDecimalAsync(FinanceSettingsKeys.DefaultConsultationFee);
        var defaultDoctorPct = req.DefaultDoctorCommissionPercentage
            ?? await financeSettings.GetDecimalAsync(FinanceSettingsKeys.CommissionDefaultDoctorPercentage);
        var defaultRecognitionMode = req.CommissionRecognitionMode
            ?? await financeSettings.GetEnumAsync(FinanceSettingsKeys.CommissionDefaultRecognitionMode, CommissionRecognitionMode.OnPaymentCollection);
        var defaultBaseRule = req.CommissionBaseRule
            ?? await financeSettings.GetEnumAsync(FinanceSettingsKeys.CommissionDefaultBaseRule, CommissionBaseRule.AfterDiscountAndCosts);

        // YOLO-S2: Color (optional hex string for calendar/queue display).
        // Normalize to trimmed lowercase hex or null; reject malformed values.
        var normalizedColor = NormalizeColor(req.Color);
        if (normalizedColor == "__invalid__")
            return BadRequest(new { message = "اللون يجب أن يكون بصيغة hex مثل #3b82f6" });

        var service = new ClinicService
        {
            ArabicName = req.ArabicName.Trim(),
            EnglishName = req.EnglishName?.Trim() ?? "",
            Code = req.Code.Trim(),
            Department = req.Department?.Trim(),
            Category = req.Category ?? ServiceCategory.Other,
            Description = req.Description?.Trim(),
            DefaultDurationMinutes = req.DefaultDurationMinutes ?? 30,
            DefaultPrice = defaultPrice,
            RequiresDoctor = req.RequiresDoctor ?? true,
            RequiresConsultationFee = req.RequiresConsultationFee ?? false,
            ShowInBooking = req.ShowInBooking ?? true,
            ShowInReception = req.ShowInReception ?? true,
            ShowInTreatmentPlan = req.ShowInTreatmentPlan ?? true,
            SortOrder = req.SortOrder ?? 0,
            Color = normalizedColor,
            DefaultMaterialCost = req.DefaultMaterialCost ?? 0,
            DefaultMaterialCostType = req.DefaultMaterialCostType ?? MaterialCostType.FixedAmount,
            DefaultLabCost = req.DefaultLabCost ?? 0,
            DefaultDoctorCommissionPercentage = defaultDoctorPct,
            CommissionBaseRule = defaultBaseRule,
            CommissionRecognitionMode = defaultRecognitionMode,
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

        // YOLO-S2: Color — nullable + hex validation. A null clears the color.
        if (req.Color != null)
        {
            var normalizedColor = NormalizeColor(req.Color);
            if (normalizedColor == "__invalid__")
                return BadRequest(new { message = "اللون يجب أن يكون بصيغة hex مثل #3b82f6" });
            service.Color = normalizedColor;
        }

        // Commission defaults — always update if provided
        if (req.DefaultMaterialCost != null) service.DefaultMaterialCost = req.DefaultMaterialCost.Value;
        if (req.DefaultMaterialCostType != null) service.DefaultMaterialCostType = req.DefaultMaterialCostType.Value;
        if (req.DefaultLabCost != null) service.DefaultLabCost = req.DefaultLabCost.Value;
        if (req.DefaultDoctorCommissionPercentage != null) service.DefaultDoctorCommissionPercentage = req.DefaultDoctorCommissionPercentage.Value;
        if (req.CommissionBaseRule != null) service.CommissionBaseRule = req.CommissionBaseRule.Value;
        if (req.CommissionRecognitionMode != null) service.CommissionRecognitionMode = req.CommissionRecognitionMode.Value;

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
        s.Color,
        s.DefaultMaterialCost,
        DefaultMaterialCostType = s.DefaultMaterialCostType.ToString(),
        s.DefaultLabCost,
        s.DefaultDoctorCommissionPercentage,
        CommissionBaseRule = s.CommissionBaseRule.ToString(),
        CommissionRecognitionMode = s.CommissionRecognitionMode.ToString(),
        s.CreatedAt,
        s.UpdatedAt
    };

    /// <summary>
    /// YOLO-S2: Normalize a hex color input. Accepts "#3b82f6", "3b82f6", or empty.
    /// Returns trimmed lowercase "#3b82f6" form, null when input is empty, or the
    /// sentinel "__invalid__" when the input is non-empty but not a valid 6-digit hex.
    /// Mirrors the validator regex used for TreatmentPackage.Color.
    /// </summary>
    private static string? NormalizeColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^#?[0-9A-Fa-f]{6}$"))
            return "__invalid__";
        return (trimmed.StartsWith("#") ? trimmed : "#" + trimmed).ToLowerInvariant();
    }
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
    /// <summary>YOLO-S2: optional hex color (e.g. "#3b82f6") for calendar/queue display.</summary>
    public string? Color { get; set; }
    // Commission defaults
    public decimal? DefaultMaterialCost { get; set; }
    public MaterialCostType? DefaultMaterialCostType { get; set; }
    public decimal? DefaultLabCost { get; set; }
    public decimal? DefaultDoctorCommissionPercentage { get; set; }
    public CommissionBaseRule? CommissionBaseRule { get; set; }
    public CommissionRecognitionMode? CommissionRecognitionMode { get; set; }
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
    /// <summary>YOLO-S2: optional hex color. Pass null to clear, empty string is treated as null.</summary>
    public string? Color { get; set; }
    // Commission defaults
    public decimal? DefaultMaterialCost { get; set; }
    public MaterialCostType? DefaultMaterialCostType { get; set; }
    public decimal? DefaultLabCost { get; set; }
    public decimal? DefaultDoctorCommissionPercentage { get; set; }
    public CommissionBaseRule? CommissionBaseRule { get; set; }
    public CommissionRecognitionMode? CommissionRecognitionMode { get; set; }
}
