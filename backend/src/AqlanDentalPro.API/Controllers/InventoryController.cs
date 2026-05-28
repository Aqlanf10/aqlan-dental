using System.Security.Claims;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateInventoryItemRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public int Quantity { get; init; }
    public int MinQuantity { get; init; }
    public string? Unit { get; init; }
    public decimal? CostPerUnit { get; init; }
    public string? BatchNumber { get; init; }
    public string? ExpiryDate { get; init; }
    public Guid? DefaultSupplierId { get; init; }
    public Guid? BranchId { get; init; }
}

public sealed class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المادة مطلوب")
            .MaximumLength(200).WithMessage("اسم المادة يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("الكمية يجب أن تكون صفراً أو أكثر");

        RuleFor(x => x.MinQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("الحد الأدنى يجب أن يكون صفراً أو أكثر");

        RuleFor(x => x.CostPerUnit)
            .GreaterThanOrEqualTo(0).WithMessage("تكلفة الوحدة يجب أن تكون صفراً أو أكثر")
            .When(x => x.CostPerUnit.HasValue);

        RuleFor(x => x.BatchNumber)
            .MaximumLength(50).WithMessage("رقم الدفعة يجب ألا يتجاوز 50 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.BatchNumber));

        RuleFor(x => x.ExpiryDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الانتهاء غير صالح. استخدم YYYY-MM-DD")
            .When(x => !string.IsNullOrWhiteSpace(x.ExpiryDate));
    }
}

public sealed class AdjustQuantityRequest
{
    public int Delta { get; init; }
    public string? Reason { get; init; }
}

public sealed class AdjustQuantityRequestValidator : AbstractValidator<AdjustQuantityRequest>
{
    public AdjustQuantityRequestValidator()
    {
        RuleFor(x => x.Delta)
            .NotEqual(0).WithMessage("التعديل يجب أن يكون قيمة غير صفرية");
    }
}

[ApiController]
[Route("api/inventory")]
[Authorize(Policy = "AdminOnly")]
public class InventoryController(AppDbContext db) : ControllerBase
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

    // ─── 1. GET /api/inventory — List inventory items (branch-scoped) ──────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] bool? lowStock,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var resolvedBranchId = ResolveBranchId(branchId);

        var query = db.Inventory.AsQueryable();

        // ─── Branch restriction ───
        if (resolvedBranchId.HasValue)
            query = query.Where(i => i.BranchId == resolvedBranchId.Value);

        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(i => i.Category == category);
        if (lowStock == true) query = query.Where(i => i.Quantity <= i.MinQuantity);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.Category).ThenBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.Category,
                i.Quantity,
                i.MinQuantity,
                i.Unit,
                i.CostPerUnit,
                i.BatchNumber,
                ExpiryDate = i.ExpiryDate != null ? i.ExpiryDate.Value.ToString("yyyy-MM-dd") : (string?)null,
                i.DefaultSupplierId,
                DefaultSupplierName = i.DefaultSupplier != null ? i.DefaultSupplier.Name : (string?)null,
                IsLowStock = i.Quantity <= i.MinQuantity,
                i.BranchId,
                CreatedAt = i.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize, IsConsolidated = !resolvedBranchId.HasValue });
    }

    // ─── 2. GET /api/inventory/categories ──────────────────────────────────────
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] Guid? branchId)
    {
        var resolvedBranchId = ResolveBranchId(branchId);
        var query = db.Inventory.Where(i => i.Category != null);

        if (resolvedBranchId.HasValue)
            query = query.Where(i => i.BranchId == resolvedBranchId.Value);

        var categories = await query
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        return Ok(categories);
    }

    // ─── 3. GET /api/inventory/low-stock ───────────────────────────────────────
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock([FromQuery] Guid? branchId)
    {
        var resolvedBranchId = ResolveBranchId(branchId);
        var query = db.Inventory.Where(i => i.Quantity <= i.MinQuantity);

        if (resolvedBranchId.HasValue)
            query = query.Where(i => i.BranchId == resolvedBranchId.Value);

        var items = await query
            .OrderBy(i => i.Quantity)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.Category,
                i.Quantity,
                i.MinQuantity,
                i.Unit
            })
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Returns total inventory valuation (sum of quantity * costPerUnit).</summary>
    [HttpGet("valuation")]
    public async Task<IActionResult> GetValuation([FromQuery] Guid? branchId)
    {
        var resolvedBranchId = ResolveBranchId(branchId);
        var query = db.Inventory.AsQueryable();

        if (resolvedBranchId.HasValue)
            query = query.Where(i => i.BranchId == resolvedBranchId.Value);

        var totalItems = await query.CountAsync();
        var totalQuantity = await query.SumAsync(i => (int?)i.Quantity) ?? 0;
        var totalValue = await query
            .Where(i => i.CostPerUnit.HasValue)
            .SumAsync(i => (decimal?)(i.Quantity * i.CostPerUnit!.Value)) ?? 0m;

        var lowStockCount = await query.CountAsync(i => i.Quantity <= i.MinQuantity);

        return Ok(new
        {
            TotalItems = totalItems,
            TotalQuantity = totalQuantity,
            TotalValue = totalValue,
            LowStockCount = lowStockCount,
            IsConsolidated = !resolvedBranchId.HasValue
        });
    }

    /// <summary>Returns items expiring within the specified number of days.</summary>
    [HttpGet("expiring-soon")]
    public async Task<IActionResult> GetExpiringSoon([FromQuery] int days = 30, [FromQuery] Guid? branchId = null)
    {
        if (days < 1) days = 30;

        var resolvedBranchId = ResolveBranchId(branchId);
        var query = db.Inventory.AsQueryable();

        if (resolvedBranchId.HasValue)
            query = query.Where(i => i.BranchId == resolvedBranchId.Value);

        var cutoffDate = DateOnly.FromDateTime(DateTime.Today.AddDays(days));
        var today = DateOnly.FromDateTime(DateTime.Today);

        var items = await query
            .Where(i => i.ExpiryDate != null && i.ExpiryDate <= cutoffDate)
            .OrderBy(i => i.ExpiryDate)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.Category,
                i.Quantity,
                i.BatchNumber,
                ExpiryDateStr = i.ExpiryDate!.Value.ToString("yyyy-MM-dd"),
                ExpiryDateValue = i.ExpiryDate!.Value,
                IsExpired = i.ExpiryDate < today
            })
            .ToListAsync();

        // Compute DaysUntilExpiry in memory (PostgreSQL DateDiff not available via EF.Functions)
        var result = items.Select(i => new
        {
            i.Id,
            i.Name,
            i.Category,
            i.Quantity,
            i.BatchNumber,
            ExpiryDate = i.ExpiryDateStr,
            DaysUntilExpiry = i.ExpiryDateValue.DayNumber - today.DayNumber,
            i.IsExpired
        }).ToList();

        return Ok(result);
    }

    // ─── 4. POST /api/inventory — Create inventory item ────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemRequest req)
    {
        DateOnly? expiryDate = null;
        if (!string.IsNullOrWhiteSpace(req.ExpiryDate))
        {
            if (!DateOnly.TryParse(req.ExpiryDate, out var parsed))
                return BadRequest(new { message = "تنسيق تاريخ الانتهاء غير صالح. استخدم YYYY-MM-DD" });
            expiryDate = parsed;
        }

        // Resolve branch for the new item
        var resolvedBranchId = ResolveBranchId(req.BranchId);

        var item = new Inventory
        {
            Name = req.Name,
            Category = req.Category,
            Quantity = req.Quantity,
            MinQuantity = req.MinQuantity,
            Unit = req.Unit,
            CostPerUnit = req.CostPerUnit,
            BatchNumber = req.BatchNumber,
            ExpiryDate = expiryDate,
            DefaultSupplierId = req.DefaultSupplierId,
            BranchId = resolvedBranchId
        };

        db.Inventory.Add(item);

        // ─── Double-write: Create InventoryAdjustment for initial stock ───
        if (req.Quantity > 0)
        {
            var adjustment = new InventoryAdjustment
            {
                InventoryItemId = item.Id,
                PreviousQuantity = 0,
                NewQuantity = req.Quantity,
                Delta = req.Quantity,
                Reason = "رصيد افتتاحي — إنشاء مادة جديدة",
                AdjustmentType = "initial",
                AdjustedBy = GetCurrentUserId()
            };
            db.InventoryAdjustments.Add(adjustment);
        }

        await db.SaveChangesAsync();
        return CreatedAtAction(null, new { id = item.Id }, new { item.Id, item.Name });
    }

    // ─── 5. PUT /api/inventory/{id} — Update inventory item ────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateInventoryItemRequest req)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        DateOnly? expiryDate = null;
        if (!string.IsNullOrWhiteSpace(req.ExpiryDate))
        {
            if (!DateOnly.TryParse(req.ExpiryDate, out var parsed))
                return BadRequest(new { message = "تنسيق تاريخ الانتهاء غير صالح. استخدم YYYY-MM-DD" });
            expiryDate = parsed;
        }

        // Note: Quantity changes should go through AdjustQuantity endpoint, not here
        item.Name = req.Name;
        item.Category = req.Category;
        // Quantity is NOT updated here — use /adjust endpoint for audit trail
        item.MinQuantity = req.MinQuantity;
        item.Unit = req.Unit;
        item.CostPerUnit = req.CostPerUnit;
        item.BatchNumber = req.BatchNumber;
        item.ExpiryDate = expiryDate;
        item.DefaultSupplierId = req.DefaultSupplierId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── 6. PUT /api/inventory/{id}/adjust — Adjust quantity (double-write) ────
    /// <summary>
    /// Adjusts inventory quantity with full audit trail.
    /// Every adjustment creates an InventoryAdjustment record describing:
    /// previousQuantity, newQuantity, delta, reason, adjustedBy, adjustmentType.
    /// </summary>
    [HttpPut("{id:guid}/adjust")]
    public async Task<IActionResult> AdjustQuantity(Guid id, [FromBody] AdjustQuantityRequest req)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        var previousQty = item.Quantity;
        var newQty = item.Quantity + req.Delta;
        if (newQty < 0) return BadRequest(new { message = "الكمية لا يمكن أن تكون سالبة" });

        item.Quantity = newQty;

        // ─── Double-write: Create detailed inventory adjustment record ───
        // Every stock increment/decrement MUST create an automatic log describing delta logic.
        var adjustmentType = req.Delta > 0 ? "manual_add" : "manual_remove";
        var adjustment = new InventoryAdjustment
        {
            InventoryItemId = item.Id,
            PreviousQuantity = previousQty,
            NewQuantity = newQty,
            Delta = req.Delta,
            Reason = req.Reason ?? (req.Delta > 0 ? "إضافة يدوية للمخزون" : "سحب يدوي من المخزون"),
            AdjustmentType = adjustmentType,
            AdjustedBy = GetCurrentUserId()
        };

        db.InventoryAdjustments.Add(adjustment);
        await db.SaveChangesAsync();

        return Ok(new
        {
            id,
            previousQuantity = previousQty,
            newQuantity = item.Quantity,
            delta = req.Delta,
            reason = adjustment.Reason,
            isLowStock = item.Quantity <= item.MinQuantity
        });
    }

    /// <summary>Returns adjustment history for an inventory item.</summary>
    [HttpGet("{id:guid}/adjustments")]
    public async Task<IActionResult> GetAdjustments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var itemExists = await db.Inventory.AnyAsync(i => i.Id == id);
        if (!itemExists)
            return NotFound(new { message = "المادة غير موجودة" });

        pageSize = Math.Max(1, Math.Min(pageSize, 100));

        var query = db.InventoryAdjustments
            .Where(a => a.InventoryItemId == id);

        var total = await query.CountAsync();
        var adjustments = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.PreviousQuantity,
                a.NewQuantity,
                a.Delta,
                a.Reason,
                a.AdjustmentType,
                a.AdjustedBy,
                a.PurchaseOrderLineItemId,
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();

        return Ok(new { data = adjustments, total, page, pageSize });
    }

    // ─── 7. DELETE /api/inventory/{id} — Soft-delete ──────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        item.IsActive = false;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = GetCurrentUserId();
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المادة بنجاح" });
    }

    // ─── Private helpers ───────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
