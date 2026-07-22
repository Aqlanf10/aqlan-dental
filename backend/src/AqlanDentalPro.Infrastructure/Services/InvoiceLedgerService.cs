using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Invoice-ledger posting service — extracted from <c>FinanceService</c> as part of TD-021 PR A1.
/// Owns the accrual journal entries that mirror invoice state changes.
///
/// This is a pure code move (no logic change). The previous implementation lived in
/// <c>FinanceService.PostInvoiceIssuedEntryAsync</c> /
/// <c>FinanceService.ReverseInvoiceIssuedEntryAsync</c> and was self-contained
/// (no shared private helpers with the rest of FinanceService).
/// </summary>
public class InvoiceLedgerService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IJournalEntryService journalEntryService,
    ILogger<InvoiceLedgerService> logger) : IInvoiceLedgerService
{
    /// <summary>
    /// Dual-write journal entry for invoice issuance (accrual basis).
    /// Debit PatientReceivable / Credit Revenue.
    /// Only for InvoiceStatus.Issued, NOT for Draft.
    /// </summary>
    public async Task PostInvoiceIssuedEntryAsync(Guid invoiceId)
    {
        var invoice = await db.Invoices
            .Include(i => i.Patient)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
            throw new ArgumentException($"Invoice {invoiceId} not found");

        if (invoice.Status != InvoiceStatus.Issued)
            throw new ArgumentException($"Invoice {invoiceId} is not in Issued status — cannot post issuance entry");

        if (invoice.PatientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be Guid.Empty for invoice issuance entry");

        var branchId = invoice.Patient?.BranchId ?? Guid.Empty;
        if (branchId == Guid.Empty)
            throw new ArgumentException("Cannot determine BranchId for invoice issuance entry — patient has no branch");

        var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
        {
            (JournalAccountType.PatientReceivable, invoice.PatientId, invoice.TotalAmount, 0m, $"إصدار فاتورة {invoice.InvoiceNumber} - ذمم مدينة"),
            (JournalAccountType.Revenue, invoice.Id, 0m, invoice.TotalAmount, $"إيراد فاتورة {invoice.InvoiceNumber}")
        };

        var entry = await journalEntryService.CreateEntryAsync(
            documentType: FinancialDocumentType.Invoice,
            financialDocumentId: invoice.Id,
            description: $"إصدار فاتورة {invoice.InvoiceNumber} - إثبات الإيراد المستحق",
            entryDate: DateOnly.FromDateTime(invoice.CreatedAt),
            branchId: branchId,
            performedBy: invoice.UpdatedBy ?? invoice.CreatedBy ?? Guid.Empty,
            cashierSessionId: null,
            treasuryId: null,
            lines: lines,
            autoSave: false);

        // Auto-post
        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Reverses the original invoice issuance JournalEntry for a cancelled invoice.
    /// Finds the original issuance JE (Debit PatientReceivable / Credit Revenue)
    /// and creates a reversal entry (Credit PatientReceivable / Debit Revenue).
    /// Auto-posts the reversal. Used when cancelling an Issued invoice.
    /// </summary>
    public async Task ReverseInvoiceIssuedEntryAsync(Guid invoiceId)
    {
        var originalEntry = await db.JournalEntries
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == invoiceId
                && (e.FinancialDocumentType == FinancialDocumentType.Invoice
                    || e.FinancialDocumentType == FinancialDocumentType.OpeningBalance)
                && !e.IsReversal);

        if (originalEntry == null)
        {
            logger.LogWarning("No original issuance JournalEntry found for invoice {InvoiceId} — skipping reversal", invoiceId);
            return;
        }

        var reversal = await journalEntryService.CreateReversalEntryAsync(
            originalEntryId: originalEntry.Id,
            reason: "إلغاء فاتورة مصدرة",
            performedBy: currentUser.UserId ?? Guid.Empty);

        // Auto-post the reversal
        reversal.IsPosted = true;
        reversal.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
