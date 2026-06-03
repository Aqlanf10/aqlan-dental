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

public sealed class CreateLabWorkTypeRequest
{
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? Category { get; init; }
    public int SortOrder { get; init; }
}

public sealed class CreateLabWorkTypeRequestValidator : AbstractValidator<CreateLabWorkTypeRequest>
{
    public CreateLabWorkTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم نوع العمل مطلوب").MaximumLength(100);
        RuleFor(x => x.NameAr).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.NameAr));
        RuleFor(x => x.Category).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Category));
    }
}

public sealed class UpdateLabWorkTypeRequest
{
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? Category { get; init; }
    public int SortOrder { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class UpdateLabWorkTypeRequestValidator : AbstractValidator<UpdateLabWorkTypeRequest>
{
    public UpdateLabWorkTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم نوع العمل مطلوب").MaximumLength(100);
        RuleFor(x => x.NameAr).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.NameAr));
        RuleFor(x => x.Category).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Category));
    }
}

// ─── Controller ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/lab-work-types")]
[Authorize(Policy = "StaffOnly")]
public class LabWorkTypesController(AppDbContext db, ICurrentUserService currentUser, ILogger<LabWorkTypesController> logger) : ControllerBase
{
    private Task<bool> CanAsync(string action) => PermissionGuard.HasAsync(db, currentUser, "lab_work_types", action);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = true)
    {
        if (!await CanAsync("view")) return Forbid();

        var query = db.LabWorkTypes.AsQueryable();
        if (activeOnly == true)
            query = query.Where(w => w.IsActive);

        var types = await query
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Name)
            .Select(w => new
            {
                w.Id,
                w.Name,
                w.NameAr,
                w.Category,
                w.SortOrder,
                w.IsActive,
                CreatedAt = w.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = w.UpdatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = types });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        var workType = await db.LabWorkTypes.FindAsync(id);
        if (workType is null) return NotFound(new { message = "نوع العمل غير موجود" });

        return Ok(new
        {
            workType.Id,
            workType.Name,
            workType.NameAr,
            workType.Category,
            workType.SortOrder,
            workType.IsActive,
            CreatedAt = workType.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = workType.UpdatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLabWorkTypeRequest req)
    {
        if (!await CanAsync("create")) return Forbid();

        var workType = new LabWorkType
        {
            Name = req.Name,
            NameAr = req.NameAr,
            Category = req.Category,
            SortOrder = req.SortOrder,
        };

        db.LabWorkTypes.Add(workType);
        await db.SaveChangesAsync();

        logger.LogInformation("LabWorkType created: {Id} — {Name}", workType.Id, workType.Name);

        return CreatedAtAction(nameof(GetById), new { id = workType.Id }, new
        {
            workType.Id,
            workType.Name,
            workType.SortOrder
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLabWorkTypeRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        var workType = await db.LabWorkTypes.FindAsync(id);
        if (workType is null) return NotFound(new { message = "نوع العمل غير موجود" });

        workType.Name = req.Name;
        workType.NameAr = req.NameAr;
        workType.Category = req.Category;
        workType.SortOrder = req.SortOrder;
        if (req.IsActive.HasValue) workType.IsActive = req.IsActive.Value;

        await db.SaveChangesAsync();

        logger.LogInformation("LabWorkType updated: {Id} — {Name}", workType.Id, workType.Name);

        return Ok(new
        {
            workType.Id,
            workType.Name,
            workType.SortOrder
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await CanAsync("delete")) return Forbid();

        var workType = await db.LabWorkTypes.FindAsync(id);
        if (workType is null) return NotFound(new { message = "نوع العمل غير موجود" });

        workType.IsActive = false;
        await db.SaveChangesAsync();

        logger.LogInformation("LabWorkType soft-deleted: {Id} — {Name}", workType.Id, workType.Name);

        return Ok(new { message = "تم حذف نوع العمل بنجاح" });
    }
}
