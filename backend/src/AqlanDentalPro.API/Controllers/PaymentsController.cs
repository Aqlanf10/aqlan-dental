using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.API.Hubs;
using AqlanDentalPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = "FinanceAccess")]
public class PaymentsController(IFinanceService service, IPdfService pdfService, IAuditService audit, ICurrentUserService currentUser, IRealTimePushService pushService, ILogger<PaymentsController> logger) : ControllerBase
{
    /// <summary>
    /// Best-effort push of JourneyUpdated. Payment changes affect the checkout/balance
    /// flow on the daily-ops screen. Scoped to the caller's branch when resolvable
    /// (via ICurrentUserService.BranchId, or the ResolvedBranchId on CreatePaymentRequest);
    /// falls back to PushToAllAsync for admin callers without a branch.
    /// Never throws — push failure must not fail the HTTP request.
    /// </summary>
    private async Task PushJourneyUpdatedAsync(string action, Guid? paymentId = null, Guid? patientId = null, Guid? invoiceId = null, Guid? branchId = null)
    {
        try
        {
            var payload = new { action, paymentId, patientId, invoiceId, branchId };
            if (branchId.HasValue && branchId.Value != Guid.Empty)
                await pushService.PushToBranchAsync(branchId.Value, MessagingHubEvents.JourneyUpdated, payload);
            else
                await pushService.PushToAllAsync(MessagingHubEvents.JourneyUpdated, payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push JourneyUpdated ({Action})", action);
        }
    }

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

            // H3: Audit logging for payment creation
            await audit.LogAsync(AuditAction.Create, "Payment", result.Id,
                newData: new { result.Amount, result.PatientId, result.PaymentMethod });

            // SignalR: best-effort push so daily-ops + finance screens invalidate instantly.
            // Prefer the controller-resolved branch on the request (matches FinanceService write);
            // fall back to currentUser.BranchId for non-admin callers without it.
            await PushJourneyUpdatedAsync("payment-created", result.Id, result.PatientId, result.InvoiceId, branchId: req.ResolvedBranchId ?? currentUser.BranchId);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Payment creation validation failed");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Payment creation operation failed");
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            // DB-02: xmin concurrency token on Payment (or a related Invoice/CashierSession) detected a concurrent edit.
            return Conflict(new { message = "تم تعديل الدفعة من قبل مستخدم آخر، يرجى التحديث والمحاولة مرة أخرى" });
        }
    }

    [HttpPut("payments/{id:guid}")]
    public async Task<IActionResult> UpdatePayment(Guid id, [FromBody] UpdatePaymentRequest req)
    {
        try
        {
            var result = await service.UpdatePaymentAsync(id, req);
            if (result == null) return NotFound(new { message = "الدفعة غير موجودة" });

            // SignalR: best-effort push so daily-ops + finance screens invalidate instantly.
            await PushJourneyUpdatedAsync("payment-updated", result.Id, result.PatientId, result.InvoiceId, branchId: currentUser.BranchId);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Payment update validation failed for payment {PaymentId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            // DB-02: xmin concurrency token on Payment detected a concurrent edit.
            return Conflict(new { message = "تم تعديل الدفعة من قبل مستخدم آخر، يرجى التحديث والمحاولة مرة أخرى" });
        }
    }

    [HttpDelete("payments/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        try
        {
            // Fetch payment details for audit before deletion
            var payment = await service.GetPaymentByIdAsync(id);

            var deleted = await service.DeletePaymentAsync(id);

            if (deleted && payment != null)
            {
                // H3: Audit logging for payment deletion
                await audit.LogAsync(AuditAction.Delete, "Payment", id,
                    oldData: new { payment.Amount, payment.PatientId });

                // SignalR: best-effort push so daily-ops + finance screens invalidate instantly.
                await PushJourneyUpdatedAsync("payment-deleted", id, payment.PatientId, payment.InvoiceId, branchId: currentUser.BranchId);
            }

            return deleted ? Ok(new { message = "تم حذف الدفعة بنجاح" }) : NotFound(new { message = "الدفعة غير موجودة" });
        }
        catch (DbUpdateConcurrencyException)
        {
            // DB-02: xmin concurrency token on Payment detected a concurrent edit.
            return Conflict(new { message = "تم تعديل الدفعة من قبل مستخدم آخر، يرجى التحديث والمحاولة مرة أخرى" });
        }
    }

    [HttpPost("payments/{id:guid}/refund")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundPaymentRequest? req)
    {
        var result = await service.RefundPaymentAsync(id, req?.Reason, req?.PartialAmount);

        if (result != null)
        {
            // H3: Audit logging for payment refund
            await audit.LogAsync(AuditAction.Refund, "PaymentRefund", result.Id,
                details: $"Refund of payment {id}");

            // SignalR: best-effort push so daily-ops + finance screens invalidate instantly.
            await PushJourneyUpdatedAsync("payment-refunded", result.Id, result.PatientId, result.InvoiceId, branchId: currentUser.BranchId);
        }

        return result == null ? NotFound(new { message = "الدفعة غير موجودة أو ملغاة" }) : Ok(result);
    }

    [HttpGet("patients/{patientId:guid}/finance-summary")]
    public async Task<IActionResult> GetPatientFinanceSummary(Guid patientId)
    {
        var result = await service.GetPatientFinanceSummaryAsync(patientId);
        return Ok(result);
    }

    [HttpGet("patients/{patientId:guid}/financial-statement/pdf")]
    public async Task<IActionResult> GetPatientFinancialStatementPdf(Guid patientId)
    {
        try
        {
            var pdfBytes = await pdfService.GenerateFinancialStatementAsync(patientId);
            return File(pdfBytes, "application/pdf", $"financial-statement-{patientId}.pdf");
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Financial statement PDF generation failed for patient {PatientId}", patientId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error generating financial statement PDF for patient {PatientId}", patientId);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء كشف الحساب" });
        }
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error generating payment receipt PDF for payment {PaymentId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء سند القبض" });
        }
    }
}
