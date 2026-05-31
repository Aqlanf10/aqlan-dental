using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps FinancialDocumentType to legacy CashFlowTransaction Category strings
    /// for frontend compatibility during the migration period.
    /// </summary>
    private static string MapDocumentTypeToCategory(FinancialDocumentType docType) => docType switch
    {
        FinancialDocumentType.Payment => "PatientPayment",
        FinancialDocumentType.Refund => "Refund",
        FinancialDocumentType.Expense => "OperationalExpense",
        FinancialDocumentType.SalaryPayment => "SalaryPayment",
        FinancialDocumentType.CommissionPayment => "DoctorCommission",
        FinancialDocumentType.SupplierPayment => "SupplierPayment",
        FinancialDocumentType.VaultTransfer => "InternalTransfer",
        FinancialDocumentType.ContractCancellation => "Reversal",
        FinancialDocumentType.PaymentDeletion => "Reversal",
        FinancialDocumentType.CreditNoteRefund => "Refund",
        FinancialDocumentType.Invoice => "Revenue",
        FinancialDocumentType.AdvancePayment => "AdvancePayment",
        _ => "Other"
    };

    /// <summary>
    /// Calculates the net cash outflow for a specific FinancialDocumentType category.
    /// Returns: original outflows (Treasury Credit) minus reversal inflows (Treasury Debit).
    /// In double-entry: Treasury Credit = cash paid out, Treasury Debit = cash received back (reversal).
    /// Reversals are identified by JournalEntry.IsReversal on entries of the same document type.
    /// </summary>
    private async Task<decimal> CalculateCashCategoryAsync(
        FinancialDocumentType docType,
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        // Original outflows: Treasury Credit lines from non-reversal entries
        var outflows = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.Credit > 0
                && l.JournalEntry.FinancialDocumentType == docType
                && !l.JournalEntry.IsReversal
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Credit) ?? 0;

        // Reversal inflows: Treasury Debit lines from reversal entries of same doc type
        var reversalInflows = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.Debit > 0
                && l.JournalEntry.FinancialDocumentType == docType
                && l.JournalEntry.IsReversal
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Debit) ?? 0;

        return outflows - reversalInflows;
    }

    /// <summary>
    /// Sprint 1: Resolves the effective branch ID for the current user.
    /// - Non-admin users: uses their assigned BranchId (must be valid, else Guid.Empty).
    /// - Admin users with a valid BranchId: uses their assigned branch.
    /// - Admin users with no branch (Guid.Empty): falls back to the first active
    ///   branch in the system, allowing admin to perform write operations across branches.
    /// Returns Guid.Empty only if no active branches exist in the system.
    /// </summary>
    private async Task<Guid> ResolveBranchIdAsync()
    {
        // Non-admin: must have a valid branch assignment
        if (!currentUser.IsAdmin)
        {
            if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
                throw new InvalidOperationException("المستخدم ليس لديه فرع معين. تواصل مع الإدارة.");
            return currentUser.BranchId.Value;
        }

        // Admin with valid branch: use their assigned branch
        if (currentUser.BranchId.HasValue && currentUser.BranchId.Value != Guid.Empty)
            return currentUser.BranchId.Value;

        // Admin without branch: fallback to first active branch
        var firstBranchId = await db.Branches
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => b.Id)
            .FirstOrDefaultAsync();

        return firstBranchId; // Guid.Empty if no branches exist
    }

    private async Task<decimal> CalculateContractOutstandingAsync(Guid? branchId)
    {
        var query = db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active);

        if (branchId.HasValue)
            query = query.Where(c => c.Patient.BranchId == branchId.Value);

        var contracts = await query.ToListAsync();
        // Sprint 1: Nullable-safe aggregation — use decimal? sum with ?? 0m fallback
        // to prevent overflow or null issues on empty payment collections
        return contracts.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => (decimal?)p.Amount ?? 0m));
    }

    private async Task<decimal> CalculateInvoiceOutstandingAsync(Guid? branchId)
    {
        var query = db.Invoices
            .Include(i => i.Payments)
            .Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);

        if (branchId.HasValue)
            query = query.Where(i => i.Patient.BranchId == branchId.Value);

        var invoices = await query.ToListAsync();
        // Sprint 1: Nullable-safe aggregation
        return invoices.Sum(i => i.TotalAmount - i.Payments.Where(p => p.IsActive).Sum(p => (decimal?)p.Amount ?? 0m));
    }
}
