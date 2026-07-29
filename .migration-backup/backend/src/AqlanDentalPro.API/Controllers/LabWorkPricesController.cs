using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

// ─── DTOs ──────────────────────────────────────────────────────────────────────

public sealed class CreateLabWorkPriceRequest
{
    public Guid LabId { get; init; }
    public Guid WorkTypeId { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal? UrgentSurcharge { get; init; }
    public string? UrgentSurchargeType { get; init; } // "fixed" or "percentage"
    public int? EstimatedDays { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateLabWorkPriceRequestValidator : AbstractValidator<CreateLabWorkPriceRequest>
{
    private static readonly HashSet<string> ValidSurchargeTypes = ["fixed", "percentage"];

    public CreateLabWorkPriceRequestValidator()
    {
        RuleFor(x => x.LabId).NotEmpty().WithMessage("المعمل مطلوب");
        RuleFor(x => x.WorkTypeId).NotEmpty().WithMessage("نوع العمل مطلوب");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("السعر يجب أن يكون صفراً أو أكثر");
        RuleFor(x => x.UrgentSurcharge).GreaterThanOrEqualTo(0).WithMessage("الإضافة المستعجلة يجب أن تكون صفراً أو أكثر")
            .When(x => x.UrgentSurcharge.HasValue);
        RuleFor(x => x.UrgentSurchargeType)
            .Must(t => ValidSurchargeTypes.Contains(t!)).WithMessage("نوع الإضافة المستعجلة غير صالح")
            .When(x => !string.IsNullOrEmpty(x.UrgentSurchargeType));
        RuleFor(x => x.EstimatedDays).GreaterThanOrEqualTo(0).When(x => x.EstimatedDays.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public sealed class UpdateLabWorkPriceRequest
{
    public decimal UnitPrice { get; init; }
    public decimal? UrgentSurcharge { get; init; }
    public string? UrgentSurchargeType { get; init; }
    public int? EstimatedDays { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateLabWorkPriceRequestValidator : AbstractValidator<UpdateLabWorkPriceRequest>
{
    public UpdateLabWorkPriceRequestValidator()
    {
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("السعر يجب أن يكون صفراً أو أكثر");
        RuleFor(x => x.UrgentSurcharge).GreaterThanOrEqualTo(0).When(x => x.UrgentSurcharge.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

// ─── Controller ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/lab-work-prices")]
[Authorize(Policy = "StaffOnly")]
public class LabWorkPricesController(AppDbContext db, ICurrentUserService currentUser, ILogger<LabWorkPricesController> logger) : ControllerBase
{
    private Task<bool> CanAsync(string action) => PermissionGuard.HasAsync(db, currentUser, "lab_work_prices", action);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? labId, [FromQuery] Guid? workTypeId)
    {
        if (!await CanAsync("view")) return Forbid();

        var query = db.LabWorkPrices
            .Include(p => p.Lab)
            .Include(p => p.WorkType)
            .AsQueryable();

        if (labId.HasValue) query = query.Where(p => p.LabId == labId.Value);
        if (workTypeId.HasValue) query = query.Where(p => p.WorkTypeId == workTypeId.Value);

        var prices = await query
            .OrderBy(p => p.Lab.Name).ThenBy(p => p.WorkType.SortOrder)
            .Select(p => new
            {
                p.Id,
                p.LabId,
                LabName = p.Lab.Name,
                p.WorkTypeId,
                WorkTypeName = p.WorkType.Name,
                WorkTypeNameAr = p.WorkType.NameAr,
                p.UnitPrice,
                p.UrgentSurcharge,
                p.UrgentSurchargeType,
                p.EstimatedDays,
                p.Notes,
                p.IsActive,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = prices });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        var price = await db.LabWorkPrices
            .Include(p => p.Lab)
            .Include(p => p.WorkType)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (price is null) return NotFound(new { message = "سعر العمل غير موجود" });

        return Ok(new
        {
            price.Id,
            price.LabId,
            LabName = price.Lab.Name,
            price.WorkTypeId,
            WorkTypeName = price.WorkType.Name,
            price.UnitPrice,
            price.UrgentSurcharge,
            price.UrgentSurchargeType,
            price.EstimatedDays,
            price.Notes,
            price.IsActive,
            CreatedAt = price.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = price.UpdatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLabWorkPriceRequest req)
    {
        if (!await CanAsync("create")) return Forbid();

        // Check for duplicate
        var exists = await db.LabWorkPrices
            .AnyAsync(p => p.LabId == req.LabId && p.WorkTypeId == req.WorkTypeId);
        if (exists)
            return BadRequest(new { message = "يوجد سعر مسجل مسبقاً لهذا المعمل ونوع العمل" });

        var lab = await db.Labs.FindAsync(req.LabId);
        if (lab is null) return BadRequest(new { message = "المعمل غير موجود" });

        var workType = await db.LabWorkTypes.FindAsync(req.WorkTypeId);
        if (workType is null) return BadRequest(new { message = "نوع العمل غير موجود" });

        var price = new LabWorkPrice
        {
            LabId = req.LabId,
            WorkTypeId = req.WorkTypeId,
            UnitPrice = req.UnitPrice,
            UrgentSurcharge = req.UrgentSurcharge,
            UrgentSurchargeType = req.UrgentSurchargeType,
            EstimatedDays = req.EstimatedDays,
            Notes = req.Notes,
        };

        db.LabWorkPrices.Add(price);
        await db.SaveChangesAsync();

        logger.LogInformation("LabWorkPrice created: {Id} — Lab:{LabId} Type:{WorkTypeId}", price.Id, req.LabId, req.WorkTypeId);

        return CreatedAtAction(nameof(GetById), new { id = price.Id }, new { price.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLabWorkPriceRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        var price = await db.LabWorkPrices.FindAsync(id);
        if (price is null) return NotFound(new { message = "سعر العمل غير موجود" });

        price.UnitPrice = req.UnitPrice;
        price.UrgentSurcharge = req.UrgentSurcharge;
        price.UrgentSurchargeType = req.UrgentSurchargeType;
        price.EstimatedDays = req.EstimatedDays;
        price.Notes = req.Notes;

        await db.SaveChangesAsync();

        return Ok(new { price.Id, price.UnitPrice });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await CanAsync("delete")) return Forbid();

        var price = await db.LabWorkPrices.FindAsync(id);
        if (price is null) return NotFound(new { message = "سعر العمل غير موجود" });

        price.IsActive = false;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف السعر بنجاح" });
    }
}
