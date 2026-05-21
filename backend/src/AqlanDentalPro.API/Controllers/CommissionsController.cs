using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/commissions")]
[Authorize(Policy = "CommissionView")]
public class CommissionsController(
    ICommissionService commissionService,
    ICurrentUserService currentUser) : ControllerBase
{
    // ── Line item commission ──────────────────────────────────────────────────

    [HttpGet("line-items/{lineItemId:guid}")]
    public async Task<IActionResult> GetLineItem(Guid lineItemId)
    {
        var result = await commissionService.GetLineItemCommissionAsync(lineItemId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("invoices/{invoiceId:guid}")]
    public async Task<IActionResult> GetInvoiceCommissions(Guid invoiceId)
    {
        var result = await commissionService.GetInvoiceCommissionsAsync(invoiceId);
        return Ok(result);
    }

    [HttpPost("line-items/{lineItemId:guid}/recalculate")]
    [Authorize(Policy = "CommissionEdit")]
    public async Task<IActionResult> Recalculate(Guid lineItemId)
    {
        var result = await commissionService.RecalculateAsync(lineItemId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPatch("line-items/{lineItemId:guid}/costs")]
    [Authorize(Policy = "CommissionEdit")]
    public async Task<IActionResult> UpdateCosts(Guid lineItemId, [FromBody] UpdateLineItemCommissionRequest req)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.UpdateCostsAsync(lineItemId, req, userId.Value);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("line-items/{lineItemId:guid}/approve")]
    [Authorize(Policy = "CommissionApprove")]
    public async Task<IActionResult> Approve(Guid lineItemId, [FromBody] ApproveCommissionRequest req)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.ApproveAsync(lineItemId, req, userId.Value);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("line-items/{lineItemId:guid}/unlock")]
    [Authorize(Policy = "CommissionApprove")]
    public async Task<IActionResult> Unlock(Guid lineItemId)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.UnlockAsync(lineItemId, userId.Value);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("line-items/{lineItemId:guid}/auto-fill")]
    [Authorize(Policy = "CommissionEdit")]
    public async Task<IActionResult> AutoFill(Guid lineItemId)
    {
        await commissionService.AutoFillFromServiceAsync(lineItemId);
        var result = await commissionService.GetLineItemCommissionAsync(lineItemId);
        return result == null ? NotFound() : Ok(result);
    }

    // ── Report ────────────────────────────────────────────────────────────────

    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] Guid? doctorId,
        [FromQuery] Guid? branchId,
        [FromQuery] string? serviceCategory,
        [FromQuery] string? commissionStatus,
        [FromQuery] string? paymentStatus)
    {
        if (!DateOnly.TryParse(from, out var fromDate))
            return BadRequest(new { message = "تاريخ البداية غير صالح" });
        if (!DateOnly.TryParse(to, out var toDate))
            return BadRequest(new { message = "تاريخ النهاية غير صالح" });

        // Doctors can only see their own data unless ViewAllDoctorsCommissions
        if (!currentUser.IsAdmin && currentUser.UserId != null)
        {
            var doctorRecord = await GetDoctorIdForUserAsync(currentUser.UserId.Value);
            if (doctorRecord.HasValue && (!doctorId.HasValue || doctorId != doctorRecord))
                doctorId = doctorRecord;
        }

        var result = await commissionService.GetReportAsync(
            fromDate, toDate, doctorId, branchId,
            serviceCategory, commissionStatus, paymentStatus);

        return Ok(result);
    }

    // ── Commission payment disbursement ───────────────────────────────────────

    [HttpPost("payments")]
    [Authorize(Policy = "CommissionPay")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordCommissionPaymentRequest req)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.RecordPaymentAsync(req, userId.Value);
            return Created(string.Empty, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] Guid? doctorId)
    {
        var result = await commissionService.GetPaymentsAsync(doctorId);
        return Ok(result);
    }

    // ── Service commission defaults ───────────────────────────────────────────

    [HttpGet("services/{serviceId:guid}/defaults")]
    public async Task<IActionResult> GetServiceDefaults(Guid serviceId)
    {
        var result = await commissionService.GetServiceDefaultsAsync(serviceId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut("services/{serviceId:guid}/defaults")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateServiceDefaults(
        Guid serviceId, [FromBody] UpdateServiceCommissionDefaultsRequest req)
    {
        var result = await commissionService.UpdateServiceDefaultsAsync(serviceId, req);
        return result == null ? NotFound() : Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<Guid?> GetDoctorIdForUserAsync(Guid userId) =>
        commissionService.GetDoctorIdForUserAsync(userId);
}
