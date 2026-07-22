using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    /// <summary>
    /// GET /api/finance-v3/expenses/{id}/disbursement-voucher/pdf
    /// Prints the posted expense voucher under the expense-view permission.
    /// </summary>
    [HttpGet("expenses/{id:guid}/disbursement-voucher/pdf")]
    [Authorize(Policy = "ReportsAccess")]
    public async Task<IActionResult> DownloadExpenseDisbursementVoucher(
        Guid id,
        [FromServices] IPdfService pdfService)
    {
        if (!await CanAsync("finance.expenses", "view")) return Deny();

        var expense = await db.OperationalExpenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
        if (expense == null)
            return NotFound(new { message = "المصروف غير موجود" });

        if (!currentUser.IsAdmin && expense.BranchId != currentUser.BranchId)
            return Forbid();

        var journalEntryId = await db.JournalEntries
            .Where(je => je.FinancialDocumentId == expense.Id
                && je.FinancialDocumentType == FinancialDocumentType.Expense
                && !je.IsReversal
                && je.IsPosted)
            .Select(je => (Guid?)je.Id)
            .FirstOrDefaultAsync();
        if (!journalEntryId.HasValue)
            return BadRequest(new { message = "لم يتم ترحيل المصروف بعد، لذلك لا يوجد سند صرف قابل للطباعة." });

        try
        {
            var pdf = await pdfService.GenerateJournalEntryDisbursementVoucherAsync(journalEntryId.Value);
            return File(pdf, "application/pdf", $"disbursement-voucher-{expense.ExpenseNumber}.pdf");
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "تعذر إنشاء سند الصرف لهذا المصروف." });
        }
    }

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
