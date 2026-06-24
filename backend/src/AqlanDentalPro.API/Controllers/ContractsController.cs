using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize(Policy = "FinanceAccess")]
public class ContractsController(IFinanceService service, FinanceSettingsReader financeSettings, AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // FIN-PERM: the class-level FinanceAccess policy (Admin + Reception + Accountant)
    // is the coarse gate; the granular finance.contracts permission (RolePermissions,
    // owner-configurable from Settings) is the real per-action gate. Admin always
    // bypasses (see PermissionGuard). Reception is seeded view-only on finance.contracts.
    // Status transitions (active/completed/cancelled) = approve (Accountant/Admin).
    private Task<bool> CanAsync(string action) =>
        PermissionGuard.HasAsync(db, currentUser, "finance.contracts", action);

    private IActionResult Deny() =>
        StatusCode(403, new { message = "غير مصرح لك بهذا الإجراء المالي" });

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? patientId = null,
        [FromQuery] string? status = null)
    {
        // FIN-PERM: finance.contracts.view
        if (!await CanAsync("view")) return Deny();

        var result = await service.GetContractsAsync(page, pageSize, patientId, status);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // FIN-PERM: finance.contracts.view
        if (!await CanAsync("view")) return Deny();

        var result = await service.GetContractByIdAsync(id);
        return result == null ? NotFound(new { message = "العقد غير موجود" }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContractRequest req)
    {
        // FIN-PERM: finance.contracts.create (Reception is seeded view-only).
        if (!await CanAsync("create")) return Deny();

        // FIN-SETTINGS: global max-discount % cap (defaults to 100 = no restriction).
        // Preserves current behavior until the clinic owner lowers the setting.
        var maxError = await ValidateMaxDiscountAsync(req.TotalAmount, req.DiscountAmount);
        if (maxError is not null) return maxError;

        var result = await service.CreateContractAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractRequest req)
    {
        // FIN-PERM: finance.contracts.edit (Reception is seeded view-only).
        if (!await CanAsync("edit")) return Deny();

        if (req.DiscountAmount > req.TotalAmount)
            return BadRequest(new { message = "قيمة الخصم لا يمكن أن تتجاوز إجمالي العقد" });

        // FIN-SETTINGS: global max-discount % cap (defaults to 100 = no restriction).
        var maxError = await ValidateMaxDiscountAsync(req.TotalAmount, req.DiscountAmount);
        if (maxError is not null) return maxError;

        if (!string.IsNullOrWhiteSpace(req.StartDate) && !DateOnly.TryParse(req.StartDate, out _))
            return BadRequest(new { message = "تاريخ البدء غير صالح" });

        var result = await service.UpdateContractAsync(id, req);
        return result == null ? NotFound(new { message = "العقد غير موجود" }) : Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateContractStatusBody body)
    {
        // FIN-PERM: finance.contracts.approve — status transitions (active/completed/
        // cancelled) are financial state changes. Accountant/Admin only.
        if (!await CanAsync("approve")) return Deny();

        var allowed = new[] { "active", "completed", "cancelled" };
        if (!allowed.Contains(body.Status))
            return BadRequest(new { message = "الحالة غير صالحة — القيم المسموحة: active، completed، cancelled" });

        var result = await service.UpdateContractStatusAsync(id, body.Status);
        if (result == null) return NotFound(new { message = "العقد غير موجود" });
        return Ok(result);
    }

    /// <summary>
    /// FIN-SETTINGS — rejects a discount whose percentage-of-total exceeds the
    /// configured cap. Returns <c>null</c> when the discount is within bounds.
    /// Defaults to 100% (= no restriction) so current behavior is preserved.
    /// </summary>
    private async Task<IActionResult?> ValidateMaxDiscountAsync(decimal totalAmount, decimal discountAmount)
    {
        if (discountAmount <= 0 || totalAmount <= 0) return null;

        var maxDiscountPct = await financeSettings.GetDecimalAsync(FinanceSettingsKeys.MaxDiscountPercentage);
        if (maxDiscountPct >= 100m) return null;

        var discountPct = discountAmount / totalAmount * 100m;
        if (discountPct > maxDiscountPct)
        {
            return BadRequest(new
            {
                message = $"نسبة الخصم ({discountPct:F1}%) تتجاوز الحد الأقصى المسموح ({maxDiscountPct:F0}%)",
                discountPercent = discountPct,
                maxAllowed = maxDiscountPct,
            });
        }
        return null;
    }
}

public record UpdateContractStatusBody(string Status);
