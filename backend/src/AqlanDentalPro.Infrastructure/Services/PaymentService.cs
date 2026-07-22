using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// TD-021 PR A4 (slice 2): the payment cluster, extracted verbatim from
/// <c>FinanceService</c> — reads, create (atomic dual-write), metadata update,
/// delete (reversal), refund (partial/full with idempotency guard), plus the
/// invoice/contract status reconciliation the payment mutations trigger.
/// Pure code move — no behavior change. Receipt numbering moved to
/// <see cref="FinanceLedgerWriter"/> (shared with SupplierRefundService).
/// </summary>
public class PaymentService(AppDbContext db, ICurrentUserService currentUser, INotificationService notifications, ILogger<PaymentService> logger, ICommissionService commissionService, IJournalEntryService journalEntryService)
    : IPaymentService
{
    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid id)
    {
        var p = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.Id == id);
        return p == null ? null : FinanceMappers.MapPayment(p);
    }

    public async Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (branchId.HasValue) query = query.Where(p => p.BranchId == branchId.Value);
        if (patientId.HasValue) query = query.Where(p => p.PatientId == patientId);

        return await query
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => FinanceMappers.MapPayment(p))
            .ToListAsync();
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req)
    {
        // Require active open cashier session
        var userId = currentUser.UserId ?? Guid.Empty;
        var activeSession = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
        if (activeSession == null)
            throw new ArgumentException("عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل أي مدفوعات.");

        // BranchId resolution: prefer controller-resolved branch (prevents Guid.Empty for Admin),
        // then fall back to current user's branch, then first active branch for Admin
        Guid branchId;
        if (req.ResolvedBranchId.HasValue && req.ResolvedBranchId.Value != Guid.Empty)
        {
            // Use the branch resolved and validated by the controller (Fix 4)
            branchId = req.ResolvedBranchId.Value;
        }
        else if (currentUser.BranchId.HasValue && currentUser.BranchId.Value != Guid.Empty)
        {
            branchId = currentUser.BranchId.Value;
        }
        else
        {
            // An admin without a selected branch can still post only to the
            // branch of the open cashier drawer they are using.
            branchId = activeSession.BranchId;
        }

        // Safety guard: never write Guid.Empty as BranchId in financial records (Fix 4)
        if (branchId == Guid.Empty)
            throw new ArgumentException("لم يتم تحديد فرع صالح. لا يمكن تسجيل الدفعة بدون فرع.");

        if (activeSession.BranchId != branchId)
            throw new InvalidOperationException("وردية الكاشير المفتوحة لا تتبع الفرع المحدد للدفعة.");

        // Phase 0B: Validate payment amount is positive
        if (req.Amount <= 0)
            throw new ArgumentException("يجب أن يكون مبلغ الدفعة أكبر من الصفر.");

        // Validate InvoiceId / ContractId if provided and resolve the account currency.
        Invoice? invoice = null;
        Contract? contract = null;
        if (req.InvoiceId.HasValue)
        {
            invoice = await db.Invoices.FindAsync(req.InvoiceId.Value);
            if (invoice == null || !invoice.IsActive)
                throw new ArgumentException("الفاتورة المحددة غير موجودة");
            // Only Issued invoices can receive payments
            if (invoice.Status != InvoiceStatus.Issued)
                throw new ArgumentException("يمكن تسجيل الدفعات للفواتير المصدرة فقط");
            // Payment patient must match invoice patient
            if (req.PatientId != invoice.PatientId)
                throw new ArgumentException("المريض في الدفعة لا يطابق المريض في الفاتورة");

        }
        if (req.ContractId.HasValue)
        {
            contract = await db.Contracts.FindAsync(req.ContractId.Value);
            if (contract == null || !contract.IsActive)
                throw new ArgumentException("العقد المحدد غير موجود");
            if (contract.PatientId != req.PatientId)
                throw new ArgumentException("المريض في الدفعة لا يطابق المريض في العقد");
        }

        var paymentCurrency = FinanceMappers.NormalizeCurrency(req.Currency);
        var accountCurrency = ResolveAccountCurrency(req.AccountCurrency, invoice, contract);
        var fx = await ResolveFxSnapshotAsync(
            paymentCurrency,
            accountCurrency,
            req.ExchangeRateToAccountCurrency,
            req.ExchangeRateToYer);
        var exchange = fx.PaymentToAccountCurrency;
        var exchangeRateSource = string.IsNullOrWhiteSpace(req.ExchangeRateSource)
            ? (req.ExchangeRateToAccountCurrency.HasValue || req.ExchangeRateToYer.HasValue ? "manual" : "settings")
            : req.ExchangeRateSource.Trim();
        var appliedAmount = Math.Round(req.Amount * exchange, 2, MidpointRounding.AwayFromZero);

        if (invoice != null)
        {
            var alreadyPaid = await db.Payments
                .Where(p => p.InvoiceId == invoice.Id && p.IsActive)
                .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;
            var advanceAllocated = await db.PaymentAllocations
                .Where(a => a.InvoiceId == invoice.Id && a.IsActive)
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;
            var remaining = invoice.TotalAmount - alreadyPaid - advanceAllocated;
            if (appliedAmount > remaining)
                throw new ArgumentException($"المبلغ المحتسب ({appliedAmount:N0} {accountCurrency}) يتجاوز الرصيد المتبقي للفاتورة ({remaining:N0} {accountCurrency})");
        }

        // Finance V3: True atomic dual-write — start transaction BEFORE any entity mutation
        // so that Payment, Receipt, CashFlow, Treasury, and JournalEntry are all committed
        // together or rolled back together. Previously, UpdateTreasuryBalanceAsync called
        // SaveChangesAsync independently, committing entities before the JE transaction started.

        // H9 FIX: Generate receipt number and ALL entity mutations INSIDE the transaction
        // to avoid DbContext concurrency issues. Any DbContext query (like GenerateReceiptNumberAsync)
        // before BeginTransactionAsync can conflict with the transaction's DbContext tracking.
        var storedPaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "cash" : req.PaymentMethod.Trim();
        var normalizedPaymentMethod = FinanceLedgerWriter.NormalizePaymentMethod(storedPaymentMethod);
        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        Payment payment;
        try
        {
            var receiptNumber = await FinanceLedgerWriter.GenerateReceiptNumberAsync(db);

            payment = new Payment
            {
                PatientId = req.PatientId,
                ContractId = req.ContractId,
                InvoiceId = req.InvoiceId,
                Amount = req.Amount,
                Currency = paymentCurrency,
                AccountCurrency = accountCurrency,
                ExchangeRateToAccountCurrency = exchange,
                ExchangeRateToYer = fx.PaymentCurrencyToYer,
                AppliedAmount = appliedAmount,
                ExchangeRateSource = exchangeRateSource,
                PaymentDate = ClinicTimeProvider.ClinicToday(),
                PaymentMethod = storedPaymentMethod,
                ServiceDescription = req.ServiceDescription,
                Specialty = req.Specialty,
                DoctorId = req.DoctorId,
                BranchId = branchId,
                ReceivedBy = currentUser.UserId,
                ReceiptNumber = receiptNumber,
                Notes = req.Notes
            };

            db.Payments.Add(payment);

            // Auto-create receipt record
            db.Receipts.Add(new Receipt
            {
                PaymentId = payment.Id,
                ReceiptNumber = receiptNumber,
                PrintedBy = currentUser.UserId
            });

            // Auto-create central ledger cashflow transaction (Inflow) in the physical
            // currency received. Patient balance settlement uses AppliedAmount below.
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-IN-{payment.ReceiptNumber?[4..] ?? Guid.NewGuid().ToString()[..8]}",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.PatientPayment,
                Amount = payment.Amount,
                Currency = paymentCurrency,
                PaymentMethod = storedPaymentMethod,
                TransactionDate = payment.PaymentDate,
                ReferenceId = payment.Id,
                ReferenceNumber = payment.ReceiptNumber,
                Description = $"تحصيل دفعة مريض - سند قبض {payment.ReceiptNumber}",
                PerformedBy = userId,
                BranchId = branchId,
                CashierSessionId = activeSession.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            await FinanceLedgerWriter.UpdateTreasuryBalanceNoSaveAsync(db, payment.BranchId ?? Guid.Empty, payment.Amount, normalizedPaymentMethod, paymentCurrency);
            await FinanceLedgerWriter.DualWritePaymentEntryAsync(db, journalEntryService, payment, cashflow, invoice);
            await db.SaveChangesAsync();
            if (useTx) await tx!.CommitAsync();
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }

        // Auto-transition invoice to Paid if payments cover the total
        if (invoice != null)
        {
            await TryMarkInvoicePaidAsync(invoice.Id);
            // Trigger proportional commission for OnPaymentCollection services
            try { await commissionService.TriggerOnPaymentCommissionsAsync(invoice.Id); }
            catch (Exception ex) { logger.LogWarning(ex, "OnPaymentCollection commission trigger failed for invoice {InvoiceId}", invoice.Id); }
        }

        // Auto-transition contract to Completed if payments cover the effective amount
        if (payment.ContractId.HasValue)
        {
            try { await TryReconcileContractStatusAsync(payment.ContractId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to reconcile contract {ContractId} after payment creation", payment.ContractId); }
        }

        await db.Entry(payment).Reference(p => p.Patient).LoadAsync();
        await db.Entry(payment).Reference(p => p.Doctor).LoadAsync();
        // Load Invoice navigation for mapping
        if (payment.InvoiceId.HasValue)
            await db.Entry(payment).Reference(p => p.Invoice).LoadAsync();

        var dto = FinanceMappers.MapPayment(payment);

        // Notify accountants and admins — fire-and-forget replaced with direct await
        // to avoid DbContext concurrent operation. If the notification service shares
        // the same DI scope, running on a different thread via Task.Run could access
        // a disposed DbContext or cause concurrent access. Instead, we await the
        // notification after the financial transaction is fully committed. If the
        // notification fails, we log a warning but do not fail the payment.
        try
        {
            var patientName = dto.PatientName ?? "مريض";
            var amountStr = req.Amount.ToString("N0");
            var msg = $"تم استلام دفعة {amountStr} ر.ي من {patientName}";
            await notifications.NotifyRoleAsync("Accountant", "payment", "دفعة جديدة", msg, "Payment", payment.Id);
            await notifications.NotifyRoleAsync("Admin", "payment", "دفعة جديدة", msg, "Payment", payment.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[FinanceService] Payment notification failed after payment {PaymentId} — payment is still saved", payment.Id);
        }

        return dto;
    }

    public async Task<PaymentDto?> UpdatePaymentAsync(Guid id, UpdatePaymentRequest req)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null) return null;
        if (!payment.IsActive)
            throw new ArgumentException("لا يمكن تعديل دفعة محذوفة");

        // Phase 0B: Financial integrity — Amount, PaymentMethod, and PaymentDate
        // are locked after creation because they affect CashFlowTransaction, Treasury,
        // and CashierSession reconciliation. Changing them would corrupt the ledger.
        if (req.Amount.HasValue && req.Amount.Value != payment.Amount)
            throw new ArgumentException("لا يمكن تعديل مبلغ الدفعة بعد إنشائها. احذف الدفعة وأنشئ واحدة جديدة.");
        if (req.PaymentMethod != null && !string.Equals(req.PaymentMethod, payment.PaymentMethod, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("لا يمكن تغيير طريقة الدفع بعد إنشاء الدفعة. احذف الدفعة وأنشئ واحدة جديدة.");
        if (!string.IsNullOrWhiteSpace(req.PaymentDate) && DateOnly.TryParse(req.PaymentDate, out var pd) && pd != payment.PaymentDate)
            throw new ArgumentException("لا يمكن تغيير تاريخ الدفعة بعد إنشائها. احذف الدفعة وأنشئ واحدة جديدة.");

        // Safe to update: metadata fields only
        if (req.ServiceDescription != null) payment.ServiceDescription = req.ServiceDescription;
        if (req.Specialty != null)     payment.Specialty          = req.Specialty;
        if (req.DoctorId.HasValue)     payment.DoctorId           = req.DoctorId;
        if (req.Notes != null)         payment.Notes              = req.Notes;

        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await db.Entry(payment).Reference(p => p.Patient).LoadAsync();
        await db.Entry(payment).Reference(p => p.Doctor).LoadAsync();
        return FinanceMappers.MapPayment(payment);
    }

    public async Task<bool> DeletePaymentAsync(Guid id)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null) return false;

        if (await db.PaymentAllocations.AnyAsync(a => a.PaymentId == id))
            throw new ArgumentException("Cannot delete an advance payment while it is allocated to an invoice. Release its allocations first.");

        var invoiceId  = payment.InvoiceId;  // H3: capture before deactivation
        var contractId = payment.ContractId; // capture before deactivation

        var userId = currentUser.UserId;

        // Phase 0B: Guard — do not corrupt a closed or reconciled session by removing
        // a payment whose cashflow was part of its reconciliation calculation.
        var linkedCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == payment.Id && t.Category == FinancialCategory.PatientPayment && t.IsActive);
        if (linkedCashflow?.CashierSessionId != null)
        {
            var linkedSession = await db.CashierSessions.FindAsync(linkedCashflow.CashierSessionId.Value);
            if (linkedSession != null && linkedSession.Status != SessionStatus.Open)
            {
                throw new ArgumentException("لا يمكن حذف دفعة مرتبطة بوردية مقفلة أو مطابقة. تواصل مع المحاسب.");
            }
        }

        payment.IsActive  = false;
        payment.DeletedAt = DateTime.UtcNow;
        payment.DeletedBy = userId;

        // C3: Instead of soft-deleting the linked CashFlowTransaction, create a reversal entry.
        // CashFlowTransaction entries are immutable — they MUST NEVER be soft-deleted for
        // financial ledger integrity. The reversal creates an opposite entry and links
        // it to the original via ReversalOfTransactionId / ReversedByTransactionId.
        if (linkedCashflow != null)
        {
            var reversalCashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-REV-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Type = linkedCashflow.Type == TransactionType.Inflow ? TransactionType.Outflow : TransactionType.Inflow,
                Category = FinancialCategory.Reversal,
                Amount = linkedCashflow.Amount,
                PaymentMethod = linkedCashflow.PaymentMethod,
                TransactionDate = ClinicTimeProvider.ClinicToday(),
                ReferenceId = payment.Id,
                ReferenceNumber = linkedCashflow.ReferenceNumber,
                Description = $"قيد عكسي لحذف دفعة - {linkedCashflow.Description}",
                PerformedBy = userId ?? Guid.Empty,
                BranchId = linkedCashflow.BranchId,
                CashierSessionId = linkedCashflow.CashierSessionId,
                IsReversal = true,
                ReversalOfTransactionId = linkedCashflow.Id
            };
            db.CashFlowTransactions.Add(reversalCashflow);

            // Link original to the reversal
            linkedCashflow.ReversedByTransactionId = reversalCashflow.Id;
            // Keep original's IsActive = true (never soft-delete CashFlowTransactions)
        }

        // Finance V3: True atomic dual-write — start transaction BEFORE any entity mutation
        var useDeleteTx = db.Database.IsRelational();
        var deleteTx = useDeleteTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            await FinanceLedgerWriter.UpdateTreasuryBalanceNoSaveAsync(db, payment.BranchId ?? Guid.Empty, -payment.Amount, payment.PaymentMethod);
            await FinanceLedgerWriter.DualWriteReversalEntryAsync(db, journalEntryService, logger, currentUser.UserId ?? Guid.Empty, payment.Id, "حذف دفعة");
            await db.SaveChangesAsync();
            if (useDeleteTx) await deleteTx!.CommitAsync();
        }
        catch
        {
            if (useDeleteTx) await deleteTx!.RollbackAsync();
            throw;
        }

        // H3 FIX: Re-evaluate invoice status after deleting a payment.
        if (invoiceId.HasValue)
        {
            try { await TryMarkInvoicePaidAsync(invoiceId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "H3: Failed to re-evaluate invoice {InvoiceId} after payment deletion", invoiceId); }
            // Re-trigger proportional commission for OnPaymentCollection services
            try { await commissionService.TriggerOnPaymentCommissionsAsync(invoiceId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "OnPaymentCollection commission trigger failed for invoice {InvoiceId} after payment deletion", invoiceId); }
        }

        // Re-evaluate contract status (Completed → Active if paid total drops below effective amount)
        if (contractId.HasValue)
        {
            try { await TryReconcileContractStatusAsync(contractId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to reconcile contract {ContractId} after payment deletion", contractId); }
        }

        return true;
    }

    public async Task<PaymentDto?> RefundPaymentAsync(Guid id, string? reason, decimal? partialAmount = null)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null || !payment.IsActive) return null;

        if (await db.PaymentAllocations.AnyAsync(a => a.PaymentId == id))
            throw new ArgumentException("Cannot refund an advance payment while it is allocated to an invoice. Release its allocations first.");

        // H1: Partial refund validation
        var refundAmount = partialAmount.HasValue && partialAmount.Value > 0 && partialAmount.Value < payment.Amount
            ? partialAmount.Value
            : payment.Amount; // Full refund if null or >= original

        if (partialAmount.HasValue && (partialAmount.Value <= 0 || partialAmount.Value > payment.Amount))
            throw new ArgumentException("يجب أن يكون مبلغ الاسترداد الجزئي أكبر من الصفر ولا يتجاوز مبلغ الدفعة الأصلية.");

        var isPartialRefund = refundAmount < payment.Amount;

        // Sprint 14: Strengthened idempotency guard.
        //
        // The original Phase 0B guard only blocked an EXACT duplicate full refund
        // (`Amount == -payment.Amount` with `ServiceDescription` starting `"استرداد:"`).
        // That left two real holes:
        //   1. A partial refund (`"استرداد جزئي ..."`) was never matched, so a user could
        //      issue partial refunds repeatedly and exceed the original payment amount.
        //   2. A full refund issued after a prior partial refund was NOT blocked (the
        //      prior partial refund has `Amount != -payment.Amount`), so a 10,000 payment
        //      refunded 5,000 then 10,000 would total 15,000 of refunds on a 10,000 payment.
        //
        // The new guard sums ALL prior refunds against this payment (both full and partial,
        // matched by the existing (PatientId, ContractId, InvoiceId, ServiceDescription prefix)
        // heuristic) and rejects when the cumulative refund + new refund would exceed the
        // original payment amount. The error message retains the `استرداد هذه الدفعة مسبقاً`
        // substring so the existing Phase 0B test (`RefundPaymentAsync_DoubleRefund_ThrowsArgumentException`)
        // and any UI string-match against it keep working.
        var priorRefundedTotal = await db.Payments
            .Where(p => p.IsActive && p.Amount < 0
                && p.ServiceDescription != null
                && (p.ServiceDescription.StartsWith("استرداد:") || p.ServiceDescription.StartsWith("استرداد جزئي"))
                && p.ContractId == payment.ContractId
                && p.InvoiceId == payment.InvoiceId
                && p.PatientId == payment.PatientId)
            .SumAsync(p => (decimal?)(-p.Amount)) ?? 0m;

        if (priorRefundedTotal > 0m && refundAmount + priorRefundedTotal > payment.Amount)
            throw new ArgumentException(
                "تم استرداد هذه الدفعة مسبقاً أو أن مجموع المبالغ المستردة يتجاوز مبلغ الدفعة الأصلية. " +
                "لا يمكن استرداد نفس الدفعة مرتين أو تجاوز قيمتها الأصلية.");

        // Require active open cashier session for refund payouts
        var userId = currentUser.UserId ?? Guid.Empty;
        var activeSession = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
        if (activeSession == null)
            throw new ArgumentException("عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل إجراء أي عمليات استرداد للدفعة.");

        // BranchId guard: must have a valid branch assignment before processing a refund
        if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
            throw new ArgumentException("عذراً، يجب تحديد الفرع قبل إجراء أي عمليات استرداد للدفعة.");

        var refund = new Payment
        {
            PatientId          = payment.PatientId,
            ContractId         = payment.ContractId,
            InvoiceId          = payment.InvoiceId,
            Amount             = -refundAmount,
            Currency           = FinanceMappers.NormalizeCurrency(payment.Currency),
            AccountCurrency    = FinanceMappers.NormalizeCurrency(payment.AccountCurrency),
            ExchangeRateToAccountCurrency = payment.ExchangeRateToAccountCurrency == 0 ? 1m : payment.ExchangeRateToAccountCurrency,
            ExchangeRateToYer  = payment.ExchangeRateToYer,
            AppliedAmount      = -Math.Round(refundAmount * (payment.ExchangeRateToAccountCurrency == 0 ? 1m : payment.ExchangeRateToAccountCurrency), 2, MidpointRounding.AwayFromZero),
            ExchangeRateSource = payment.ExchangeRateSource,
            PaymentDate        = ClinicTimeProvider.ClinicToday(),
            PaymentMethod      = payment.PaymentMethod,
            ServiceDescription = isPartialRefund
                ? $"استرداد جزئي ({refundAmount:N0}): {payment.ServiceDescription ?? payment.ReceiptNumber}"
                : $"استرداد: {payment.ServiceDescription ?? payment.ReceiptNumber}",
            Specialty          = payment.Specialty,
            DoctorId           = payment.DoctorId,
            BranchId           = payment.BranchId,
            ReceivedBy         = currentUser.UserId,
            ReceiptNumber = await FinanceLedgerWriter.GenerateRefundReceiptNumberAsync(db),
            Notes              = reason
        };

        db.Payments.Add(refund);

        // Auto-create central ledger cashflow transaction (Outflow / Refund)
        var refundDatePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var refundCashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{refundDatePart}-REF-{refund.ReceiptNumber?[4..] ?? Guid.NewGuid().ToString()[..8]}",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.Refund,
            Amount = refundAmount, // the actual refund amount (positive for outflow recording)
            Currency = FinanceMappers.NormalizeCurrency(payment.Currency),
            PaymentMethod = refund.PaymentMethod ?? "cash",
            TransactionDate = refund.PaymentDate,
            ReferenceId = refund.Id,
            ReferenceNumber = refund.ReceiptNumber,
            Description = isPartialRefund
                ? $"استرداد جزئي ({refundAmount:N0}) دفعة مريض - سند قبض {refund.ReceiptNumber}"
                : $"استرداد دفعة مريض - سند قبض {refund.ReceiptNumber}",
            PerformedBy = userId,
            BranchId = currentUser.BranchId.Value,
            CashierSessionId = activeSession.Id
        };
        db.CashFlowTransactions.Add(refundCashflow);

        // Finance V3: True atomic dual-write — start transaction BEFORE any entity mutation
        var useRefundTx = db.Database.IsRelational();
        var refundTx = useRefundTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            await FinanceLedgerWriter.UpdateTreasuryBalanceNoSaveAsync(db, refund.BranchId ?? Guid.Empty, refund.Amount, refund.PaymentMethod, refund.Currency);
            await FinanceLedgerWriter.DualWriteRefundEntryAsync(db, journalEntryService, payment, refund, refundAmount);
            await db.SaveChangesAsync();
            if (useRefundTx) await refundTx!.CommitAsync();
        }
        catch
        {
            if (useRefundTx) await refundTx!.RollbackAsync();
            throw;
        }

        // H3 FIX: Re-evaluate invoice status after creating a refund.
        if (payment.InvoiceId.HasValue)
        {
            try { await TryMarkInvoicePaidAsync(payment.InvoiceId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "H3: Failed to re-evaluate invoice {InvoiceId} after refund", payment.InvoiceId); }
            // Re-trigger proportional commission for OnPaymentCollection services
            try { await commissionService.TriggerOnPaymentCommissionsAsync(payment.InvoiceId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "OnPaymentCollection commission trigger failed for invoice {InvoiceId} after refund", payment.InvoiceId); }
        }

        // Re-evaluate contract status after refund (Completed → Active if paid total drops below effective amount)
        if (payment.ContractId.HasValue)
        {
            try { await TryReconcileContractStatusAsync(payment.ContractId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to reconcile contract {ContractId} after refund", payment.ContractId); }
        }

        // Sprint 14: Best-effort audit-log warning that commission may need manual adjustment.
        //
        // Audit finding: "Refund does not automatically reverse commission or clearly audit it."
        //
        // Why we don't auto-reverse: commission recognition has two designed tracks
        // (see CLAUDE.md):
        //   - Accrual mode (`CommissionRecognitionMode != OnPaymentCollection`): commission is
        //     recognized on invoice issuance. The OnPaymentCollection trigger above does NOT
        //     touch accrual-mode line items, so Approved/Paid commissions on accrual-mode
        //     services are NOT reversed by this refund. Auto-reversing them here would break
        //     accounting (Paid commissions have already been disbursed via DoctorCommissionPayment
        //     + Treasury outflow + JournalEntry — reversing requires reversal entries, treasury
        //     top-ups, and possibly already-closed cashier sessions).
        //   - OnPaymentCollection mode: commission IS re-proportioned by the trigger above based
        //     on the new (lower) collected total — that's the correct behavior and we leave it alone.
        //
        // The safe, accounting-preserving action is to write a clear Arabic audit-log entry
        // flagging that manual review may be needed for Approved/Paid accrual-mode commissions
        // tied to this invoice/contract. The owner / accountant can then decide per-case whether
        // to issue a manual commission adjustment (via the existing Unlock → Recalculate flow).
        //
        // This is best-effort: the refund transaction has already committed, so an audit-write
        // failure here must NOT roll back the refund. We log + swallow.
        try
        {
            await LogCommissionAdjustmentWarningAsync(payment, refund, refundAmount, isPartialRefund);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Sprint 14: Failed to write commission-adjustment audit warning for refund {RefundId} of payment {PaymentId}",
                refund.Id, payment.Id);
        }

        await db.Entry(refund).Reference(p => p.Patient).LoadAsync();
        await db.Entry(refund).Reference(p => p.Doctor).LoadAsync();
        return FinanceMappers.MapPayment(refund);
    }

    /// <summary>
    /// Sprint 14: Writes an Arabic audit-log entry warning that a refund may require manual
    /// commission adjustment. See `RefundPaymentAsync` for the rationale (no auto-reversal).
    ///
    /// Uses a distinct resource name (`"PaymentRefund.CommissionAdjustment"`) so finance
    /// managers can filter the audit log for refund-driven commission warnings separately
    /// from the primary refund audit (resource `"PaymentRefund"`, written by the controller).
    ///
    /// The payload snapshots whether any OnPaymentCollection commission line items were in
    /// Approved or Paid status at refund time — that's the risky case where
    /// `TriggerOnPaymentCommissionsAsync` (called above) silently re-proportions the
    /// commission via the dual-write path, and where accrual-mode Approved/Paid commissions
    /// are NOT touched at all. Finance staff can use this flag to prioritize which refunds
    /// to review first.
    /// </summary>
    private async Task LogCommissionAdjustmentWarningAsync(
        Payment originalPayment, Payment refundPayment, decimal refundAmount, bool isPartialRefund)
    {
        // Snapshot OnPaymentCollection commission line items BEFORE writing the audit, so the
        // accountant sees the state of commissions AT REFUND TIME (not after the trigger above
        // already re-proportioned them). For accrual-mode services we don't snapshot per-line
        // — there can be many, and the message is the same ("manual review required") — but we
        // do flag the high-risk case where any OnPaymentCollection item was Approved/Paid.
        var hasApprovedOrPaidOnPaymentCollection = false;
        var onPaymentCollectionItemsAffected = 0;
        if (originalPayment.InvoiceId.HasValue)
        {
            var affectedItems = await db.InvoiceLineItems
                .Include(i => i.Service)
                .Where(i => i.InvoiceId == originalPayment.InvoiceId.Value
                         && i.IsActive
                         && i.Service != null
                         && i.Service.CommissionRecognitionMode == CommissionRecognitionMode.OnPaymentCollection)
                .ToListAsync();
            onPaymentCollectionItemsAffected = affectedItems.Count;
            hasApprovedOrPaidOnPaymentCollection = affectedItems
                .Any(i => i.CommissionStatus == CommissionStatus.Approved
                       || i.CommissionStatus == CommissionStatus.Paid);
        }

        // Two warning variants — both must mention "العمولات" so finance staff can grep the
        // audit log. The high-risk variant (Approved/Paid commissions exist) is more urgent.
        string baseWarning = isPartialRefund
            ? "تم استرداد جزء من دفعة مرتبطة بفاتورة/عقد قد يحتوي على بنود عمولات. "
            : "تم استرداد دفعة كاملة مرتبطة بفاتورة/عقد قد يحتوي على بنود عمولات. ";

        const string autoReverseNote =
            "العمولات المستحقة على نمط التحصيل (OnPaymentCollection) تُعيد الاحتساب النسبي تلقائياً، " +
            "لكن العمولات المعتمدة أو المدفوعة على نمط الاستحقاق (Accrual) لا تُعكس تلقائياً. " +
            "لم يتم تنفيذ العكس التلقائي للعمولات بعد الاسترداد تجنبًا لاختلال المحاسبة التاريخية. " +
            "يجب على المحاسب/المالك مراجعة بنود العمولات لهذه الفاتورة وتعديلها يدوياً إن لزم.";

        var warning = hasApprovedOrPaidOnPaymentCollection
            ? "تحذير عالي الخطورة: " + baseWarning + "يوجد بنود عمولات معتمدة أو مدفوعة على نمط التحصيل قد تتأثر. " + autoReverseNote
            : baseWarning + autoReverseNote;

        var payload = new
        {
            warning,
            originalPaymentId                       = originalPayment.Id,
            refundPaymentId                         = refundPayment.Id,
            invoiceId                               = originalPayment.InvoiceId,
            contractId                              = originalPayment.ContractId,
            doctorId                                = originalPayment.DoctorId,
            patientId                               = originalPayment.PatientId,
            originalAmount                          = originalPayment.Amount,
            refundAmount,
            isPartialRefund,
            reason                                  = refundPayment.Notes,
            hasApprovedOrPaidOnPaymentCollection,
            onPaymentCollectionItemsAffected,
        };

        db.AuditLogs.Add(new AuditLog
        {
            UserId     = currentUser.UserId,
            Action     = AuditAction.Refund,
            Resource   = "PaymentRefund.CommissionAdjustment",
            ResourceId = refundPayment.Id,
            NewData    = System.Text.Json.JsonSerializer.SerializeToDocument(payload),
        });
        await db.SaveChangesAsync();
    }

    // ─── Private helpers (moved verbatim from FinanceService) ──────────────

    private static string ResolveAccountCurrency(string? requestedCurrency, Invoice? invoice, Contract? contract)
    {
        if (invoice != null) return FinanceMappers.NormalizeCurrency(invoice.Currency);
        if (contract != null) return FinanceMappers.NormalizeCurrency(contract.Currency);
        return FinanceMappers.NormalizeCurrency(requestedCurrency);
    }

    private async Task<FxSnapshot> ResolveFxSnapshotAsync(
        string paymentCurrency,
        string accountCurrency,
        decimal? directPaymentToAccountRate,
        decimal? directPaymentToYerRate)
    {
        paymentCurrency = FinanceMappers.NormalizeCurrency(paymentCurrency);
        accountCurrency = FinanceMappers.NormalizeCurrency(accountCurrency);

        if (directPaymentToYerRate.HasValue && directPaymentToYerRate.Value <= 0m)
            throw new ArgumentException("Payment currency rate to YER must be greater than zero.");
        if (directPaymentToAccountRate.HasValue && directPaymentToAccountRate.Value <= 0m)
            throw new ArgumentException("Payment-to-account exchange rate must be greater than zero.");

        if (paymentCurrency == accountCurrency)
        {
            var sameCurrencyRate = directPaymentToYerRate ?? await GetCurrencyToYerRateAsync(paymentCurrency);
            return new FxSnapshot(1m, sameCurrencyRate, sameCurrencyRate);
        }

        if (directPaymentToAccountRate.HasValue)
        {
            var paymentToYer = directPaymentToYerRate
                ?? (paymentCurrency == FinanceMappers.BaseCurrency
                    ? 1m
                    : accountCurrency == FinanceMappers.BaseCurrency
                        ? directPaymentToAccountRate.Value
                        : await GetCurrencyToYerRateAsync(paymentCurrency));
            var accountToYer = Math.Round(
                paymentToYer / directPaymentToAccountRate.Value,
                6,
                MidpointRounding.AwayFromZero);
            if (accountToYer <= 0m)
                throw new ArgumentException("Account currency rate to YER is invalid.");

            return new FxSnapshot(directPaymentToAccountRate.Value, paymentToYer, accountToYer);
        }

        var configuredPaymentToYer = directPaymentToYerRate ?? await GetCurrencyToYerRateAsync(paymentCurrency);
        var configuredAccountToYer = await GetCurrencyToYerRateAsync(accountCurrency);
        if (configuredAccountToYer <= 0m)
            throw new ArgumentException("Account currency rate to YER is invalid.");

        return new FxSnapshot(
            Math.Round(configuredPaymentToYer / configuredAccountToYer, 6, MidpointRounding.AwayFromZero),
            configuredPaymentToYer,
            configuredAccountToYer);
    }

    private async Task<decimal> ResolveExchangeRateAsync(string paymentCurrency, string accountCurrency, decimal? directRate)
    {
        paymentCurrency = FinanceMappers.NormalizeCurrency(paymentCurrency);
        accountCurrency = FinanceMappers.NormalizeCurrency(accountCurrency);
        if (paymentCurrency == accountCurrency) return 1m;
        if (directRate.HasValue && directRate.Value > 0m) return directRate.Value;

        var paymentToYer = await GetCurrencyToYerRateAsync(paymentCurrency);
        var accountToYer = await GetCurrencyToYerRateAsync(accountCurrency);
        if (accountToYer <= 0m) throw new ArgumentException("سعر صرف عملة الحساب غير صالح");
        return Math.Round(paymentToYer / accountToYer, 6, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> GetCurrencyToYerRateAsync(string currency)
    {
        currency = FinanceMappers.NormalizeCurrency(currency);
        if (currency == FinanceMappers.BaseCurrency) return 1m;

        var key = $"finance.exchange_rate.{currency}_YER";
        var value = await db.Settings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0m)
            return parsed;

        throw new ArgumentException($"لا يوجد سعر صرف معتمد للعملة {currency}. حدده من إعدادات المالية قبل تسجيل الدفعة.");
    }

    private sealed record FxSnapshot(
        decimal PaymentToAccountCurrency,
        decimal PaymentCurrencyToYer,
        decimal AccountCurrencyToYer);

    /// <summary>
    /// Checks if total active payments for a contract cover its effective amount (TotalAmount - DiscountAmount).
    /// Handles both directions:
    ///   - Active → Completed (when payments cover the effective amount)
    ///   - Completed → Active (when payments are deleted/refunded and no longer cover the effective amount)
    /// Skips Cancelled contracts. Safe to call after payment creation, deletion, or refund.
    /// </summary>
    private async Task TryReconcileContractStatusAsync(Guid contractId)
    {
        var contract = await db.Contracts
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) return;
        if (contract.Status == ContractStatus.Cancelled) return;

        var effectiveAmount = contract.TotalAmount - contract.DiscountAmount;
        var totalPaid = contract.Payments
            .Where(p => p.IsActive)
            .Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount);

        if (contract.Status == ContractStatus.Active && totalPaid >= effectiveAmount && effectiveAmount > 0)
        {
            contract.Status    = ContractStatus.Completed;
            contract.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        else if (contract.Status == ContractStatus.Completed && totalPaid < effectiveAmount)
        {
            contract.Status    = ContractStatus.Active;
            contract.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Checks if total payments for an invoice cover its TotalAmount.
    /// H3 FIX: Now handles BOTH directions:
    ///   - Issued → Paid (when payments cover the total)
    ///   - Paid → Issued (when payments are deleted/refunded and no longer cover total)
    /// Safe to call after payment creation, deletion, or refund.
    /// </summary>
    public async Task TryMarkInvoicePaidAsync(Guid invoiceId)
    {
        var invoice = await db.Invoices.FindAsync(invoiceId);
        if (invoice == null) return;

        // Only re-evaluate Issued and Paid invoices
        if (invoice.Status != InvoiceStatus.Issued && invoice.Status != InvoiceStatus.Paid) return;

        var totalPaid = await db.Payments
            .Where(p => p.InvoiceId == invoiceId && p.IsActive)
            .SumAsync(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount);
        var advanceAllocated = await db.PaymentAllocations
            .Where(a => a.InvoiceId == invoiceId && a.IsActive)
            .SumAsync(a => (decimal?)a.Amount) ?? 0m;
        totalPaid += advanceAllocated;

        if (invoice.Status == InvoiceStatus.Issued && totalPaid >= invoice.TotalAmount)
        {
            // Issued → Paid
            invoice.Status = InvoiceStatus.Paid;
            invoice.UpdatedBy = currentUser.UserId;
            invoice.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        else if (invoice.Status == InvoiceStatus.Paid && totalPaid < invoice.TotalAmount)
        {
            // H3 FIX: Paid → Issued (payment was deleted/refunded)
            invoice.Status = InvoiceStatus.Issued;
            invoice.UpdatedBy = currentUser.UserId;
            invoice.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
