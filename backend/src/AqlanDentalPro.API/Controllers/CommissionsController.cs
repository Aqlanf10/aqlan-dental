using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/commissions")]
[Authorize(Policy = "CommissionView")]
public class CommissionsController(
    AppDbContext db,
    ICommissionService commissionService,
    ICommissionAdjustmentService adjustmentService,
    ICurrentUserService currentUser) : ControllerBase
{
    // FIN-PERM (Group B): the class-level CommissionView policy is the coarse gate;
    // the granular finance.commissions permission (RolePermissions, owner-configurable
    // from Settings) is the real per-action gate. Admin always bypasses (PermissionGuard).
    private Task<bool> CanAsync(string action) =>
        PermissionGuard.HasAsync(db, currentUser, "finance.commissions", action);

    private IActionResult Deny() =>
        StatusCode(403, new { message = "غير مصرح لك بهذا الإجراء المالي" });

    // ── Line item commission ──────────────────────────────────────────────────

    [HttpGet("line-items/{lineItemId:guid}")]
    public async Task<IActionResult> GetLineItem(Guid lineItemId)
    {
        if (!await CanAsync("view")) return Deny();
        var result = await commissionService.GetLineItemCommissionAsync(lineItemId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("invoices/{invoiceId:guid}")]
    public async Task<IActionResult> GetInvoiceCommissions(Guid invoiceId)
    {
        if (!await CanAsync("view")) return Deny();
        var result = await commissionService.GetInvoiceCommissionsAsync(invoiceId);
        return Ok(result);
    }

    [HttpPost("line-items/{lineItemId:guid}/recalculate")]
    [Authorize(Policy = "CommissionEdit")]
    public async Task<IActionResult> Recalculate(Guid lineItemId)
    {
        if (!await CanAsync("edit")) return Deny();
        var result = await commissionService.RecalculateAsync(lineItemId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPatch("line-items/{lineItemId:guid}/costs")]
    [Authorize(Policy = "CommissionEdit")]
    public async Task<IActionResult> UpdateCosts(Guid lineItemId, [FromBody] UpdateLineItemCommissionRequest req)
    {
        if (!await CanAsync("edit")) return Deny();
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.UpdateCostsAsync(lineItemId, req, userId.Value);
            return result == null ? NotFound() : Ok(result);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "بيانات تكاليف العمولة غير صالحة" });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "تعذر تحديث تكاليف العمولة — الحالة الحالية لا تسمح" });
        }
    }

    [HttpPost("line-items/{lineItemId:guid}/approve")]
    [Authorize(Policy = "CommissionApprove")]
    public async Task<IActionResult> Approve(Guid lineItemId, [FromBody] ApproveCommissionRequest req)
    {
        if (!await CanAsync("approve")) return Deny();
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.ApproveAsync(lineItemId, req, userId.Value);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "تعذر اعتماد العمولة — الحالة الحالية لا تسمح" });
        }
    }

    [HttpPost("line-items/{lineItemId:guid}/unlock")]
    [Authorize(Policy = "CommissionApprove")]
    public async Task<IActionResult> Unlock(Guid lineItemId)
    {
        if (!await CanAsync("approve")) return Deny();
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.UnlockAsync(lineItemId, userId.Value);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "تعذر فتح قفل العمولة — الحالة الحالية لا تسمح" });
        }
    }

    [HttpPost("line-items/{lineItemId:guid}/auto-fill")]
    [Authorize(Policy = "CommissionEdit")]
    public async Task<IActionResult> AutoFill(Guid lineItemId)
    {
        if (!await CanAsync("edit")) return Deny();
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
        if (!await CanAsync("view")) return Deny();
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
        if (!await CanAsync("create")) return Deny();
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await commissionService.RecordPaymentAsync(req, userId.Value);
            return Created(string.Empty, result);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "بيانات دفعة العمولة غير صالحة" });
        }
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] Guid? doctorId)
    {
        if (!await CanAsync("view")) return Deny();
        var result = await commissionService.GetPaymentsAsync(doctorId);
        return Ok(result);
    }

    // ── Service commission defaults ───────────────────────────────────────────

    [HttpGet("services/{serviceId:guid}/defaults")]
    public async Task<IActionResult> GetServiceDefaults(Guid serviceId)
    {
        if (!await CanAsync("view")) return Deny();
        var result = await commissionService.GetServiceDefaultsAsync(serviceId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut("services/{serviceId:guid}/defaults")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateServiceDefaults(
        Guid serviceId, [FromBody] UpdateServiceCommissionDefaultsRequest req)
    {
        try
        {
            var result = await commissionService.UpdateServiceDefaultsAsync(serviceId, req);
            return result == null ? NotFound() : Ok(result);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "بيانات افتراضيات العمولة غير صالحة" });
        }
    }

    // ── Backfill preview (admin-only, read-only) ──────────────────────────────

    [HttpGet("backfill-preview")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetBackfillPreview(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] Guid? doctorId)
    {
        if (!DateOnly.TryParse(from, out var fromDate))
            return BadRequest(new { message = "تاريخ البداية غير صالح" });
        if (!DateOnly.TryParse(to, out var toDate))
            return BadRequest(new { message = "تاريخ النهاية غير صالح" });

        var result = await commissionService.GetBackfillPreviewAsync(fromDate, toDate, doctorId);
        return Ok(result);
    }

    // ── Commission adjustments (CORE-FIN-LAB-ADJ) ─────────────────────────────

    /// <summary>
    /// Correction lines raised against already-PAID commissions. A doctor only ever sees their
    /// own, mirroring the scoping the commission report already applies.
    /// </summary>
    [HttpGet("adjustments")]
    public async Task<IActionResult> GetAdjustments([FromQuery] Guid? doctorId, [FromQuery] string? status)
    {
        if (!await CanAsync("view")) return Deny();

        doctorId = await ScopeToOwnDoctorAsync(doctorId);

        var result = await adjustmentService.GetAdjustmentsAsync(doctorId, status, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>What the clinic owes this doctor right now, correction lines included.</summary>
    [HttpGet("doctors/{doctorId:guid}/settlement")]
    public async Task<IActionResult> GetSettlement(Guid doctorId)
    {
        if (!await CanAsync("view")) return Deny();

        // A doctor asking about someone else's settlement gets their own, not a 403 leak of
        // whether that other doctor exists — same shape as the report endpoint above.
        var scoped = await ScopeToOwnDoctorAsync(doctorId);
        var result = await adjustmentService.GetSettlementSummaryAsync(
            scoped ?? doctorId, HttpContext.RequestAborted);

        return result == null ? NotFound(new { message = "الطبيب غير موجود" }) : Ok(result);
    }

    /// <summary>
    /// Re-syncs every commission linked to a lab order against that order's ACTUAL cost.
    /// Unpaid commissions are corrected in place; paid ones get a separate correction line.
    /// Idempotent — running it again with nothing changed raises nothing.
    /// </summary>
    [HttpPost("lab-orders/{labOrderId:guid}/resync")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ResyncLabOrder(Guid labOrderId)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await adjustmentService.ResyncLabOrderAsync(
            labOrderId, userId.Value, HttpContext.RequestAborted);
        await db.SaveChangesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Backfill sweep over historical records. Only touches line items that ALREADY carry a lab
    /// order link — it never guesses a link, so an ambiguous record stays untouched and shows up
    /// in the unlinked fix-up list instead.
    /// </summary>
    [HttpPost("adjustments/backfill")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> BackfillAdjustments([FromQuery] Guid? doctorId)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await adjustmentService.ResyncAllLinkedAsync(
            doctorId, userId.Value, HttpContext.RequestAborted);
        await db.SaveChangesAsync();
        return Ok(result);
    }

    [HttpPost("adjustments/{adjustmentId:guid}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CancelAdjustment(
        Guid adjustmentId, [FromBody] CancelCommissionAdjustmentRequest req)
    {
        var userId = currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await adjustmentService.CancelAdjustmentAsync(
                adjustmentId, req.Reason ?? string.Empty, userId.Value, HttpContext.RequestAborted);
            return result == null ? NotFound(new { message = "بند التسوية غير موجود" }) : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<Guid?> GetDoctorIdForUserAsync(Guid userId) =>
        commissionService.GetDoctorIdForUserAsync(userId);

    /// <summary>
    /// Narrows a requested doctor filter to the caller's own doctor record unless they are an
    /// admin — the same rule GetReport applies, kept in one place so a new endpoint cannot
    /// accidentally expose one doctor's earnings to another.
    /// </summary>
    private async Task<Guid?> ScopeToOwnDoctorAsync(Guid? requested)
    {
        if (currentUser.IsAdmin || currentUser.UserId == null) return requested;

        var own = await GetDoctorIdForUserAsync(currentUser.UserId.Value);
        return own ?? requested;
    }
}
