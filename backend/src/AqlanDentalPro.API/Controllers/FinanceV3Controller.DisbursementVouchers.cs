using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    /// <summary>
    /// GET /api/finance-v3/cashflows/{id}/disbursement-voucher/pdf
    /// Prints a unified disbursement voucher for any financial outflow.
    /// </summary>
    [HttpGet("cashflows/{id:guid}/disbursement-voucher/pdf")]
    [Authorize(Policy = "ReportsAccess")]
    public async Task<IActionResult> DownloadCashFlowDisbursementVoucher(
        Guid id,
        [FromServices] IPdfService pdfService)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();

        try
        {
            var pdf = await pdfService.GenerateCashFlowDisbursementVoucherAsync(id);
            return File(pdf, "application/pdf", $"disbursement-voucher-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "تعذر إنشاء سند الصرف — البيانات غير صالحة" });
        }
    }

    /// <summary>
    /// GET /api/finance-v3/journal-entries/{id}/disbursement-voucher/pdf
    /// Prints a disbursement voucher for posted journal entries that credit a treasury.
    /// </summary>
    [HttpGet("journal-entries/{id:guid}/disbursement-voucher/pdf")]
    [Authorize(Policy = "ReportsAccess")]
    public async Task<IActionResult> DownloadJournalEntryDisbursementVoucher(
        Guid id,
        [FromServices] IPdfService pdfService)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();

        try
        {
            var pdf = await pdfService.GenerateJournalEntryDisbursementVoucherAsync(id);
            return File(pdf, "application/pdf", $"disbursement-voucher-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "تعذر إنشاء سند الصرف — البيانات غير صالحة" });
        }
    }
}
