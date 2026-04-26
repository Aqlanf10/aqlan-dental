using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class PaymentsController(FinanceService service) : ControllerBase
{
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? patientId = null)
    {
        var result = await service.GetPaymentsAsync(page, pageSize, patientId);
        return Ok(result);
    }

    [HttpGet("payments/{id:guid}")]
    public async Task<IActionResult> GetPaymentById(Guid id)
    {
        var result = await service.GetPaymentByIdAsync(id);
        return result == null ? NotFound(new { message = "الدفعة غير موجودة" }) : Ok(result);
    }

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest req)
    {
        var result = await service.CreatePaymentAsync(req);
        return Ok(result);
    }

    [HttpGet("finance/summary")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await service.GetSummaryAsync();
        return Ok(result);
    }

    [HttpGet("finance/overdue")]
    public async Task<IActionResult> GetOverdue()
    {
        var result = await service.GetOverdueContractsAsync();
        return Ok(result);
    }
}
