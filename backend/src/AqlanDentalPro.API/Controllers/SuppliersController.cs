using System.Security.Claims;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Controllers;

// ─── Request DTOs ────────────────────────────────────────────────────────────

public sealed class CreateSupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المورد مطلوب")
            .MaximumLength(200).WithMessage("اسم المورد يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("اسم جهة الاتصال يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPerson));

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("رقم الهاتف يجب ألا يتجاوز 30 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .MaximumLength(200).WithMessage("البريد الإلكتروني يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("العنوان يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));
    }
}

public sealed class UpdateSupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المورد مطلوب")
            .MaximumLength(200).WithMessage("اسم المورد يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("اسم جهة الاتصال يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPerson));

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("رقم الهاتف يجب ألا يتجاوز 30 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .MaximumLength(200).WithMessage("البريد الإلكتروني يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("العنوان يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));
    }
}

// ─── Controller ──────────────────────────────────────────────────────────────

[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = "AdminOnly")]
public class SuppliersController(AppDbContext db) : ControllerBase
{
    // ─── Branch resolution: Admin sees all branches; non-admin is restricted to their token branch ───
    private Guid? ResolveBranchId(Guid? requestBranchId)
    {
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var userBranch = User.FindFirst("BranchId")?.Value;

        // Admin with no specific branch request → see all (return null)
        if (userRole == "Admin" && (!requestBranchId.HasValue || requestBranchId.Value == Guid.Empty))
            return null;

        // Non-admin: force their token branch
        if (userRole != "Admin")
        {
            if (Guid.TryParse(userBranch, out Guid tokenBranchId))
                return tokenBranchId;
        }

        return requestBranchId ?? Guid.Empty;
    }
    // ─── 1. GET /api/suppliers — List all suppliers ──────────────────────
    /// <summary>Returns paginated list of suppliers with optional search.
    /// Suppliers are branch-agnostic (shared), but PurchaseOrders are branch-scoped.
    /// Non-admin users see only purchase orders from their branch.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var resolvedBranchId = ResolveBranchId(branchId);

        var query = db.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) ||
                                     (s.ContactPerson != null && s.ContactPerson.Contains(search)) ||
                                     (s.Phone != null && s.Phone.Contains(search)));

        var total = await query.CountAsync();
        var suppliers = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.ContactPerson,
                s.Phone,
                s.Email,
                s.Address,
                s.Notes,
                // Branch-scoped purchase order count: non-admin only sees their branch POs
                PurchaseOrderCount = resolvedBranchId.HasValue
                    ? s.PurchaseOrders.Count(po => po.BranchId == resolvedBranchId.Value)
                    : s.PurchaseOrders.Count,
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = suppliers, total, page, pageSize, IsConsolidated = !resolvedBranchId.HasValue });
    }

    // ─── 2. GET /api/suppliers/{id} — Get supplier by ID ────────────────
    /// <summary>Returns supplier details with purchase order count and total spent.
    /// Branch-scoped: non-admin only sees PO totals from their branch.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? branchId)
    {
        var resolvedBranchId = ResolveBranchId(branchId);

        var supplier = await db.Suppliers
            .Include(s => s.PurchaseOrders)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (supplier is null)
            return NotFound(new { message = "المورد غير موجود" });

        // Filter POs by branch for non-admin users
        var pos = resolvedBranchId.HasValue
            ? supplier.PurchaseOrders.Where(po => po.BranchId == resolvedBranchId.Value)
            : supplier.PurchaseOrders;

        var purchaseOrderCount = pos.Count();
        var totalSpent = pos
            .Where(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled)
            .Sum(po => po.TotalAmount);

        return Ok(new
        {
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.Notes,
            PurchaseOrderCount = purchaseOrderCount,
            TotalSpent = totalSpent,
            CreatedAt = supplier.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = supplier.UpdatedAt.ToString("yyyy-MM-dd"),
            IsConsolidated = !resolvedBranchId.HasValue
        });
    }

    // ─── 3. POST /api/suppliers — Create supplier ───────────────────────
    /// <summary>Creates a new supplier.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest req)
    {
        var supplier = new Supplier
        {
            Name = req.Name,
            ContactPerson = req.ContactPerson,
            Phone = req.Phone,
            Email = req.Email,
            Address = req.Address,
            Notes = req.Notes
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id },
            new { supplier.Id, supplier.Name, message = "تم إنشاء المورد بنجاح" });
    }

    // ─── 4. PUT /api/suppliers/{id} — Update supplier ───────────────────
    /// <summary>Updates an existing supplier.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierRequest req)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null)
            return NotFound(new { message = "المورد غير موجود" });

        supplier.Name = req.Name;
        supplier.ContactPerson = req.ContactPerson;
        supplier.Phone = req.Phone;
        supplier.Email = req.Email;
        supplier.Address = req.Address;
        supplier.Notes = req.Notes;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── 5. DELETE /api/suppliers/{id} — Soft-delete supplier ───────────
    /// <summary>Soft-deletes a supplier.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null)
            return NotFound(new { message = "المورد غير موجود" });

        supplier.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المورد بنجاح" });
    }
}
