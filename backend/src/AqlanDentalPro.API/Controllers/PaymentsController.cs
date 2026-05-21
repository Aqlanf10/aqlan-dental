using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = "FinanceAccess")]
public class PaymentsController(FinanceService service, IPdfService pdfService, ILogger<PaymentsController> logger) : ControllerBase
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
        try
        {
            var result = await service.CreatePaymentAsync(req);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Payment creation validation failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("payments/{id:guid}")]
    public async Task<IActionResult> UpdatePayment(Guid id, [FromBody] UpdatePaymentRequest req)
    {
        try
        {
            var result = await service.UpdatePaymentAsync(id, req);
            return result == null ? NotFound(new { message = "الدفعة غير موجودة" }) : Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Payment update validation failed for payment {PaymentId}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("payments/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        var deleted = await service.DeletePaymentAsync(id);
        return deleted ? Ok(new { message = "تم حذف الدفعة بنجاح" }) : NotFound(new { message = "الدفعة غير موجودة" });
    }

    [HttpPost("payments/{id:guid}/refund")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundPaymentRequest? req)
    {
        var result = await service.RefundPaymentAsync(id, req?.Reason);
        return result == null ? NotFound(new { message = "الدفعة غير موجودة أو ملغاة" }) : Ok(result);
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

    [HttpGet("patients/{patientId:guid}/finance-summary")]
    public async Task<IActionResult> GetPatientFinanceSummary(Guid patientId)
    {
        var result = await service.GetPatientFinanceSummaryAsync(patientId);
        return Ok(result);
    }

    [HttpGet("payments/{id:guid}/pdf")]
    public async Task<IActionResult> GetPaymentPdf(Guid id)
    {
        try
        {
            var pdfBytes = await pdfService.GeneratePaymentReceiptAsync(id);
            return File(pdfBytes, "application/pdf", $"receipt-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Payment receipt PDF generation failed for payment {PaymentId}", id);
            return NotFound(new { message = ex.Message });
        }
    }
}
