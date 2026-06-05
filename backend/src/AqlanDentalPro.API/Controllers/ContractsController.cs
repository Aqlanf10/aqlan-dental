using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize(Policy = "FinanceAccess")]
public class ContractsController(IFinanceService service) : ControllerBase
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
        var result = await service.CreateContractAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractRequest req)
    {
        if (req.DiscountAmount > req.TotalAmount)
            return BadRequest(new { message = "قيمة الخصم لا يمكن أن تتجاوز إجمالي العقد" });

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
}

public record UpdateContractStatusBody(string Status);
