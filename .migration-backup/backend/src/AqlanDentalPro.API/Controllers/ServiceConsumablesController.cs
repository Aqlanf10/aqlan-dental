using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// YOLO-S2: CRUD for ServiceConsumable (service ↔ inventory item with quantity +
/// optional notes). GET is open to all staff (settings/services page reads these
/// for the consumables-management UI when an admin opens a service). POST/DELETE
/// are AdminOnly — consumables affect catalog pricing logic and material usage
/// tracking, so writes are admin-gated like the rest of the services catalog.
/// Soft-delete pattern (IsActive=false) matches the codebase.
/// </summary>
[ApiController]
[Route("api/service-consumables")]
[Authorize(Policy = "StaffOnly")]
public class ServiceConsumablesController(AppDbContext db, ILogger<ServiceConsumablesController> logger) : ControllerBase
{
    /// <summary>Get all consumables for a service. Pass serviceId to filter.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? serviceId)
    {
        var query = db.ServiceConsumables.AsQueryable();
        if (serviceId.HasValue)
            query = query.Where(c => c.ClinicServiceId == serviceId.Value);

        var items = await query
            .Join(db.Inventory.IgnoreQueryFilters(),
                c => c.InventoryItemId,
                i => i.Id,
                (c, i) => new ServiceConsumableDto
                {
                    Id = c.Id,
                    ClinicServiceId = c.ClinicServiceId,
                    InventoryItemId = c.InventoryItemId,
                    InventoryItemName = i.Name,
                    InventoryItemUnit = i.Unit,
                    Quantity = c.Quantity,
                    Notes = c.Notes,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                })
            .OrderBy(c => c.InventoryItemName)
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Create a new ServiceConsumable. Admin only.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateServiceConsumableRequest req)
    {
        var validation = new CreateServiceConsumableRequestValidator();
        var validationResult = await validation.ValidateAsync(req);
        if (!validationResult.IsValid)
            return BadRequest(new { message = string.Join(" · ", validationResult.Errors.Select(e => e.ErrorMessage)) });

        // Validate the service + inventory item exist.
        var serviceExists = await db.ClinicServices.IgnoreQueryFilters().AnyAsync(s => s.Id == req.ClinicServiceId);
        if (!serviceExists)
            return BadRequest(new { message = "الخدمة غير موجودة" });

        var itemExists = await db.Inventory.IgnoreQueryFilters().AnyAsync(i => i.Id == req.InventoryItemId);
        if (!itemExists)
            return BadRequest(new { message = "المادة غير موجودة" });

        // Prevent duplicate (service, item) pairs — bump quantity instead.
        var existing = await db.ServiceConsumables
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ClinicServiceId == req.ClinicServiceId
                && c.InventoryItemId == req.InventoryItemId
                && c.IsActive);
        if (existing is not null)
            return Conflict(new { message = "المادة مضافة بالفعل لهذه الخدمة — حدّث الكمية بدلًا من الإضافة المكررة" });

        var consumable = new ServiceConsumable
        {
            ClinicServiceId = req.ClinicServiceId,
            InventoryItemId = req.InventoryItemId,
            Quantity = req.Quantity > 0 ? req.Quantity : 1,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
        };

        db.ServiceConsumables.Add(consumable);
        await db.SaveChangesAsync();

        logger.LogInformation("ServiceConsumable created: service={ServiceId} item={ItemId} qty={Qty}",
            consumable.ClinicServiceId, consumable.InventoryItemId, consumable.Quantity);

        var itemName = await db.Inventory.IgnoreQueryFilters()
            .Where(i => i.Id == consumable.InventoryItemId)
            .Select(i => new { i.Name, i.Unit })
            .FirstOrDefaultAsync();

        return Ok(new ServiceConsumableDto
        {
            Id = consumable.Id,
            ClinicServiceId = consumable.ClinicServiceId,
            InventoryItemId = consumable.InventoryItemId,
            InventoryItemName = itemName?.Name ?? "",
            InventoryItemUnit = itemName?.Unit,
            Quantity = consumable.Quantity,
            Notes = consumable.Notes,
            CreatedAt = consumable.CreatedAt,
            UpdatedAt = consumable.UpdatedAt,
        });
    }

    /// <summary>Soft-delete a ServiceConsumable. Admin only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var consumable = await db.ServiceConsumables.FirstOrDefaultAsync(c => c.Id == id);
        if (consumable is null) return NotFound(new { message = "المادة المستهلكة غير موجودة" });

        consumable.IsActive = false;
        consumable.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("ServiceConsumable soft-deleted: {Id}", consumable.Id);

        return Ok(new { message = "تم حذف المادة المستهلكة بنجاح" });
    }
}

// ─── Validators ─────────────────────────────────────────────────────────────────

public sealed class CreateServiceConsumableRequestValidator : AbstractValidator<CreateServiceConsumableRequest>
{
    public CreateServiceConsumableRequestValidator()
    {
        RuleFor(x => x.ClinicServiceId).NotEmpty().WithMessage("معرّف الخدمة مطلوب");
        RuleFor(x => x.InventoryItemId).NotEmpty().WithMessage("معرّف المادة مطلوب");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("الكمية يجب أن تكون 1 على الأقل");
        RuleFor(x => x.Notes).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
