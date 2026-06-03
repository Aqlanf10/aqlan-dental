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

public sealed class CreateLabRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? WhatsApp { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? Email { get; init; }
    public string? Notes { get; init; }
    public Guid? BranchId { get; init; }
}

public sealed class CreateLabRequestValidator : AbstractValidator<CreateLabRequest>
{
    public CreateLabRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المعمل مطلوب").MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(30).When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.WhatsApp).MaximumLength(30).When(x => !string.IsNullOrEmpty(x.WhatsApp));
        RuleFor(x => x.Address).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Address));
        RuleFor(x => x.ContactPerson).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.ContactPerson));
        RuleFor(x => x.Email).EmailAddress().WithMessage("البريد الإلكتروني غير صالح").MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public sealed class UpdateLabRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? WhatsApp { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? Email { get; init; }
    public string? Notes { get; init; }
    public Guid? BranchId { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class UpdateLabRequestValidator : AbstractValidator<UpdateLabRequest>
{
    public UpdateLabRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المعمل مطلوب").MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(30).When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.WhatsApp).MaximumLength(30).When(x => !string.IsNullOrEmpty(x.WhatsApp));
        RuleFor(x => x.Address).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Address));
        RuleFor(x => x.ContactPerson).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.ContactPerson));
        RuleFor(x => x.Email).EmailAddress().WithMessage("البريد الإلكتروني غير صالح").MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}

// ─── Controller ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/labs")]
[Authorize(Policy = "StaffOnly")]
public class LabsController(AppDbContext db, ICurrentUserService currentUser, ILogger<LabsController> logger) : ControllerBase
{
    private Task<bool> CanAsync(string action) => PermissionGuard.HasAsync(db, currentUser, "labs", action);

    private static readonly Func<Lab, object> LabProjection = l => new
    {
        l.Id,
        l.Name,
        l.Phone,
        l.WhatsApp,
        l.Address,
        l.ContactPerson,
        l.Email,
        l.Notes,
        l.BranchId,
        l.IsActive,
        CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd"),
        UpdatedAt = l.UpdatedAt.ToString("yyyy-MM-dd")
    };

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = true, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!await CanAsync("view")) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.Labs.AsQueryable();
        if (activeOnly == true)
            query = query.Where(l => l.IsActive);

        var total = await query.CountAsync();
        var labs = await query
            .OrderBy(l => l.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.Phone,
                l.WhatsApp,
                l.Address,
                l.ContactPerson,
                l.Email,
                l.Notes,
                l.BranchId,
                l.IsActive,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = l.UpdatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = labs, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        var lab = await db.Labs.FindAsync(id);
        if (lab is null) return NotFound(new { message = "المعمل غير موجود" });

        return Ok(new
        {
            lab.Id,
            lab.Name,
            lab.Phone,
            lab.WhatsApp,
            lab.Address,
            lab.ContactPerson,
            lab.Email,
            lab.Notes,
            lab.BranchId,
            lab.IsActive,
            CreatedAt = lab.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = lab.UpdatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLabRequest req)
    {
        if (!await CanAsync("create")) return Forbid();

        var lab = new Lab
        {
            Name = req.Name,
            Phone = req.Phone,
            WhatsApp = req.WhatsApp,
            Address = req.Address,
            ContactPerson = req.ContactPerson,
            Email = req.Email,
            Notes = req.Notes,
            BranchId = req.BranchId,
        };

        db.Labs.Add(lab);
        await db.SaveChangesAsync();

        logger.LogInformation("Lab created: {LabId} — {LabName}", lab.Id, lab.Name);

        return CreatedAtAction(nameof(GetById), new { id = lab.Id }, new
        {
            lab.Id,
            lab.Name,
            lab.IsActive
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLabRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        var lab = await db.Labs.FindAsync(id);
        if (lab is null) return NotFound(new { message = "المعمل غير موجود" });

        lab.Name = req.Name;
        lab.Phone = req.Phone;
        lab.WhatsApp = req.WhatsApp;
        lab.Address = req.Address;
        lab.ContactPerson = req.ContactPerson;
        lab.Email = req.Email;
        lab.Notes = req.Notes;
        lab.BranchId = req.BranchId;
        if (req.IsActive.HasValue) lab.IsActive = req.IsActive.Value;

        await db.SaveChangesAsync();

        logger.LogInformation("Lab updated: {LabId} — {LabName}", lab.Id, lab.Name);

        return Ok(new
        {
            lab.Id,
            lab.Name,
            lab.IsActive
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await CanAsync("delete")) return Forbid();

        var lab = await db.Labs.FindAsync(id);
        if (lab is null) return NotFound(new { message = "المعمل غير موجود" });

        lab.IsActive = false;
        await db.SaveChangesAsync();

        logger.LogInformation("Lab soft-deleted: {LabId} — {LabName}", lab.Id, lab.Name);

        return Ok(new { message = "تم حذف المعمل بنجاح" });
    }
}
