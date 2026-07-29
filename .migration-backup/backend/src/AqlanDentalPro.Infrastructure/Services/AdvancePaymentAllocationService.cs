using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Moves an already collected patient advance from its liability account to the
/// receivable created by an issued invoice. No cash-flow or treasury row is written here.
/// </summary>
public class AdvancePaymentAllocationService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IJournalEntryService journalEntryService,
    ICommissionService commissionService,
    ILogger<AdvancePaymentAllocationService> logger) : IAdvancePaymentAllocationService
{
    public async Task<AdvancePaymentAllocationResult> AllocateAvailableAdvancesAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(i => i.Patient)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new ArgumentException("Invoice was not found.", nameof(invoiceId));

        if (invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.Paid))
            throw new ArgumentException("Only issued invoices can receive advance allocations.", nameof(invoiceId));

        var branchId = invoice.Patient?.BranchId ?? Guid.Empty;
        if (branchId == Guid.Empty)
            throw new InvalidOperationException("The invoice patient must belong to a branch before allocating an advance.");

        var currency = FinanceMappers.NormalizeCurrency(invoice.Currency);
        var directPaid = await DirectInvoicePaymentsAsync(invoiceId, ct);
        var alreadyAllocated = await AllocatedToInvoiceAsync(invoiceId, ct);
        var remaining = Math.Max(0m, invoice.TotalAmount - directPaid - alreadyAllocated);
        if (remaining == 0m)
            return Result(invoice, 0m, 0, remaining, currency);

        // Existing historical refunds have no source-payment FK. Skipping allocation when
        // one exists is intentionally conservative: it avoids allocating a refunded advance.
        var hasUnresolvedAdvanceRefund = await db.Payments.AnyAsync(p =>
            p.PatientId == invoice.PatientId &&
            p.BranchId == branchId &&
            p.InvoiceId == null &&
            p.ContractId == null &&
            p.Amount < 0m &&
            p.IsActive, ct);

        if (hasUnresolvedAdvanceRefund)
        {
            logger.LogWarning(
                "Skipped advance allocation for invoice {InvoiceId}: unresolved historical advance refund exists for patient {PatientId}",
                invoice.Id, invoice.PatientId);
            return Result(invoice, 0m, 0, remaining, currency, skippedDueToUnresolvedRefunds: true);
        }

        var candidates = await db.Payments
            .Include(p => p.Allocations.Where(a => a.IsActive))
            .Where(p => p.PatientId == invoice.PatientId &&
                p.BranchId == branchId &&
                p.InvoiceId == null &&
                p.ContractId == null &&
                p.Amount > 0m &&
                p.IsActive)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var performedBy = currentUser.UserId
            ?? throw new InvalidOperationException("An authenticated user is required to allocate advances.");
        var allocatedAmount = 0m;
        var allocationCount = 0;

        foreach (var payment in candidates)
        {
            if (remaining == 0m)
                break;

            if (FinanceMappers.NormalizeCurrency(payment.AccountCurrency) != currency)
                continue;

            var paymentAppliedAmount = payment.AppliedAmount == 0m ? payment.Amount : payment.AppliedAmount;
            var paymentAllocatedAmount = payment.Allocations.Sum(a => a.Amount);
            var availableAmount = Math.Max(0m, paymentAppliedAmount - paymentAllocatedAmount);
            if (availableAmount == 0m)
                continue;

            var amount = Math.Min(availableAmount, remaining);
            var allocation = new PaymentAllocation
            {
                PaymentId = payment.Id,
                InvoiceId = invoice.Id,
                BranchId = branchId,
                Amount = amount,
                Currency = currency,
                Notes = "Automatic advance allocation when invoice was issued."
            };
            db.PaymentAllocations.Add(allocation);

            var journalEntry = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.PaymentAllocation,
                financialDocumentId: allocation.Id,
                description: $"Apply patient advance to invoice {invoice.InvoiceNumber}",
                entryDate: ClinicTimeProvider.ClinicToday(),
                branchId: branchId,
                performedBy: performedBy,
                cashierSessionId: null,
                treasuryId: null,
                lines:
                [
                    (JournalAccountType.PatientAdvance, invoice.PatientId, amount, 0m, $"Release patient advance for invoice {invoice.InvoiceNumber}"),
                    (JournalAccountType.PatientReceivable, invoice.PatientId, 0m, amount, $"Settle invoice receivable {invoice.InvoiceNumber}")
                ],
                autoSave: false,
                ct: ct);

            journalEntry.Currency = currency;
            journalEntry.IsPosted = true;
            journalEntry.PostedAt = DateTime.UtcNow;
            allocation.JournalEntryId = journalEntry.Id;

            // Payment has an xmin concurrency token. Updating its timestamp makes a
            // competing allocation of this same advance fail instead of overspending it.
            payment.UpdatedAt = DateTime.UtcNow;

            remaining -= amount;
            allocatedAmount += amount;
            allocationCount++;
        }

        if (allocationCount == 0)
            return Result(invoice, 0m, 0, remaining, currency);

        await db.SaveChangesAsync(ct);
        await ReconcileInvoiceStatusAsync(invoice, ct);

        try
        {
            await commissionService.TriggerOnPaymentCommissionsAsync(invoice.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OnPaymentCollection commission trigger failed after advance allocation for invoice {InvoiceId}", invoice.Id);
        }

        return Result(invoice, allocatedAmount, allocationCount, remaining, currency);
    }

    public async Task<AdvancePaymentAllocationResult> ReleaseInvoiceAllocationsAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new ArgumentException("Invoice was not found.", nameof(invoiceId));

        var allocations = await db.PaymentAllocations
            .Where(a => a.InvoiceId == invoiceId && a.IsActive)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        if (allocations.Count == 0)
            return Result(invoice, 0m, 0, Math.Max(0m, invoice.TotalAmount - await DirectInvoicePaymentsAsync(invoiceId, ct)), FinanceMappers.NormalizeCurrency(invoice.Currency));

        var performedBy = currentUser.UserId
            ?? throw new InvalidOperationException("An authenticated user is required to release advance allocations.");
        var releasedAmount = 0m;

        foreach (var allocation in allocations)
        {
            if (!allocation.JournalEntryId.HasValue)
                throw new InvalidOperationException($"Allocation {allocation.Id} has no journal entry and cannot be released safely.");

            var originalEntry = await db.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == allocation.JournalEntryId.Value, ct);
            if (originalEntry == null || !originalEntry.IsPosted)
                throw new InvalidOperationException($"Allocation {allocation.Id} has no posted journal entry and cannot be released safely.");
            if (originalEntry.ReversedByEntryId.HasValue)
                throw new InvalidOperationException($"Allocation {allocation.Id} was already reversed.");

            var reversalEntry = await journalEntryService.CreateReversalEntryAsync(
                originalEntry.Id,
                "Release patient advance allocation",
                performedBy,
                ct);
            reversalEntry.IsPosted = true;
            reversalEntry.PostedAt = DateTime.UtcNow;

            allocation.IsActive = false;
            allocation.DeletedAt = DateTime.UtcNow;
            allocation.DeletedBy = performedBy;
            releasedAmount += allocation.Amount;
        }

        await db.SaveChangesAsync(ct);
        await ReconcileInvoiceStatusAsync(invoice, ct);

        try
        {
            await commissionService.TriggerOnPaymentCommissionsAsync(invoice.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OnPaymentCollection commission trigger failed after releasing advances for invoice {InvoiceId}", invoice.Id);
        }

        var currency = FinanceMappers.NormalizeCurrency(invoice.Currency);
        var remaining = Math.Max(0m, invoice.TotalAmount - await DirectInvoicePaymentsAsync(invoiceId, ct));
        return Result(invoice, releasedAmount, allocations.Count, remaining, currency);
    }

    public Task<bool> HasActiveAllocationsForPaymentAsync(Guid paymentId, CancellationToken ct = default) =>
        db.PaymentAllocations.AnyAsync(a => a.PaymentId == paymentId, ct);

    private async Task ReconcileInvoiceStatusAsync(Invoice invoice, CancellationToken ct)
    {
        var totalPaid = await DirectInvoicePaymentsAsync(invoice.Id, ct) + await AllocatedToInvoiceAsync(invoice.Id, ct);
        var shouldBePaid = totalPaid >= invoice.TotalAmount && invoice.TotalAmount > 0m;

        if (invoice.Status == InvoiceStatus.Issued && shouldBePaid)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.UpdatedBy = currentUser.UserId;
            await db.SaveChangesAsync(ct);
        }
        else if (invoice.Status == InvoiceStatus.Paid && !shouldBePaid)
        {
            invoice.Status = InvoiceStatus.Issued;
            invoice.UpdatedBy = currentUser.UserId;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<decimal> DirectInvoicePaymentsAsync(Guid invoiceId, CancellationToken ct) =>
        await db.Payments
            .Where(p => p.InvoiceId == invoiceId && p.IsActive)
            .SumAsync(p => (decimal?)(p.AppliedAmount == 0m ? p.Amount : p.AppliedAmount), ct) ?? 0m;

    private async Task<decimal> AllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct) =>
        await db.PaymentAllocations
            .Where(a => a.InvoiceId == invoiceId && a.IsActive)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

    private static AdvancePaymentAllocationResult Result(
        Invoice invoice,
        decimal amount,
        int count,
        decimal remaining,
        string currency,
        bool skippedDueToUnresolvedRefunds = false) =>
        new(invoice.Id, amount, count, remaining, currency, skippedDueToUnresolvedRefunds);
}
