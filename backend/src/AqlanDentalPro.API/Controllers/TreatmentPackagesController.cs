using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

// ─── Controller ─────────────────────────────────────────────────────────────────

/// <summary>
/// CRUD for TreatmentPackage catalog (باقات العلاج). Admin-only — the doctor
/// and reception read the active packages from the same GET endpoint (when
/// authorized at all, they can fetch activeOnly=true). Soft-delete pattern
/// matches the rest of the codebase (IsActive=false, not a hard DELETE).
/// YOLO-S1.
/// </summary>
[ApiController]
[Route("api/treatment-packages")]
[Authorize(Policy = "StaffOnly")]
public class TreatmentPackagesController(AppDbContext db, ILogger<TreatmentPackagesController> logger) : ControllerBase
{
    /// <summary>Get all packages. Pass activeOnly=true (default) to exclude soft-deleted.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = true)
    {
        var query = db.TreatmentPackages.AsQueryable();
        if (activeOnly == true)
            query = query.Where(p => p.IsActive);

        var items = await query
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .Select(p => new TreatmentPackageDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                TotalPrice = p.TotalPrice,
                SessionCount = p.SessionCount,
                Color = p.Color,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Get a single package by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var p = await db.TreatmentPackages.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound(new { message = "الباقة غير موجودة" });

        return Ok(new TreatmentPackageDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            TotalPrice = p.TotalPrice,
            SessionCount = p.SessionCount,
            Color = p.Color,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        });
    }

    /// <summary>Create a new package. Admin only.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateTreatmentPackageRequest req)
    {
        var validation = new CreateTreatmentPackageRequestValidator();
        var validationResult = await validation.ValidateAsync(req);
        if (!validationResult.IsValid)
            return BadRequest(new { message = string.Join(" · ", validationResult.Errors.Select(e => e.ErrorMessage)) });

        var pkg = new TreatmentPackage
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            TotalPrice = req.TotalPrice,
            SessionCount = req.SessionCount > 0 ? req.SessionCount : 1,
            Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim(),
            IsActive = req.IsActive,
        };

        db.TreatmentPackages.Add(pkg);
        await db.SaveChangesAsync();

        logger.LogInformation("TreatmentPackage created: {Id} — {Name}", pkg.Id, pkg.Name);

        return CreatedAtAction(nameof(GetById), new { id = pkg.Id }, new TreatmentPackageDto
        {
            Id = pkg.Id,
            Name = pkg.Name,
            Description = pkg.Description,
            TotalPrice = pkg.TotalPrice,
            SessionCount = pkg.SessionCount,
            Color = pkg.Color,
            IsActive = pkg.IsActive,
            CreatedAt = pkg.CreatedAt,
            UpdatedAt = pkg.UpdatedAt,
        });
    }

    /// <summary>Update an existing package. Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTreatmentPackageRequest req)
    {
        var validation = new UpdateTreatmentPackageRequestValidator();
        var validationResult = await validation.ValidateAsync(req);
        if (!validationResult.IsValid)
            return BadRequest(new { message = string.Join(" · ", validationResult.Errors.Select(e => e.ErrorMessage)) });

        var pkg = await db.TreatmentPackages.FirstOrDefaultAsync(x => x.Id == id);
        if (pkg is null) return NotFound(new { message = "الباقة غير موجودة" });

        pkg.Name = req.Name.Trim();
        pkg.Description = req.Description?.Trim();
        pkg.TotalPrice = req.TotalPrice;
        pkg.SessionCount = req.SessionCount > 0 ? req.SessionCount : 1;
        pkg.Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim();
        if (req.IsActive.HasValue) pkg.IsActive = req.IsActive.Value;

        await db.SaveChangesAsync();

        logger.LogInformation("TreatmentPackage updated: {Id} — {Name}", pkg.Id, pkg.Name);

        return Ok(new TreatmentPackageDto
        {
            Id = pkg.Id,
            Name = pkg.Name,
            Description = pkg.Description,
            TotalPrice = pkg.TotalPrice,
            SessionCount = pkg.SessionCount,
            Color = pkg.Color,
            IsActive = pkg.IsActive,
            CreatedAt = pkg.CreatedAt,
            UpdatedAt = pkg.UpdatedAt,
        });
    }

    /// <summary>Soft-delete a package (IsActive=false). Admin only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var pkg = await db.TreatmentPackages.FirstOrDefaultAsync(x => x.Id == id);
        if (pkg is null) return NotFound(new { message = "الباقة غير موجودة" });

        pkg.IsActive = false;
        pkg.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("TreatmentPackage soft-deleted: {Id} — {Name}", pkg.Id, pkg.Name);

        return Ok(new { message = "تم حذف الباقة بنجاح" });
    }
}

// ─── Validators ─────────────────────────────────────────────────────────────────

public sealed class CreateTreatmentPackageRequestValidator : AbstractValidator<CreateTreatmentPackageRequest>
{
    public CreateTreatmentPackageRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم الباقة مطلوب").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => !string.IsNullOrEmpty(x.Description));
        RuleFor(x => x.TotalPrice).GreaterThanOrEqualTo(0).WithMessage("السعر الإجمالي لا يمكن أن يكون سالبًا");
        RuleFor(x => x.SessionCount).GreaterThan(0).WithMessage("عدد الجلسات يجب أن يكون 1 على الأقل");
        RuleFor(x => x.Color).MaximumLength(20).Matches(@"^#?[0-9A-Fa-f]{6}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("اللون يجب أن يكون بصيغة hex مثل #3b82f6");
    }
}

public sealed class UpdateTreatmentPackageRequestValidator : AbstractValidator<UpdateTreatmentPackageRequest>
{
    public UpdateTreatmentPackageRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم الباقة مطلوب").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => !string.IsNullOrEmpty(x.Description));
        RuleFor(x => x.TotalPrice).GreaterThanOrEqualTo(0).WithMessage("السعر الإجمالي لا يمكن أن يكون سالبًا");
        RuleFor(x => x.SessionCount).GreaterThan(0).WithMessage("عدد الجلسات يجب أن يكون 1 على الأقل");
        RuleFor(x => x.Color).MaximumLength(20).Matches(@"^#?[0-9A-Fa-f]{6}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("اللون يجب أن يكون بصيغة hex مثل #3b82f6");
    }
}
