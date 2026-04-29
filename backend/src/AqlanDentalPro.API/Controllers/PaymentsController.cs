using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = "FinanceAccess")]
public class PaymentsController(FinanceService service, INotificationService notificationService) : ControllerBase
{
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? contractId = null,
        [FromQuery] string? paymentMethod = null,
        [FromQuery] string? specialty = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var result = await service.GetPaymentsAsync(page, pageSize, patientId, contractId, paymentMethod, specialty, from, to);
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
        var validator = new CreatePaymentRequestValidator();
        var validationResult = await validator.ValidateAsync(req);
        if (!validationResult.IsValid)
            return BadRequest(new { message = "بيانات غير صالحة", errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        var result = await service.CreatePaymentAsync(req);

        await notificationService.NotifyRoleAsync("Accountant", "payment", "دفعة جديدة", "تم تسجيل دفعة جديدة", "payments", result.Id);

        return Ok(result);
    }

    [HttpGet("receipts/{paymentId:guid}")]
    public async Task<IActionResult> GetReceipt(Guid paymentId)
    {
        var result = await service.GetReceiptByPaymentIdAsync(paymentId);
        return result == null ? NotFound(new { message = "الإيصال غير موجود" }) : Ok(result);
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
