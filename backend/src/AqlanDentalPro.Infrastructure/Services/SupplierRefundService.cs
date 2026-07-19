using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// TD-021 PR A4 (slice 3): the supplier-payables / credit-note cluster,
/// extracted verbatim from <c>FinanceService</c>. Pure code move — no behavior
/// change. Refund receipt numbering comes from <see cref="FinanceLedgerWriter"/>
/// (shared with PaymentService).
/// </summary>
public class SupplierRefundService(AppDbContext db, ICurrentUserService currentUser, ILogger<SupplierRefundService> logger, IJournalEntryService journalEntryService)
    : ISupplierRefundService
{
    /// <summary>
    /// Finance Phase 1: Pays a supplier bill (partially or fully).
    /// Validates open cashier session, loads bill + supplier, updates PaidAmount/Status/Balance,
    /// creates SupplierBillPayment, CashFlowTransaction (Outflow), and double-entry journal
    /// (Debit AccountsPayable / Credit Treasury). Commits atomically.
    /// </summary>
    public async Task<SupplierPaymentPostingResult> PaySupplierBillAsync(Guid billId, PaySupplierBillRequest request, Guid currentUserId)
    {
        // Validate active open cashier session
        var activeSession = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == currentUserId && s.Status == SessionStatus.Open && s.IsActive);
        if (activeSession == null)
            throw new ArgumentException("عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل سداد فواتير الموردين.");

        // BranchId guard
        if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
            throw new ArgumentException("عذراً، يجب تحديد الفرع قبل سداد فواتير الموردين.");

        // Validate amount
        if (request.Amount <= 0)
            throw new ArgumentException("يجب أن يكون مبلغ الدفعة أكبر من الصفر.");

        // Load bill with supplier
        var bill = await db.SupplierBills
            .Include(b => b.Supplier)
            .FirstOrDefaultAsync(b => b.Id == billId);
        if (bill == null || !bill.IsActive)
            throw new ArgumentException("فاتورة المورد غير موجودة.");
        if (bill.Status == BillStatus.FullyPaid)
            throw new ArgumentException("فاتورة المورد مدفوعة بالكامل بالفعل.");
        if (bill.Status == BillStatus.Cancelled)
            throw new ArgumentException("لا يمكن السداد لفاتورة ملغاة.");

        if (bill.BranchId != currentUser.BranchId.Value)
            throw new ArgumentException("لا يمكن سداد فاتورة تابعة لفرع آخر.");

        var paymentCurrency = NormalizeCurrency(bill.Currency);
        var paymentRate = await ResolveExchangeRateToYerAsync(paymentCurrency, request.ExchangeRateToYer);
        var paymentRateSource = paymentCurrency == "YER"
            ? "same_currency"
            : string.IsNullOrWhiteSpace(request.ExchangeRateSource)
                ? (request.ExchangeRateToYer.HasValue ? "manual" : "settings")
                : request.ExchangeRateSource.Trim();

        var remaining = bill.TotalAmount - bill.PaidAmount;
        if (request.Amount > remaining)
            throw new ArgumentException($"المبلغ ({request.Amount:N0}) يتجاوز الرصيد المتبقي للفاتورة ({remaining:N0}).");

        // Update bill PaidAmount and Status
        bill.PaidAmount += request.Amount;
        bill.Status = bill.PaidAmount >= bill.TotalAmount ? BillStatus.FullyPaid : BillStatus.PartiallyPaid;
        bill.UpdatedAt = DateTime.UtcNow;

        // The legacy scalar is YER-only; Finance V3 derives balances per currency.
        if (bill.Supplier != null && paymentCurrency == "YER")
        {
            bill.Supplier.Balance -= request.Amount;
            bill.Supplier.UpdatedAt = DateTime.UtcNow;
        }

        // Create SupplierBillPayment record
        var billPayment = new SupplierBillPayment
        {
            SupplierBillId = bill.Id,
            Amount = request.Amount,
            Currency = paymentCurrency,
            ExchangeRateToYer = paymentRate,
            ExchangeRateSource = paymentRateSource,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = ClinicTimeProvider.ClinicToday(),
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes,
            PaidBy = currentUserId
        };
        db.SupplierBillPayments.Add(billPayment);

        // Create CashFlowTransaction (Outflow — supplier payment)
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{datePart}-SP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SupplierPayment,
            Amount = request.Amount,
            Currency = paymentCurrency,
            PaymentMethod = request.PaymentMethod,
            TransactionDate = ClinicTimeProvider.ClinicToday(),
            ReferenceId = billPayment.Id,
            ReferenceNumber = bill.BillNumber,
            Description = $"سداد فاتورة مورد {bill.Supplier?.Name ?? "غير معروف"} - {bill.BillNumber}",
            PerformedBy = currentUserId,
            BranchId = currentUser.BranchId.Value,
            CashierSessionId = activeSession.Id
        };
        db.CashFlowTransactions.Add(cashflow);

        // Link bill payment to cashflow
        billPayment.CashFlowTransactionId = cashflow.Id;

        // Finance V3: Atomic dual-write — treasury update + journal entry within a transaction
        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            // Deduct from treasury (outflow) — use explicit TreasuryId if provided
            Treasury treasury;
            if (request.TreasuryId.HasValue && request.TreasuryId.Value != Guid.Empty)
            {
                treasury = await db.Treasuries.FirstOrDefaultAsync(t => t.Id == request.TreasuryId.Value && t.IsActive)
                    ?? throw new ArgumentException("الخزينة المحددة غير موجودة.");
                if (treasury.BranchId != currentUser.BranchId.Value)
                    throw new ArgumentException("الخزينة المحددة تابعة لفرع آخر.");
                if (!string.Equals(NormalizeCurrency(treasury.Currency), paymentCurrency, StringComparison.Ordinal))
                    throw new ArgumentException($"عملة الخزينة ({treasury.Currency}) لا تطابق عملة فاتورة المورد ({paymentCurrency}). سجل مصارفة مستقلة أولاً.");
                treasury.Balance -= request.Amount;
                treasury.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                await FinanceLedgerWriter.UpdateTreasuryBalanceNoSaveAsync(db, currentUser.BranchId.Value, -request.Amount, request.PaymentMethod, paymentCurrency);
                treasury = await FinanceLedgerWriter.ResolveTreasuryNoSaveAsync(db, currentUser.BranchId.Value, request.PaymentMethod, paymentCurrency);
            }
            cashflow.TreasuryId = treasury.Id;

            // Double-entry journal: Debit AccountsPayable / Credit Treasury
            var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
            {
                (JournalAccountType.AccountsPayable, bill.SupplierId, request.Amount, 0m, $"سداد ذمم دائنة - فاتورة {bill.BillNumber}"),
                (JournalAccountType.Treasury, treasury.Id, 0m, request.Amount, $"صرف لمورد - فاتورة {bill.BillNumber}")
            };

            var entry = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.SupplierPayment,
                financialDocumentId: billPayment.Id,
                description: $"سداد فاتورة مورد {bill.Supplier?.Name ?? ""} - {bill.BillNumber}",
                entryDate: ClinicTimeProvider.ClinicToday(),
                branchId: currentUser.BranchId.Value,
                performedBy: currentUserId,
                cashierSessionId: activeSession.Id,
                treasuryId: treasury.Id,
                lines: lines);

            // Auto-post
            entry.IsPosted = true;
            entry.PostedAt = DateTime.UtcNow;
            entry.Currency = paymentCurrency;
            entry.ExchangeRateToYer = paymentRate;
            await db.SaveChangesAsync();

            if (useTx) await tx!.CommitAsync();

            logger.LogInformation("Supplier bill {BillId} paid {Amount:N0} by user {UserId}", billId, request.Amount, currentUserId);
            return new SupplierPaymentPostingResult(billPayment.Id, entry.Id, entry.EntryNumber);
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }

    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency) ? "YER" : currency.Trim().ToUpperInvariant();
        return normalized switch
        {
            "YER" or "SAR" or "USD" => normalized,
            _ => throw new ArgumentException("العملة غير مدعومة. العملات المتاحة: YER أو SAR أو USD.")
        };
    }

    private async Task<decimal> ResolveExchangeRateToYerAsync(string currency, decimal? directRate)
    {
        if (currency == "YER") return 1m;
        if (directRate.HasValue && directRate.Value > 0m) return directRate.Value;

        var configuredRate = await db.Settings
            .Where(setting => setting.Key == $"finance.exchange_rate.{currency}_YER")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync();

        if (decimal.TryParse(configuredRate, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedRate) && parsedRate > 0m)
            return parsedRate;

        throw new ArgumentException($"لا يوجد سعر صرف معتمد للعملة {currency}. أدخله يدوياً أو حدده من أسعار الصرف قبل الدفع.");
    }

    /// <summary>
    /// Finance Phase 1: Processes a refund for an approved Credit Note.
    /// Validates open cashier session, loads creditNote + invoice, creates refund Payment (Expense type),
    /// updates creditNote status to Refunded, creates CashFlowTransaction (Outflow), and double-entry
    /// journal (Debit SalesReturns / Credit Treasury). Commits atomically.
    /// </summary>
    public async Task ProcessRefundAsync(Guid creditNoteId, ProcessRefundRequest request, Guid currentUserId)
    {
        // Validate active open cashier session
        var activeSession = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == currentUserId && s.Status == SessionStatus.Open && s.IsActive);
        if (activeSession == null)
            throw new ArgumentException("عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل معالجة استرداد إشعارات الدائن.");

        // BranchId guard
        if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
            throw new ArgumentException("عذراً، يجب تحديد الفرع قبل معالجة استرداد إشعارات الدائن.");

        // Load credit note with invoice
        var creditNote = await db.CreditNotes
            .Include(cn => cn.Invoice)
            .Include(cn => cn.Patient)
            .FirstOrDefaultAsync(cn => cn.Id == creditNoteId);
        if (creditNote == null || !creditNote.IsActive)
            throw new ArgumentException("إشعار الدائن غير موجود.");
        if (creditNote.Status != CreditNoteStatus.Approved)
            throw new ArgumentException("يجب اعتماد إشعار الدائن أولاً قبل معالجة الاسترداد. الحالة الحالية: " + creditNote.Status);

        // Create refund Payment (negative amount, Expense-type outflow)
        var receiptNumber = await FinanceLedgerWriter.GenerateRefundReceiptNumberAsync(db);
        var refund = new Payment
        {
            PatientId = creditNote.PatientId,
            InvoiceId = creditNote.InvoiceId,
            Amount = -creditNote.Amount,
            PaymentDate = ClinicTimeProvider.ClinicToday(),
            PaymentMethod = request.PaymentMethod,
            ServiceDescription = $"استرداد إشعار دائن - فاتورة {creditNote.Invoice?.InvoiceNumber ?? ""}",
            BranchId = currentUser.BranchId,
            ReceivedBy = currentUserId,
            ReceiptNumber = receiptNumber,
            Notes = request.Notes ?? creditNote.Reason
        };
        db.Payments.Add(refund);

        // Update credit note status and link refund payment
        creditNote.Status = CreditNoteStatus.Refunded;
        creditNote.RefundPaymentId = refund.Id;
        creditNote.UpdatedAt = DateTime.UtcNow;

        // Create CashFlowTransaction (Outflow — credit note refund)
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{datePart}-CN-{receiptNumber?[4..] ?? Guid.NewGuid().ToString()[..8]}",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.Refund,
            Amount = creditNote.Amount,
            PaymentMethod = request.PaymentMethod,
            TransactionDate = ClinicTimeProvider.ClinicToday(),
            ReferenceId = refund.Id,
            ReferenceNumber = receiptNumber,
            Description = $"استرداد إشعار دائن - مريض {creditNote.Patient?.FirstName ?? ""} - سند {receiptNumber}",
            PerformedBy = currentUserId,
            BranchId = currentUser.BranchId.Value,
            CashierSessionId = activeSession.Id
        };
        db.CashFlowTransactions.Add(cashflow);

        // Finance V3: Atomic dual-write — treasury update + journal entry within a transaction
        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            // Deduct from treasury (outflow for refund)
            await FinanceLedgerWriter.UpdateTreasuryBalanceNoSaveAsync(db, currentUser.BranchId.Value, -creditNote.Amount, request.PaymentMethod);

            // Resolve treasury for journal entry — use explicit TreasuryId if provided, otherwise auto-resolve
            Treasury treasury;
            if (request.TreasuryId.HasValue && request.TreasuryId.Value != Guid.Empty)
            {
                treasury = await db.Treasuries.FirstOrDefaultAsync(t => t.Id == request.TreasuryId.Value && t.IsActive)
                    ?? throw new ArgumentException("الخزينة المحددة غير موجودة.");
            }
            else
            {
                treasury = await FinanceLedgerWriter.ResolveTreasuryNoSaveAsync(db, currentUser.BranchId.Value, request.PaymentMethod);
            }
            cashflow.TreasuryId = treasury.Id;

            // Double-entry journal: Debit SalesReturns / Credit Treasury
            var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
            {
                (JournalAccountType.SalesReturns, creditNote.InvoiceId, creditNote.Amount, 0m, $"مرتجع مبيعات - إشعار دائن لفاتورة {creditNote.Invoice?.InvoiceNumber ?? ""}"),
                (JournalAccountType.Treasury, treasury.Id, 0m, creditNote.Amount, $"صرف استرداد - إشعار دائن {receiptNumber}")
            };

            var entry = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.CreditNoteRefund,
                financialDocumentId: creditNote.Id,
                description: $"استرداد إشعار دائن - فاتورة {creditNote.Invoice?.InvoiceNumber ?? ""}",
                entryDate: ClinicTimeProvider.ClinicToday(),
                branchId: currentUser.BranchId.Value,
                performedBy: currentUserId,
                cashierSessionId: activeSession.Id,
                treasuryId: treasury.Id,
                lines: lines);

            // Auto-post
            entry.IsPosted = true;
            entry.PostedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            if (useTx) await tx!.CommitAsync();
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }

        logger.LogInformation("Credit note {CreditNoteId} refund processed for {Amount:N0} by user {UserId}", creditNoteId, creditNote.Amount, currentUserId);
    }
}
