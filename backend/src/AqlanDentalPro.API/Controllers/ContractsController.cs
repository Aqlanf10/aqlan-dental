using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize(Policy = "FinanceAccess")]
public class ContractsController(IFinanceService service, FinanceSettingsReader financeSettings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? patientId = null,
        [FromQuery] string? status = null)
    {
        var result = await service.GetContractsAsync(page, pageSize, patientId, status);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await service.GetContractByIdAsync(id);
        return result == null ? NotFound(new { message = "العقد غير موجود" }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContractRequest req)
    {
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
