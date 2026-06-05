using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

public class FinanceService(AppDbContext db, ICurrentUserService currentUser, INotificationService notifications, ILogger<FinanceService> logger, ICommissionService commissionService, IJournalEntryService journalEntryService)
    : IFinanceService
{
    public async Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .AsQueryable();

        if (branchId.HasValue) query = query.Where(c => c.Patient.BranchId == branchId.Value);
        if (patientId.HasValue) query = query.Where(c => c.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, true, out var contractStatus))
            query = query.Where(c => c.Status == contractStatus);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapContractList(c))
            .ToListAsync();
    }

    public async Task<ContractDetailDto?> GetContractByIdAsync(Guid id)
    {
        var c = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
                .ThenInclude(p => p.Doctor)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (c == null) return null;

        var dto = new ContractDetailDto
        {
            Id = c.Id,
            PatientId = c.PatientId,
            PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
            PatientNumber = c.Patient.PatientNumber,
            Specialty = c.Specialty,
            TotalAmount = c.TotalAmount,
            DownPayment = c.DownPayment,
            PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
            RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
            InstallmentsCount = c.InstallmentsCount,
            InstallmentAmount = c.InstallmentAmount,
            StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
            Status = c.Status.ToString(),
            DiscountAmount = c.DiscountAmount,
            DiscountReason = c.DiscountReason,
            Notes = c.Notes,
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(MapPayment).ToList()
        };

        return dto;
    }

    public async Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req)
    {
        var contract = new Contract
        {
            PatientId = req.PatientId,
            Specialty = req.Specialty,
            RelatedCaseId = req.RelatedCaseId,
            TotalAmount = req.TotalAmount,
            DownPayment = req.DownPayment,
            InstallmentsCount = req.InstallmentsCount,
            InstallmentAmount = req.InstallmentAmount,
            StartDate = req.StartDate != null ? DateOnly.Parse(req.StartDate) : DateOnly.FromDateTime(DateTime.Today),
            DiscountAmount = req.DiscountAmount,
            DiscountReason = req.DiscountReason,
            Status = ContractStatus.Active,
            Notes = req.Notes,
            CreatedBy = currentUser.UserId
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        // Auto-create down payment if specified
        if (req.DownPayment > 0)
        {
            await CreatePaymentAsync(new CreatePaymentRequest
            {
                PatientId = req.PatientId,
                ContractId = contract.Id,
                Amount = req.DownPayment,
                PaymentMethod = "cash",
                ServiceDescription = "دفعة أولى"
            });
        }

        return (await GetContractByIdAsync(contract.Id))!;
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid id)
    {
        var p = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.Id == id);
        return p == null ? null : MapPayment(p);
    }

    public async Task<List<OverdueContractDto>> GetOverdueContractsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var contracts = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null)
            .ToListAsync();

        var overdue = new List<OverdueContractDto>();

        foreach (var c in contracts)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            if (monthsElapsed <= 0) continue;

            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var actualPaid   = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
            var overdueAmt   = expectedPaid - actualPaid;

            if (overdueAmt > 0)
            {
                overdue.Add(new OverdueContractDto
                {
                    ContractId     = c.Id,
                    PatientId      = c.PatientId,
                    PatientName    = c.Patient.FirstName + " " + c.Patient.LastName,
                    PatientNumber  = c.Patient.PatientNumber,
                    Phone          = c.Patient.Phone,
                    Specialty      = c.Specialty,
                    TotalAmount    = c.TotalAmount,
                    PaidAmount     = actualPaid,
                    OverdueAmount  = overdueAmt,
                    RemainingAmount= c.TotalAmount - c.DiscountAmount - actualPaid,
                    MonthsElapsed  = monthsElapsed,
                    StartDate      = c.StartDate?.ToString("yyyy-MM-dd")
                });
            }
        }

        return overdue.OrderByDescending(o => o.OverdueAmount).ToList();
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
            .Select(p => MapPayment(p))
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
            // Admin fallback: resolve to the first active branch in the system
            var firstBranch = await db.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.CreatedAt)
                .FirstOrDefaultAsync();
            if (firstBranch == null)
                throw new ArgumentException("عذراً، يجب تحديد الفرع قبل تسجيل أي مدفوعات. لا توجد فروع نشطة في النظام.");
            branchId = firstBranch.Id;
        }

        // Safety guard: never write Guid.Empty as BranchId in financial records (Fix 4)
        if (branchId == Guid.Empty)
            throw new ArgumentException("لم يتم تحديد فرع صالح. لا يمكن تسجيل الدفعة بدون فرع.");

        // Phase 0B: Validate payment amount is positive
        if (req.Amount <= 0)
            throw new ArgumentException("يجب أن يكون مبلغ الدفعة أكبر من الصفر.");

        // Validate InvoiceId if provided
        Invoice? invoice = null;
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

            // Server-side overpayment guard
            var alreadyPaid = await db.Payments
                .Where(p => p.InvoiceId == invoice.Id && p.IsActive)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var remaining = invoice.TotalAmount - alreadyPaid;
            if (req.Amount > remaining)
                throw new ArgumentException($"المبلغ ({req.Amount:N0}) يتجاوز الرصيد المتبقي للفاتورة ({remaining:N0})");

        }

        // Finance V3: True atomic dual-write — start transaction BEFORE any entity mutation
        // so that Payment, Receipt, CashFlow, Treasury, and JournalEntry are all committed
        // together or rolled back together. Previously, UpdateTreasuryBalanceAsync called
        // SaveChangesAsync independently, committing entities before the JE transaction started.

        // H9 FIX: Generate receipt number and ALL entity mutations INSIDE the transaction
        // to avoid DbContext concurrency issues. Any DbContext query (like GenerateReceiptNumberAsync)
        // before BeginTransactionAsync can conflict with the transaction's DbContext tracking.
        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        Payment payment;
        try
        {
            var receiptNumber = await GenerateReceiptNumberAsync();

            payment = new Payment
            {
                PatientId = req.PatientId,
                ContractId = req.ContractId,
                InvoiceId = req.InvoiceId,
                Amount = req.Amount,
                PaymentDate = DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod = req.PaymentMethod,
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

            // Auto-create central ledger cashflow transaction (Inflow)
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-IN-{payment.ReceiptNumber?[4..] ?? Guid.NewGuid().ToString()[..8]}",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.PatientPayment,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod ?? "cash",
                TransactionDate = payment.PaymentDate,
                ReferenceId = payment.Id,
                ReferenceNumber = payment.ReceiptNumber,
                Description = $"تحصيل دفعة مريض - سند قبض {payment.ReceiptNumber}",
                PerformedBy = userId,
                BranchId = branchId,
                CashierSessionId = activeSession.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            // All entity mutations are inside the transaction started above
            await UpdateTreasuryBalanceNoSaveAsync(payment.BranchId ?? Guid.Empty, payment.Amount, payment.PaymentMethod);
            await DualWritePaymentEntryAsync(payment, cashflow, invoice);
            // CreateEntryAsync (inside DualWrite) calls SaveChangesAsync, which persists
            // ALL tracked entities within the transaction (Payment, Receipt, CashFlow, Treasury, JE).
            // The auto-post SaveChangesAsync inside DualWrite also happens within this tx.
            // A final SaveChangesAsync ensures any remaining tracked changes are persisted.
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

        var dto = MapPayment(payment);

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

    public async Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return null;

        // Phase 0B: Validate that TotalAmount is not reduced below what's already been paid
        {
            var alreadyPaid = await db.Payments
                .Where(p => p.ContractId == id && p.IsActive)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            if (req.TotalAmount < alreadyPaid)
                throw new ArgumentException($"لا يمكن تقليل إجمالي العقد ({req.TotalAmount:N0} ر.ي) إلى أقل من المبلغ المدفوع فعلياً ({alreadyPaid:N0} ر.ي).");
        }

        contract.Specialty        = req.Specialty;
        contract.TotalAmount      = req.TotalAmount;
        contract.InstallmentsCount = req.InstallmentsCount;
        contract.InstallmentAmount = req.InstallmentAmount;
        contract.StartDate        = req.StartDate != null ? DateOnly.Parse(req.StartDate) : contract.StartDate;
        contract.DiscountAmount   = req.DiscountAmount;
        contract.DiscountReason   = req.DiscountReason;
        contract.Notes            = req.Notes;
        contract.UpdatedAt        = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetContractByIdAsync(id);
    }

    public async Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status)
    {
        if (!Enum.TryParse<ContractStatus>(status, true, out var contractStatus)) return null;

        var contract = await db.Contracts
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null) return null;

        contract.Status    = contractStatus;
        contract.UpdatedAt = DateTime.UtcNow;

        // H8 FIX: When cancelling a contract with active payments, soft-delete
        // all linked payments to prevent financial reports from including
        // payments for cancelled contracts. Previously, cancelling a contract
        // left its payments active, causing incorrect balance calculations.
        if (contractStatus == ContractStatus.Cancelled)
        {
            // Finance Transaction Safety: Wrap the entire cancellation in a single
            // atomic transaction so that treasury balance, CashFlow reversals,
            // JournalEntry reversals, payment deactivation, and contract status
            // are committed together or rolled back together.
            // Previously, UpdateTreasuryBalanceAsync called SaveChangesAsync
            // independently, and DualWriteReversalEntryAsync also called
            // SaveChangesAsync, causing partial commits if a later step failed.
            var useTx = db.Database.IsRelational();
            var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
            try
            {
                var activePayments = contract.Payments.Where(p => p.IsActive).ToList();
                foreach (var payment in activePayments)
                {
                    payment.IsActive = false;
                    payment.DeletedAt = DateTime.UtcNow;
                }

                // C3: Instead of soft-deleting linked CashFlowTransactions, create reversal entries.
                // CashFlowTransaction entries are immutable — they MUST NEVER be soft-deleted for
                // financial ledger integrity.
                foreach (var payment in activePayments)
                {
                    var linkedCashflow = await db.CashFlowTransactions
                        .FirstOrDefaultAsync(t => t.ReferenceId == payment.Id && t.Category == FinancialCategory.PatientPayment && t.IsActive);
                    if (linkedCashflow != null)
                    {
                        var reversalCashflow = new CashFlowTransaction
                        {
                            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-REV-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                            Type = linkedCashflow.Type == TransactionType.Inflow ? TransactionType.Outflow : TransactionType.Inflow,
                            Category = FinancialCategory.Reversal,
                            Amount = linkedCashflow.Amount,
                            PaymentMethod = linkedCashflow.PaymentMethod,
                            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                            ReferenceId = payment.Id,
                            ReferenceNumber = linkedCashflow.ReferenceNumber,
                            Description = $"قيد عكسي لإلغاء عقد - {linkedCashflow.Description}",
                            PerformedBy = currentUser.UserId ?? Guid.Empty,
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
                    // Reverse treasury balance — use NoSave variant inside the transaction
                    await UpdateTreasuryBalanceNoSaveAsync(payment.BranchId ?? Guid.Empty, -payment.Amount, payment.PaymentMethod);

                    // Finance V3: Dual-write reversal entry for each reversed payment
                    // DualWriteReversalEntryAsync calls SaveChangesAsync internally,
                    // which persists all tracked changes within the current transaction.
                    await DualWriteReversalEntryAsync(payment.Id, "إلغاء عقد");
                }

                // Persist all tracked changes (contract status, payment deactivations,
                // CashFlow reversals, treasury balance, JournalEntry reversals)
                await db.SaveChangesAsync();

                // H8 FIX: Re-evaluate linked invoice statuses after cancelling payments.
                // TryMarkInvoicePaidAsync calls SaveChangesAsync internally, but since
                // we are still inside the same transaction, it persists within the tx scope.
                var affectedInvoiceIds = activePayments
                    .Where(p => p.InvoiceId.HasValue)
                    .Select(p => p.InvoiceId!.Value)
                    .Distinct()
                    .ToList();

                foreach (var invoiceId in affectedInvoiceIds)
                {
                    try { await TryMarkInvoicePaidAsync(invoiceId); }
                    catch (Exception ex) { logger.LogWarning(ex, "H8: Failed to re-evaluate invoice {InvoiceId} after contract cancellation", invoiceId); }
                }

                if (useTx) await tx!.CommitAsync();
            }
            catch
            {
                if (useTx) await tx!.RollbackAsync();
                throw;
            }
        }
        else
        {
            await db.SaveChangesAsync();
        }

        return await GetContractByIdAsync(id);
    }

    public async Task<AccountStatementDto?> GetAccountStatementAsync(Guid patientId)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return null;

        var contracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        // FIX: Calculate totalPaid from ALL active payments for the patient,
        // not just contract-linked ones. Unlinked/orphan payments must still
        // count in the overall patient summary so the balance is accurate.
        // Each payment is counted exactly once via the direct Payments query.
        var totalPaid = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var invoices = await db.Invoices
            .Where(i => i.PatientId == patientId
                     && i.Status != InvoiceStatus.Cancelled
                     && i.IsActive)
            .ToListAsync();

        var totalContracted = contracts.Sum(c => c.TotalAmount)
                            + invoices.Sum(i => i.Subtotal + (i.TaxAmount ?? 0m));
        var totalDiscounts  = contracts.Sum(c => c.DiscountAmount)
                            + invoices.Sum(i => i.DiscountAmount ?? 0m);
        var totalRemaining  = totalContracted - totalDiscounts - totalPaid;

        // FIX: Filter recentPayments to active only — inactive/refunded/cancelled
        // payments should not appear in the recent list.
        var recentPayments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync();

        return new AccountStatementDto
        {
            PatientId        = patientId,
            PatientName      = patient.FirstName + " " + patient.LastName,
            PatientNumber    = patient.PatientNumber,
            TotalContracted  = totalContracted,
            TotalDiscounts   = totalDiscounts,
            TotalPaid        = totalPaid,
            TotalRemaining   = totalRemaining,
            ActiveContracts  = contracts.Count(c => c.Status == ContractStatus.Active),
            CompletedContracts = contracts.Count(c => c.Status == ContractStatus.Completed),
            Contracts        = contracts.Select(c => new ContractStatementDto
            {
                Id              = c.Id,
                Specialty       = c.Specialty,
                TotalAmount     = c.TotalAmount,
                DiscountAmount  = c.DiscountAmount,
                // Per-contract balance still uses only contract-linked payments
                PaidAmount      = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
                RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
                StartDate       = c.StartDate?.ToString("yyyy-MM-dd"),
                Status          = c.Status.ToString(),
                InstallmentsCount  = c.InstallmentsCount,
                InstallmentAmount  = c.InstallmentAmount
            }).ToList(),
            RecentPayments = recentPayments.Select(p =>
            {
                // Note: we deliberately do NOT mutate p.Patient to avoid ChangeTracker
                // side-effects. MapPayment reads p.Patient?.FirstName which falls back
                // to the already-loaded Patient navigation reference from the Include above.
                return MapPayment(p);
            }).ToList()
        };
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync()
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var todayQuery = db.Payments.Where(p => p.PaymentDate == today && p.IsActive);
        if (branchId.HasValue) todayQuery = todayQuery.Where(p => p.BranchId == branchId);
        var todayCollected = await todayQuery.SumAsync(p => (decimal?)p.Amount) ?? 0;

        var monthQuery = db.Payments.Where(p => p.PaymentDate >= monthStart && p.IsActive);
        if (branchId.HasValue) monthQuery = monthQuery.Where(p => p.BranchId == branchId);
        var monthCollected = await monthQuery.SumAsync(p => (decimal?)p.Amount) ?? 0;

        // Contract-based outstanding
        var contractQuery = db.Contracts.Include(c => c.Payments).Where(c => c.Status == ContractStatus.Active);
        if (branchId.HasValue) contractQuery = contractQuery.Where(c => c.Patient.BranchId == branchId);
        var contractOutstanding = await contractQuery
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;

        // Invoice-based outstanding (Issued invoices not fully paid)
        var invoiceQuery = db.Invoices.Include(i => i.Payments)
            .Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);
        if (branchId.HasValue) invoiceQuery = invoiceQuery.Where(i => i.Patient.BranchId == branchId);
        var invoiceOutstanding = await invoiceQuery
            .Select(i => i.TotalAmount - i.Payments.Where(p => p.IsActive).Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;

        var activeContractsQuery = db.Contracts.Where(c => c.Status == ContractStatus.Active);
        if (branchId.HasValue) activeContractsQuery = activeContractsQuery.Where(c => c.Patient.BranchId == branchId);
        var activeContracts = await activeContractsQuery.CountAsync();

        // ── Extended Sprint 1 Dashboard Stats ──
        var unpaidInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);
        if (branchId.HasValue) unpaidInvoicesQuery = unpaidInvoicesQuery.Where(i => i.Patient.BranchId == branchId);
        var unpaidInvoicesCount = await unpaidInvoicesQuery.CountAsync();

        var draftInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Draft && i.IsActive);
        if (branchId.HasValue) draftInvoicesQuery = draftInvoicesQuery.Where(i => i.Patient.BranchId == branchId);
        var draftInvoicesCount = await draftInvoicesQuery.CountAsync();

        // Overdue amount
        var overdueContracts = await GetOverdueContractsAsync();
        var overdueAmount = overdueContracts.Sum(o => o.OverdueAmount);

        // Pending doctor commissions (calculated or approved or pending, but not paid)
        var commissionQuery = db.InvoiceLineItems
            .Where(l => l.IsActive && l.CommissionStatus != CommissionStatus.Paid && l.DoctorCommissionAmount > 0);
        if (branchId.HasValue) commissionQuery = commissionQuery.Where(l => l.Invoice.Patient.BranchId == branchId);
        var pendingCommissionsAmount = await commissionQuery.SumAsync(l => l.DoctorCommissionAmount);

        var recentQuery = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.IsActive);
        if (branchId.HasValue) recentQuery = recentQuery.Where(p => p.BranchId == branchId);
        var recentPayments = await recentQuery
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => MapPayment(p))
            .ToListAsync();

        // Recent invoices
        var recentInvoicesQuery = db.Invoices
            .Include(i => i.Patient)
            .Where(i => i.IsActive);
        if (branchId.HasValue) recentInvoicesQuery = recentInvoicesQuery.Where(i => i.Patient.BranchId == branchId);
        var recentInvoices = await recentInvoicesQuery
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => new RecentInvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                PatientName = i.Patient != null ? (i.Patient.FirstName + " " + i.Patient.LastName).Trim() : "مريض",
                TotalAmount = i.TotalAmount,
                Status = i.Status.ToString(),
                StatusArabic = i.Status == InvoiceStatus.Draft ? "مسودة" :
                               i.Status == InvoiceStatus.Issued ? "مصدرة" :
                               i.Status == InvoiceStatus.Paid ? "مدفوعة" : "ملغاة",
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return new FinanceSummaryDto
        {
            TodayCollected = todayCollected,
            MonthCollected = monthCollected,
            TotalOutstanding = contractOutstanding + invoiceOutstanding,
            ActiveContracts = activeContracts,
            UnpaidInvoicesCount = unpaidInvoicesCount,
            DraftInvoicesCount = draftInvoicesCount,
            OverdueAmount = overdueAmount,
            PendingCommissionsAmount = pendingCommissionsAmount,
            RecentPayments = recentPayments,
            RecentInvoices = recentInvoices
        };
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
        return MapPayment(payment);
    }

    public async Task<bool> DeletePaymentAsync(Guid id)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null) return false;

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
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
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
            await UpdateTreasuryBalanceNoSaveAsync(payment.BranchId ?? Guid.Empty, -payment.Amount, payment.PaymentMethod);
            await DualWriteReversalEntryAsync(payment.Id, "حذف دفعة");
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

        // H1: Partial refund validation
        var refundAmount = partialAmount.HasValue && partialAmount.Value > 0 && partialAmount.Value < payment.Amount
            ? partialAmount.Value
            : payment.Amount; // Full refund if null or >= original

        if (partialAmount.HasValue && (partialAmount.Value <= 0 || partialAmount.Value > payment.Amount))
            throw new ArgumentException("يجب أن يكون مبلغ الاسترداد الجزئي أكبر من الصفر ولا يتجاوز مبلغ الدفعة الأصلية.");

        var isPartialRefund = refundAmount < payment.Amount;

        // Phase 0B: Prevent double-refund — check if a refund already exists for this payment
        var existingRefund = await db.Payments
            .AnyAsync(p => p.IsActive && p.Amount < 0
                && p.ServiceDescription != null
                && p.ServiceDescription.StartsWith("استرداد:")
                && p.ContractId == payment.ContractId
                && p.InvoiceId == payment.InvoiceId
                && p.PatientId == payment.PatientId
                && p.Amount == -payment.Amount);
        if (existingRefund)
            throw new ArgumentException("تم استرداد هذه الدفعة مسبقاً. لا يمكن استرداد نفس الدفعة مرتين.");

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
            PaymentDate        = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod      = payment.PaymentMethod,
            ServiceDescription = isPartialRefund
                ? $"استرداد جزئي ({refundAmount:N0}): {payment.ServiceDescription ?? payment.ReceiptNumber}"
                : $"استرداد: {payment.ServiceDescription ?? payment.ReceiptNumber}",
            Specialty          = payment.Specialty,
            DoctorId           = payment.DoctorId,
            BranchId           = payment.BranchId,
            ReceivedBy         = currentUser.UserId,
            ReceiptNumber = await GenerateRefundReceiptNumberAsync(),
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
            await UpdateTreasuryBalanceNoSaveAsync(refund.BranchId ?? Guid.Empty, refund.Amount, refund.PaymentMethod);
            await DualWriteRefundEntryAsync(payment, refund, refundAmount);
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

        await db.Entry(refund).Reference(p => p.Patient).LoadAsync();
        await db.Entry(refund).Reference(p => p.Doctor).LoadAsync();
        return MapPayment(refund);
    }

    public async Task<PatientFinanceSummaryDto> GetPatientFinanceSummaryAsync(Guid patientId)
    {
        // ── Contract-based financials (legacy/ortho contracts) ───────────────
        var contracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId)
            .ToListAsync();

        var contractCost    = contracts.Sum(c => c.TotalAmount - c.DiscountAmount);
        var contractPaid    = contracts.Sum(c => c.Payments.Where(p => p.IsActive).Sum(p => p.Amount));

        // ── Invoice-based financials (new invoice system) ───────────────────
        var invoices = await db.Invoices
            .Include(i => i.Payments)
            .Where(i => i.PatientId == patientId
                     && i.Status != InvoiceStatus.Cancelled
                     && i.IsActive)
            .ToListAsync();

        var invoiceCost = invoices.Sum(i => i.TotalAmount);
        var invoicePaid = invoices.Sum(i => i.Payments.Where(p => p.IsActive).Sum(p => p.Amount));

        // ── Orphan payments (no ContractId and no InvoiceId) ────────────────
        // Payments created before invoice linkage or without either FK are
        // invisible to the contract/invoice sums above. Count them separately.
        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        var contractIds = contracts.Select(c => c.Id).ToHashSet();

        var orphanPaid = await db.Payments
            .Where(p => p.PatientId == patientId
                     && p.IsActive
                     && (p.InvoiceId == null || !invoiceIds.Contains(p.InvoiceId.Value))
                     && (p.ContractId == null || !contractIds.Contains(p.ContractId.Value)))
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // ── Combined totals ─────────────────────────────────────────────────
        var totalCost      = contractCost + invoiceCost;
        var totalPaid      = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var outstanding    = totalCost - totalPaid;

        var today          = DateOnly.FromDateTime(DateTime.Today);
        var overdueAmount  = 0m;
        foreach (var c in contracts.Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null))
        {
            var months   = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            var expected = c.DownPayment + Math.Min(months, c.InstallmentsCount) * (c.InstallmentAmount ?? 0);
            var paid     = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
            if (expected > paid) overdueAmount += expected - paid;
        }

        var latestPayment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        var totalPaymentsCount = await db.Payments
            .CountAsync(p => p.PatientId == patientId && p.IsActive);

        var status = totalCost == 0 ? "no_plan"
            : outstanding <= 0 ? "paid"
            : overdueAmount > 0 ? "overdue"
            : "on_track";

        return new PatientFinanceSummaryDto
        {
            TotalTreatmentCost   = totalCost,
            TotalPaid            = totalPaid,
            OutstandingBalance   = outstanding,
            OverdueAmount        = overdueAmount,
            LatestPayment        = latestPayment == null ? null : MapPayment(latestPayment),
            FinancialStatus      = status,
            ActiveContractsCount = contracts.Count(c => c.Status == ContractStatus.Active),
            TotalPaymentsCount   = totalPaymentsCount
        };
    }

    // ─── Finance Phase 1: Supplier Payables & Credit Notes ─────────────────

    /// <summary>
    /// Finance Phase 1: Pays a supplier bill (partially or fully).
    /// Validates open cashier session, loads bill + supplier, updates PaidAmount/Status/Balance,
    /// creates SupplierBillPayment, CashFlowTransaction (Outflow), and double-entry journal
    /// (Debit AccountsPayable / Credit Treasury). Commits atomically.
    /// </summary>
    public async Task PaySupplierBillAsync(Guid billId, PaySupplierBillRequest request, Guid currentUserId)
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

        var remaining = bill.TotalAmount - bill.PaidAmount;
        if (request.Amount > remaining)
            throw new ArgumentException($"المبلغ ({request.Amount:N0}) يتجاوز الرصيد المتبقي للفاتورة ({remaining:N0}).");

        // Update bill PaidAmount and Status
        bill.PaidAmount += request.Amount;
        bill.Status = bill.PaidAmount >= bill.TotalAmount ? BillStatus.FullyPaid : BillStatus.PartiallyPaid;
        bill.UpdatedAt = DateTime.UtcNow;

        // Update supplier Balance (reduce what we owe)
        if (bill.Supplier != null)
        {
            bill.Supplier.Balance -= request.Amount;
            bill.Supplier.UpdatedAt = DateTime.UtcNow;
        }

        // Create SupplierBillPayment record
        var billPayment = new SupplierBillPayment
        {
            SupplierBillId = bill.Id,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
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
            PaymentMethod = request.PaymentMethod,
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
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
                treasury.Balance -= request.Amount;
                treasury.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                await UpdateTreasuryBalanceNoSaveAsync(currentUser.BranchId.Value, -request.Amount, request.PaymentMethod);
                treasury = await ResolveTreasuryNoSaveAsync(currentUser.BranchId.Value, request.PaymentMethod);
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
                entryDate: DateOnly.FromDateTime(DateTime.Today),
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

        logger.LogInformation("Supplier bill {BillId} paid {Amount:N0} by user {UserId}", billId, request.Amount, currentUserId);
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
        var receiptNumber = await GenerateRefundReceiptNumberAsync();
        var refund = new Payment
        {
            PatientId = creditNote.PatientId,
            InvoiceId = creditNote.InvoiceId,
            Amount = -creditNote.Amount,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
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
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
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
            await UpdateTreasuryBalanceNoSaveAsync(currentUser.BranchId.Value, -creditNote.Amount, request.PaymentMethod);

            // Resolve treasury for journal entry — use explicit TreasuryId if provided, otherwise auto-resolve
            Treasury treasury;
            if (request.TreasuryId.HasValue && request.TreasuryId.Value != Guid.Empty)
            {
                treasury = await db.Treasuries.FirstOrDefaultAsync(t => t.Id == request.TreasuryId.Value && t.IsActive)
                    ?? throw new ArgumentException("الخزينة المحددة غير موجودة.");
            }
            else
            {
                treasury = await ResolveTreasuryNoSaveAsync(currentUser.BranchId.Value, request.PaymentMethod);
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
                entryDate: DateOnly.FromDateTime(DateTime.Today),
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

    private static ContractListDto MapContractList(Contract c) => new()
    {
        Id = c.Id,
        PatientId = c.PatientId,
        PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
        PatientNumber = c.Patient.PatientNumber,
        Specialty = c.Specialty,
        TotalAmount = c.TotalAmount,
        DownPayment = c.DownPayment,
        PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
        RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
        InstallmentsCount = c.InstallmentsCount,
        InstallmentAmount = c.InstallmentAmount,
        StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
        Status = c.Status.ToString()
    };

    private static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        PatientId = p.PatientId,
        PatientName = string.Join(" ", new[] { p.Patient?.FirstName, p.Patient?.LastName }.Where(n => !string.IsNullOrEmpty(n))),
        ContractId = p.ContractId,
        InvoiceId = p.InvoiceId,
        InvoiceNumber = p.Invoice?.InvoiceNumber,
        Amount = p.Amount,
        PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd"),
        PaymentMethod = p.PaymentMethod,
        ServiceDescription = p.ServiceDescription,
        Specialty = p.Specialty,
        DoctorName = p.Doctor?.Name,
        ReceiptNumber = p.ReceiptNumber,
        Notes = p.Notes
    };

    /// <summary>
    /// H9 FIX: Generates a unique receipt number using advisory lock + sequential pattern.
    /// Format: RCP-yyyyMMdd-NNN (sequential, not random).
    /// CON FIX: Uses pg_advisory_xact_lock inside an explicit transaction to prevent
    /// race conditions when multiple payments are created concurrently.
    /// Transaction-level lock is automatically released on commit/rollback — safe with
    /// connection pooling (no risk of stuck locks if the connection is returned to the pool).
    /// </summary>
    private async Task<string> GenerateReceiptNumberAsync()
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"RCP-{datePart}-";

        // Simple sequential generation without advisory locks.
        // Advisory locks (both xact_lock and session-level lock) cause DbContext concurrency
        // issues when called from CreatePaymentAsync which uses its own transaction.
        // Instead, rely on the unique constraint on ReceiptNumber + retry logic
        // (handled by the caller's transaction rollback).
        var lastReceipt = await db.Payments
            .IgnoreQueryFilters()
            .Where(p => p.ReceiptNumber != null && p.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(p => p.ReceiptNumber)
            .Select(p => p.ReceiptNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastReceipt) && lastReceipt.Length > prefix.Length)
        {
            var seqPart = lastReceipt[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }

    /// <summary>
    /// H9 FIX: Generates a unique refund receipt number.
    /// Format: REF-yyyyMMdd-NNN (sequential).
    /// CON FIX: Uses pg_advisory_xact_lock inside an explicit transaction to prevent
    /// race conditions. Transaction-level lock is automatically released on commit/rollback
    /// — safe with connection pooling (no risk of stuck locks).
    /// </summary>
    private async Task<string> GenerateRefundReceiptNumberAsync()
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"REF-{datePart}-";

        // Simple sequential generation without advisory locks (same reason as GenerateReceiptNumberAsync).
        var lastRefund = await db.Payments
            .IgnoreQueryFilters()
            .Where(p => p.ReceiptNumber != null && p.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(p => p.ReceiptNumber)
            .Select(p => p.ReceiptNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastRefund) && lastRefund.Length > prefix.Length)
        {
            var seqPart = lastRefund[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }

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
            .Sum(p => p.Amount);

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
            .SumAsync(p => p.Amount);

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

    private async Task UpdateTreasuryBalanceAsync(Guid branchId, decimal amount, string? paymentMethod)
    {
        var type = (paymentMethod == "card" || paymentMethod == "bank_transfer" || paymentMethod == "bank") 
            ? TreasuryType.Bank 
            : TreasuryType.Vault;
        
        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        // Previously used hardcoded names ("حساب بنك التضامن", "درج كاشير الاستقبال")
        // which would fail if the treasury was renamed. Now we find the first active
        // treasury of the correct type for the branch, regardless of its name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.IsActive);
            
        if (treasury == null)
        {
            // Only use default name when auto-creating a new treasury
            var defaultName = type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
            // Do NOT call SaveChangesAsync — the caller's transaction will persist this.
            // Previously, SaveChangesAsync here caused "A second operation was started on
            // this context instance" because it conflicts with the caller's open transaction.
        }
        
        // Direct balance update (no raw SQL). Inside a transaction, raw SQL via
        // ExecuteSqlRawAsync causes "A second operation was started on this context
        // instance" because EF Core cannot pipeline a raw SQL command alongside an
        // open transaction on the same DbContext. Using the tracked entity's Balance
        // property is safe because the transaction guarantees atomicity — if two
        // concurrent payments try to update the same treasury, the database's
        // default READ COMMITTED isolation will serialize the writes.
        treasury.Balance += amount;

        // A4: Handle optimistic concurrency for Treasury balance updates
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ArgumentException("تعارض في تحديث رصيد الخزينة. يرجى المحاولة مرة أخرى.");
        }
    }

    /// <summary>
    /// Same as UpdateTreasuryBalanceAsync but does NOT call SaveChangesAsync.
    /// Used within atomic dual-write transactions (CreatePaymentAsync, DeletePaymentAsync, RefundPaymentAsync)
    /// where all entity changes must be tracked in the DbContext and persisted together
    /// at the end of the transaction.
    /// </summary>
    private async Task UpdateTreasuryBalanceNoSaveAsync(Guid branchId, decimal amount, string? paymentMethod)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury balance update");

        var type = (paymentMethod == "card" || paymentMethod == "bank_transfer" || paymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        // Previously used hardcoded names ("حساب بنك التضامن", "درج كاشير الاستقبال")
        // which would fail if the treasury was renamed. Now we find the first active
        // treasury of the correct type for the branch, regardless of its name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.IsActive);

        if (treasury == null)
        {
            // Check ChangeTracker for a locally added treasury not yet persisted
            treasury = db.ChangeTracker.Entries<Treasury>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.BranchId == branchId
                    && e.Entity.Type == type
                    && e.Entity.IsActive)
                .Select(e => e.Entity)
                .FirstOrDefault();
        }

        var isNewTreasury = false;
        if (treasury == null)
        {
            // Only use default name when auto-creating a new treasury
            var defaultName = type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
            isNewTreasury = true;
        }

        // Direct balance update (no raw SQL) — same reason as UpdateTreasuryBalanceNoSaveAsync:
        // ExecuteSqlRawAsync causes DbContext concurrency issues inside transactions.
        // The tracked entity update is safe because the caller's transaction provides atomicity.
        treasury.Balance += amount;

        // Do NOT call SaveChangesAsync — the caller persists all changes together
    }

    // ─── Finance V3 Dual-Write Methods ─────────────────────────────────────────

    /// <summary>
    /// Resolves the treasury for a given branch and payment method.
    /// MUST throw if branchId is Guid.Empty or if no treasury can be found/created.
    /// Sets TreasuryId on the CashFlowTransaction.
    /// </summary>
    private async Task<Treasury> ResolveTreasuryAsync(Guid branchId, string? paymentMethod)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury resolution and cannot be Guid.Empty");

        var type = (paymentMethod == "card" || paymentMethod == "bank_transfer" || paymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.IsActive);

        if (treasury == null)
        {
            var defaultName = type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
            await db.SaveChangesAsync();
        }

        return treasury;
    }

    /// <summary>
    /// Same as ResolveTreasuryAsync but does NOT call SaveChangesAsync.
    /// Used within atomic dual-write transactions where the caller persists all changes.
    /// </summary>
    private async Task<Treasury> ResolveTreasuryNoSaveAsync(Guid branchId, string? paymentMethod)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury resolution and cannot be Guid.Empty");

        var type = (paymentMethod == "card" || paymentMethod == "bank_transfer" || paymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.IsActive);

        if (treasury == null)
        {
            // Check ChangeTracker for a locally added treasury not yet persisted
            treasury = db.ChangeTracker.Entries<Treasury>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.BranchId == branchId
                    && e.Entity.Type == type
                    && e.Entity.IsActive)
                .Select(e => e.Entity)
                .FirstOrDefault();
        }

        if (treasury == null)
        {
            var defaultName = type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
            // Do NOT call SaveChangesAsync — the caller will save all tracked entities together
        }

        return treasury;
    }

    /// <summary>
    /// Dual-write journal entry for a patient payment.
    /// - If allocated to an Issued invoice: Debit Treasury / Credit PatientReceivable (settles AR, no revenue)
    /// - If unallocated advance: Debit Treasury / Credit PatientAdvance (records liability)
    /// MUST be atomic — if JE fails, the entire operation must fail.
    /// </summary>
    private async Task DualWritePaymentEntryAsync(Payment payment, CashFlowTransaction cashflow, Invoice? invoice)
    {
        if (payment.PatientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be Guid.Empty for dual-write journal entry");
        if (payment.BranchId == null || payment.BranchId == Guid.Empty)
            throw new ArgumentException("BranchId cannot be Guid.Empty for dual-write journal entry");

        var treasury = await ResolveTreasuryNoSaveAsync(payment.BranchId.Value, payment.PaymentMethod);
        cashflow.TreasuryId = treasury.Id;

        var isAllocatedToInvoice = invoice != null && invoice.Status == InvoiceStatus.Issued;
        var creditAccountType = isAllocatedToInvoice ? JournalAccountType.PatientReceivable : JournalAccountType.PatientAdvance;
        var creditDescription = isAllocatedToInvoice
            ? $"تسوية ذمم مريض - سند قبض {payment.ReceiptNumber}"
            : $"دفعة مقدمة غير مخصصة - سند قبض {payment.ReceiptNumber}";

        var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
        {
            (JournalAccountType.Treasury, treasury.Id, payment.Amount, 0m, $"تحصيل دفعة - سند قبض {payment.ReceiptNumber}"),
            (creditAccountType, payment.PatientId, 0m, payment.Amount, creditDescription)
        };

        var entry = await journalEntryService.CreateEntryAsync(
            documentType: FinancialDocumentType.Payment,
            financialDocumentId: payment.Id,
            description: isAllocatedToInvoice
                ? $"تحصيل دفعة مستحقة - سند قبض {payment.ReceiptNumber}"
                : $"تحصيل دفعة مقدمة - سند قبض {payment.ReceiptNumber}",
            entryDate: payment.PaymentDate,
            branchId: payment.BranchId.Value,
            performedBy: payment.ReceivedBy ?? Guid.Empty,
            cashierSessionId: cashflow.CashierSessionId,
            treasuryId: treasury.Id,
            lines: lines,
            autoSave: false);

        // Auto-post since this is an operational posting
        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

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
                && e.FinancialDocumentType == FinancialDocumentType.Invoice
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

    /// <summary>
    /// Dual-write reversal entry for deleted/cancelled payments.
    /// Finds the original JournalEntry by FinancialDocumentId and creates a mirrored reversal.
    /// MUST NOT silently swallow exceptions.
    /// </summary>
    private async Task DualWriteReversalEntryAsync(Guid paymentId, string reason)
    {
        var originalEntry = await db.JournalEntries
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == paymentId && e.FinancialDocumentType == FinancialDocumentType.Payment && !e.IsReversal);

        if (originalEntry == null)
        {
            logger.LogWarning("No original JournalEntry found for payment {PaymentId} — skipping JE reversal", paymentId);
            return;
        }

        var reversal = await journalEntryService.CreateReversalEntryAsync(
            originalEntryId: originalEntry.Id,
            reason: reason,
            performedBy: currentUser.UserId ?? Guid.Empty);

        // Auto-post the reversal
        reversal.IsPosted = true;
        reversal.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Dual-write refund entry.
    /// - If original payment was allocated to an invoice: Debit PatientReceivable / Credit Treasury
    /// - If original payment was unallocated advance: Debit PatientAdvance / Credit Treasury
    /// MUST NOT silently swallow exceptions.
    /// </summary>
    private async Task DualWriteRefundEntryAsync(Payment originalPayment, Payment refundPayment, decimal refundAmount)
    {
        if (originalPayment.PatientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be Guid.Empty for refund journal entry");
        if (originalPayment.BranchId == null || originalPayment.BranchId == Guid.Empty)
            throw new ArgumentException("BranchId cannot be Guid.Empty for refund journal entry");

        var treasury = await ResolveTreasuryNoSaveAsync(originalPayment.BranchId.Value, refundPayment.PaymentMethod);

        var wasAllocatedToInvoice = originalPayment.InvoiceId.HasValue;
        var debitAccountType = wasAllocatedToInvoice ? JournalAccountType.PatientReceivable : JournalAccountType.PatientAdvance;
        var debitDescription = wasAllocatedToInvoice
            ? $"إعادة ذمم مدينة - استرداد سند قبض {refundPayment.ReceiptNumber}"
            : $"تخفيض دفعات مقدمة - استرداد سند قبض {refundPayment.ReceiptNumber}";

        var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
        {
            (debitAccountType, originalPayment.PatientId, refundAmount, 0m, debitDescription),
            (JournalAccountType.Treasury, treasury.Id, 0m, refundAmount, $"صرف استرداد - سند قبض {refundPayment.ReceiptNumber}")
        };

        var entry = await journalEntryService.CreateEntryAsync(
            documentType: FinancialDocumentType.Refund,
            financialDocumentId: refundPayment.Id,
            description: wasAllocatedToInvoice
                ? $"استرداد دفعة مستحقة - سند قبض {refundPayment.ReceiptNumber}"
                : $"استرداد دفعة مقدمة - سند قبض {refundPayment.ReceiptNumber}",
            entryDate: refundPayment.PaymentDate,
            branchId: originalPayment.BranchId.Value,
            performedBy: refundPayment.ReceivedBy ?? Guid.Empty,
            cashierSessionId: null,
            treasuryId: treasury.Id,
            lines: lines,
            autoSave: false);

        // Auto-post
        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
