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
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] bool? lowStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.Inventory.AsQueryable();

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
                IsLowStock = i.Quantity <= i.MinQuantity,
                CreatedAt = i.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await db.Inventory
            .Where(i => i.Category != null)
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        return Ok(categories);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var items = await db.Inventory
            .Where(i => i.Quantity <= i.MinQuantity)
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemRequest req)
    {
        var item = new Inventory
        {
            Name        = req.Name,
            Category    = req.Category,
            Quantity    = req.Quantity,
            MinQuantity = req.MinQuantity,
            Unit        = req.Unit,
            CostPerUnit = req.CostPerUnit
        };

        db.Inventory.Add(item);
        await db.SaveChangesAsync();
        return CreatedAtAction(null, new { id = item.Id }, new { item.Id, item.Name });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateInventoryItemRequest req)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        item.Name        = req.Name;
        item.Category    = req.Category;
        item.Quantity    = req.Quantity;
        item.MinQuantity = req.MinQuantity;
        item.Unit        = req.Unit;
        item.CostPerUnit = req.CostPerUnit;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/adjust")]
    public async Task<IActionResult> AdjustQuantity(Guid id, [FromBody] AdjustQuantityRequest req)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        var newQty = item.Quantity + req.Delta;
        if (newQty < 0) return BadRequest(new { message = "الكمية لا يمكن أن تكون سالبة" });

        item.Quantity = newQty;
        await db.SaveChangesAsync();

        return Ok(new
        {
            id,
            newQuantity = item.Quantity,
            isLowStock = item.Quantity <= item.MinQuantity
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        item.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المادة بنجاح" });
    }
}
