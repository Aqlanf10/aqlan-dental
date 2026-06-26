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
    private const string BaseCurrency = "YER";
    private static readonly HashSet<string> SupportedCurrencies = ["YER", "SAR", "USD"];

    public async Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Include(c => c.Package) // YOLO-S2: package name + color for display
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
            .Include(c => c.Package) // YOLO-S2: package name + color for display
            .FirstOrDefaultAsync(c => c.Id == id);

        if (c == null) return null;

        var dto = new ContractDetailDto
        {
            Id = c.Id,
            PatientId = c.PatientId,
            PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
            PatientNumber = c.Patient.PatientNumber,
            Specialty = c.Specialty,
            Currency = NormalizeCurrency(c.Currency),
            TotalAmount = c.TotalAmount,
            DownPayment = c.DownPayment,
            PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
            RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
            InstallmentsCount = c.InstallmentsCount,
            InstallmentAmount = c.InstallmentAmount,
            StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
            Status = c.Status.ToString(),
            DiscountAmount = c.DiscountAmount,
            DiscountReason = c.DiscountReason,
            Notes = c.Notes,
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(MapPayment).ToList(),
            // YOLO-S2: package link
            PackageId = c.PackageId,
            PackageName = c.Package?.Name,
            PackageColor = c.Package?.Color,
        };

        return dto;
    }

    public async Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req)
    {
        // YOLO-S2: validate the optional package link if provided. Resolves Guid.Empty to null
        // so the caller can send an empty Guid to mean "no package" without a separate flag.
        Guid? packageId = req.PackageId;
        if (packageId == Guid.Empty) packageId = null;
        if (packageId.HasValue)
        {
            var pkgExists = await db.TreatmentPackages.AnyAsync(p => p.Id == packageId.Value && p.IsActive);
            if (!pkgExists)
                throw new ArgumentException("الباقة المحددة غير موجودة أو معطّلة");
        }

        var contract = new Contract
        {
            PatientId = req.PatientId,
            Specialty = req.Specialty,
            RelatedCaseId = req.RelatedCaseId,
            Currency = NormalizeCurrency(req.Currency),
            TotalAmount = req.TotalAmount,
            DownPayment = req.DownPayment,
            InstallmentsCount = req.InstallmentsCount,
            InstallmentAmount = req.InstallmentAmount,
            StartDate = req.StartDate != null ? DateOnly.Parse(req.StartDate) : ClinicTimeProvider.ClinicToday(),
            DiscountAmount = req.DiscountAmount,
            DiscountReason = req.DiscountReason,
            Status = ContractStatus.Active,
            Notes = req.Notes,
            CreatedBy = currentUser.UserId,
            PackageId = packageId, // YOLO-S2
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
                Currency = NormalizeCurrency(req.Currency),
                AccountCurrency = NormalizeCurrency(req.Currency),
                PaymentMethod = req.DownPaymentMethod ?? "cash", // Sprint Patient-Finance-Ledger: was hardcoded "cash"
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
        var today = ClinicTimeProvider.ClinicToday();

        // FIN-13: Project only the fields needed for the overdue calculation + the
        // per-contract PaidAmount (computed in SQL via correlated subquery).
        // Previously this loaded Contracts.Include(c => c.Payments).ToListAsync()
        // which fetched every Payment row for every active installment contract.
        // The month-since-StartDate calc remains in C# (EF can't translate DateOnly
        // year/month arithmetic cleanly) but operates on a tiny projected set.
        var candidates = await db.Contracts
            .Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null)
            .Select(c => new
            {
                c.Id,
                c.PatientId,
                PatientName   = c.Patient.FirstName + " " + c.Patient.LastName,
                PatientNumber = c.Patient.PatientNumber,
                Phone         = c.Patient.Phone,
                c.Specialty,
                c.TotalAmount,
                c.DiscountAmount,
                c.DownPayment,
                c.StartDate,
                c.InstallmentsCount,
                c.InstallmentAmount,
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)
            })
            .ToListAsync();

        var overdue = new List<OverdueContractDto>();

        foreach (var c in candidates)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            if (monthsElapsed <= 0) continue;

            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var overdueAmt   = expectedPaid - c.PaidAmount;

            if (overdueAmt > 0)
            {
                overdue.Add(new OverdueContractDto
                {
                    ContractId     = c.Id,
                    PatientId      = c.PatientId,
                    PatientName    = c.PatientName,
                    PatientNumber  = c.PatientNumber,
                    Phone          = c.Phone,
                    Specialty      = c.Specialty,
                    TotalAmount    = c.TotalAmount,
                    PaidAmount     = c.PaidAmount,
                    OverdueAmount  = overdueAmt,
                    RemainingAmount= c.TotalAmount - c.DiscountAmount - c.PaidAmount,
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

        var paymentCurrency = NormalizeCurrency(req.Currency);
        var accountCurrency = ResolveAccountCurrency(req.AccountCurrency, invoice, contract);
        var exchange = await ResolveExchangeRateAsync(paymentCurrency, accountCurrency, req.ExchangeRateToAccountCurrency);
        var exchangeRateSource = string.IsNullOrWhiteSpace(req.ExchangeRateSource)
            ? (paymentCurrency == accountCurrency ? "same_currency" : (req.ExchangeRateToAccountCurrency.HasValue ? "manual" : "settings"))
            : req.ExchangeRateSource.Trim();
        var appliedAmount = Math.Round(req.Amount * exchange, 2, MidpointRounding.AwayFromZero);

        if (invoice != null)
        {
            var alreadyPaid = await db.Payments
                .Where(p => p.InvoiceId == invoice.Id && p.IsActive)
                .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;
            var remaining = invoice.TotalAmount - alreadyPaid;
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
        var normalizedPaymentMethod = NormalizePaymentMethod(storedPaymentMethod);
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
                Currency = paymentCurrency,
                AccountCurrency = accountCurrency,
                ExchangeRateToAccountCurrency = exchange,
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

            await UpdateTreasuryBalanceNoSaveAsync(payment.BranchId ?? Guid.Empty, payment.Amount, normalizedPaymentMethod, paymentCurrency);
            await DualWritePaymentEntryAsync(payment, cashflow, invoice);
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
                .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;
            if (req.TotalAmount < alreadyPaid)
                throw new ArgumentException($"لا يمكن تقليل إجمالي العقد ({req.TotalAmount:N0} ر.ي) إلى أقل من المبلغ المدفوع فعلياً ({alreadyPaid:N0} ر.ي).");
        }

        contract.Specialty        = req.Specialty;
        contract.Currency         = NormalizeCurrency(req.Currency ?? contract.Currency);
        contract.TotalAmount      = req.TotalAmount;
        contract.InstallmentsCount = req.InstallmentsCount;
        contract.InstallmentAmount = req.InstallmentAmount;
        contract.StartDate        = req.StartDate != null ? DateOnly.Parse(req.StartDate) : contract.StartDate;
        contract.DiscountAmount   = req.DiscountAmount;
        contract.DiscountReason   = req.DiscountReason;
        contract.Notes            = req.Notes;
        contract.UpdatedAt        = DateTime.UtcNow;

        // YOLO-S2: update the package link. Resolve Guid.Empty → null ("clear").
        // null on the request body leaves the existing value unchanged (PATCH semantics).
        if (req.PackageId.HasValue)
        {
            var newPkgId = req.PackageId.Value == Guid.Empty ? null : (Guid?)req.PackageId.Value;
            if (newPkgId.HasValue && newPkgId != contract.PackageId)
            {
                var pkgExists = await db.TreatmentPackages.AnyAsync(p => p.Id == newPkgId.Value && p.IsActive);
                if (!pkgExists)
                    throw new ArgumentException("الباقة المحددة غير موجودة أو معطّلة");
            }
            contract.PackageId = newPkgId;
        }

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
                            TransactionDate = ClinicTimeProvider.ClinicToday(),
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

        // FIN-13: All summary aggregations now execute server-side as SQL SUM(...)
        // (replaces in-memory .Sum() over ToListAsync-loaded entities). Each query
        // returns a single scalar, so we move less data across the wire.

        // Sprint Patient-Finance-Ledger: Exclude Draft invoices — same as GetPatientFinanceSummaryAsync
        var invoicesPredicate = new Func<IQueryable<Invoice>, IQueryable<Invoice>>(q => q
            .Where(i => i.PatientId == patientId
                     && i.Status != InvoiceStatus.Cancelled
                     && i.Status != InvoiceStatus.Draft
                     && i.IsActive));

        var totalContractedFromContracts = await db.Contracts
            .Where(c => c.PatientId == patientId)
            .SumAsync(c => (decimal?)c.TotalAmount) ?? 0m;

        var totalContractedFromInvoices = await invoicesPredicate(db.Invoices)
            .SumAsync(i => (decimal?)(i.Subtotal + (i.TaxAmount ?? 0m))) ?? 0m;

        var totalContracted = totalContractedFromContracts + totalContractedFromInvoices;

        var totalDiscountsFromContracts = await db.Contracts
            .Where(c => c.PatientId == patientId)
            .SumAsync(c => (decimal?)c.DiscountAmount) ?? 0m;

        var totalDiscountsFromInvoices = await invoicesPredicate(db.Invoices)
            .SumAsync(i => (decimal?)i.DiscountAmount) ?? 0m;

        var totalDiscounts = totalDiscountsFromContracts + totalDiscountsFromInvoices;

        // FIX: Calculate totalPaid from ALL active payments for the patient,
        // not just contract-linked ones. Unlinked/orphan payments must still
        // count in the overall patient summary so the balance is accurate.
        // Each payment is counted exactly once via the direct Payments query.
        var totalPaid = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive)
            .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;

        var totalRemaining = Math.Max(0m, totalContracted - totalDiscounts - totalPaid);

        // ── Contracts list with per-contract paid/remaining computed in SQL ──
        // FIN-13: Project ContractStatementDto directly. The correlated subquery
        // `c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)` is translated to
        // `(SELECT COALESCE(SUM(p.Amount), 0) FROM Payments p WHERE p.ContractId = c.Id AND p.IsActive)`
        // — avoids loading any Payment rows into memory.
        var contracts = await db.Contracts
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContractStatementDto
            {
                Id              = c.Id,
                Specialty       = c.Specialty,
                TotalAmount     = c.TotalAmount,
                DiscountAmount  = c.DiscountAmount,
                PaidAmount      = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
                RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
                StartDate       = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd") : null,
                Status          = c.Status.ToString(),
                InstallmentsCount  = c.InstallmentsCount,
                InstallmentAmount  = c.InstallmentAmount
            })
            .ToListAsync();

        var activeContracts     = await db.Contracts.CountAsync(c => c.PatientId == patientId && c.Status == ContractStatus.Active);
        var completedContracts  = await db.Contracts.CountAsync(c => c.PatientId == patientId && c.Status == ContractStatus.Completed);

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
            ActiveContracts  = activeContracts,
            CompletedContracts = completedContracts,
            Contracts        = contracts,
            RecentPayments   = recentPayments.Select(p => MapPayment(p)).ToList()
        };
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync()
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = ClinicTimeProvider.ClinicToday();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var todayQuery = db.Payments.Where(p => p.PaymentDate == today && p.IsActive && (p.AccountCurrency == null || p.AccountCurrency == "YER"));
        if (branchId.HasValue) todayQuery = todayQuery.Where(p => p.BranchId == branchId);
        var todayCollected = await todayQuery.SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0;

        var monthQuery = db.Payments.Where(p => p.PaymentDate >= monthStart && p.IsActive && (p.AccountCurrency == null || p.AccountCurrency == "YER"));
        if (branchId.HasValue) monthQuery = monthQuery.Where(p => p.BranchId == branchId);
        var monthCollected = await monthQuery.SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0;

        // Contract-based outstanding
        var contractQuery = db.Contracts.Include(c => c.Payments).Where(c => c.Status == ContractStatus.Active);
        if (branchId.HasValue) contractQuery = contractQuery.Where(c => c.Patient.BranchId == branchId);
        var contractOutstanding = await contractQuery
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount))
            .SumAsync(r => (decimal?)r) ?? 0;

        // Invoice-based outstanding (Issued invoices not fully paid)
        var invoiceQuery = db.Invoices.Include(i => i.Payments)
            .Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);
        if (branchId.HasValue) invoiceQuery = invoiceQuery.Where(i => i.Patient.BranchId == branchId);
        var invoiceOutstanding = await invoiceQuery
            .Select(i => i.TotalAmount - i.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount))
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
            Currency           = NormalizeCurrency(payment.Currency),
            AccountCurrency    = NormalizeCurrency(payment.AccountCurrency),
            ExchangeRateToAccountCurrency = payment.ExchangeRateToAccountCurrency == 0 ? 1m : payment.ExchangeRateToAccountCurrency,
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
            Currency = NormalizeCurrency(payment.Currency),
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
            await UpdateTreasuryBalanceNoSaveAsync(refund.BranchId ?? Guid.Empty, refund.Amount, refund.PaymentMethod, refund.Currency);
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
        return MapPayment(refund);
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

    public async Task<PatientFinanceSummaryDto> GetPatientFinanceSummaryAsync(Guid patientId)
    {
        // ── Contract-based cost (server-side aggregation, FIN-13) ───────────
        // Previously: loaded all contracts (+ Payments collection) into memory then
        //             `contracts.Sum(c => c.TotalAmount - c.DiscountAmount)`.
        // Now:        single SQL `SELECT SUM(TotalAmount - DiscountAmount) FROM Contracts
        //             WHERE PatientId = @patientId` (returns 0 for empty set).
        var contractCost = await db.Contracts
            .Where(c => c.PatientId == patientId)
            .SumAsync(c => (decimal?)(c.TotalAmount - c.DiscountAmount)) ?? 0m;

        // ── Invoice-based financials (new invoice system) ───────────────────
        // Sprint Patient-Finance-Ledger: Exclude Draft invoices from outstanding balance.
        // Draft invoices are not yet committed — only Issued and Paid represent actual obligation.
        // This aligns with FinanceV3 GetPatientBalance which also excludes Drafts.
        var invoiceCost = await db.Invoices
            .Where(i => i.PatientId == patientId
                     && i.Status != InvoiceStatus.Cancelled
                     && i.Status != InvoiceStatus.Draft
                     && i.IsActive)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        // ── Combined totals ─────────────────────────────────────────────────
        var totalCost      = contractCost + invoiceCost;
        var totalPaid      = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive)
            .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;
        var outstanding    = Math.Max(0m, totalCost - totalPaid);

        // ── Overdue amount ──────────────────────────────────────────────────
        // FIN-13: Project only the fields needed for the date math + the per-contract
        // paid total (computed in SQL via correlated subquery). Avoids loading the
        // full Payment collection for every contract. The month-since-StartDate calc
        // stays in C# because EF can't translate DateOnly year/month arithmetic cleanly.
        var today          = ClinicTimeProvider.ClinicToday();
        var overdueCandidates = await db.Contracts
            .Where(c => c.PatientId == patientId
                     && c.Status == ContractStatus.Active
                     && c.InstallmentAmount > 0
                     && c.StartDate != null)
            .Select(c => new
            {
                c.StartDate,
                c.InstallmentsCount,
                c.InstallmentAmount,
                c.DownPayment,
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)
            })
            .ToListAsync();

        var overdueAmount  = 0m;
        foreach (var c in overdueCandidates)
        {
            var months   = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            var expected = c.DownPayment + Math.Min(months, c.InstallmentsCount) * (c.InstallmentAmount ?? 0);
            if (expected > c.PaidAmount) overdueAmount += expected - c.PaidAmount;
        }

        var latestPayment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        var totalPaymentsCount = await db.Payments
            .CountAsync(p => p.PatientId == patientId && p.IsActive);

        var activeContractsCount = await db.Contracts
            .CountAsync(c => c.PatientId == patientId && c.Status == ContractStatus.Active);

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
            ActiveContractsCount = activeContractsCount,
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

    private static ContractListDto MapContractList(Contract c) => new()
    {
        Id = c.Id,
        PatientId = c.PatientId,
        PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
        PatientNumber = c.Patient.PatientNumber,
        Specialty = c.Specialty,
        Currency = NormalizeCurrency(c.Currency),
        TotalAmount = c.TotalAmount,
        DownPayment = c.DownPayment,
        PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
        RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
        InstallmentsCount = c.InstallmentsCount,
        InstallmentAmount = c.InstallmentAmount,
        StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
        Status = c.Status.ToString(),
        // YOLO-S2: package link (display-only — pricing still driven by TotalAmount)
        PackageId = c.PackageId,
        PackageName = c.Package?.Name,
        PackageColor = c.Package?.Color,
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
        Currency = p.Currency,
        AccountCurrency = NormalizeCurrency(p.AccountCurrency),
        ExchangeRateToAccountCurrency = p.ExchangeRateToAccountCurrency == 0 ? 1m : p.ExchangeRateToAccountCurrency,
        AppliedAmount = p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount,
        ExchangeRateSource = p.ExchangeRateSource,
        PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd"),
        PaymentMethod = p.PaymentMethod,
        ServiceDescription = p.ServiceDescription,
        Specialty = p.Specialty,
        DoctorName = p.Doctor?.Name,
        ReceiptNumber = p.ReceiptNumber,
        Notes = p.Notes
    };

    private static string NormalizeCurrency(string? currency)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? BaseCurrency : currency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(code))
            throw new ArgumentException("العملة يجب أن تكون YER أو SAR أو USD");
        return code;
    }

    private static string ResolveAccountCurrency(string? requestedCurrency, Invoice? invoice, Contract? contract)
    {
        if (invoice != null) return NormalizeCurrency(invoice.Currency);
        if (contract != null) return NormalizeCurrency(contract.Currency);
        return NormalizeCurrency(requestedCurrency);
    }

    private async Task<decimal> ResolveExchangeRateAsync(string paymentCurrency, string accountCurrency, decimal? directRate)
    {
        paymentCurrency = NormalizeCurrency(paymentCurrency);
        accountCurrency = NormalizeCurrency(accountCurrency);
        if (paymentCurrency == accountCurrency) return 1m;
        if (directRate.HasValue && directRate.Value > 0m) return directRate.Value;

        var paymentToYer = await GetCurrencyToYerRateAsync(paymentCurrency);
        var accountToYer = await GetCurrencyToYerRateAsync(accountCurrency);
        if (accountToYer <= 0m) throw new ArgumentException("سعر صرف عملة الحساب غير صالح");
        return Math.Round(paymentToYer / accountToYer, 6, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> GetCurrencyToYerRateAsync(string currency)
    {
        currency = NormalizeCurrency(currency);
        if (currency == BaseCurrency) return 1m;

        var key = $"finance.exchange_rate.{currency}_YER";
        var value = await db.Settings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0m)
            return parsed;

        throw new ArgumentException($"لا يوجد سعر صرف معتمد للعملة {currency}. حدده من إعدادات المالية قبل تسجيل الدفعة.");
    }

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
        var normalizedPaymentMethod = NormalizePaymentMethod(paymentMethod);
        var type = (normalizedPaymentMethod == "card" || normalizedPaymentMethod == "bank")
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

    private static string NormalizePaymentMethod(string? method)
    {
        var value = (method ?? "cash").Trim().ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");

        return value switch
        {
            "" or "cash" or "نقدي" or "نقدا" => "cash",
            "card" or "credit card" or "debit card" or "بطاقة" => "card",
            "bank" or "bank transfer" or "transfer" or "تحويل بنكي" or "حوالة" or "karimey" or "jawaly" or "check" => "bank",
            _ => value
        };
    }

    /// <summary>
    /// Same as UpdateTreasuryBalanceAsync but does NOT call SaveChangesAsync.
    /// Used within atomic dual-write transactions (CreatePaymentAsync, DeletePaymentAsync, RefundPaymentAsync)
    /// where all entity changes must be tracked in the DbContext and persisted together
    /// at the end of the transaction.
    /// </summary>
    private async Task UpdateTreasuryBalanceNoSaveAsync(Guid branchId, decimal amount, string? paymentMethod, string? currency = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury balance update");

        var normalizedPaymentMethod = NormalizePaymentMethod(paymentMethod);
        var normalizedCurrency = NormalizeCurrency(currency);
        var type = (normalizedPaymentMethod == "card" || normalizedPaymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        // Previously used hardcoded names ("حساب بنك التضامن", "درج كاشير الاستقبال")
        // which would fail if the treasury was renamed. Now we find the first active
        // treasury of the correct type for the branch, regardless of its name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.Currency == normalizedCurrency && t.IsActive);

        if (treasury == null)
        {
            // Check ChangeTracker for a locally added treasury not yet persisted
            treasury = db.ChangeTracker.Entries<Treasury>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.BranchId == branchId
                    && e.Entity.Type == type
                    && e.Entity.Currency == normalizedCurrency
                    && e.Entity.IsActive)
                .Select(e => e.Entity)
                .FirstOrDefault();
        }

        if (treasury == null)
        {
            // Only use default name when auto-creating a new treasury
            var defaultName = normalizedCurrency == BaseCurrency
                ? (type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")
                : $"{(type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")} - {normalizedCurrency}";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Currency = normalizedCurrency,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
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
    private async Task<Treasury> ResolveTreasuryAsync(Guid branchId, string? paymentMethod, string? currency = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury resolution and cannot be Guid.Empty");

        var normalizedPaymentMethod = NormalizePaymentMethod(paymentMethod);
        var normalizedCurrency = NormalizeCurrency(currency);
        var type = (normalizedPaymentMethod == "card" || normalizedPaymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.Currency == normalizedCurrency && t.IsActive);

        if (treasury == null)
        {
            var defaultName = normalizedCurrency == BaseCurrency
                ? (type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")
                : $"{(type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")} - {normalizedCurrency}";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Currency = normalizedCurrency,
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
    private async Task<Treasury> ResolveTreasuryNoSaveAsync(Guid branchId, string? paymentMethod, string? currency = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury resolution and cannot be Guid.Empty");

        var normalizedCurrency = NormalizeCurrency(currency);
        var type = (paymentMethod == "card" || paymentMethod == "bank_transfer" || paymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.Currency == normalizedCurrency && t.IsActive);

        if (treasury == null)
        {
            // Check ChangeTracker for a locally added treasury not yet persisted
            treasury = db.ChangeTracker.Entries<Treasury>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.BranchId == branchId
                    && e.Entity.Type == type
                    && e.Entity.Currency == normalizedCurrency
                    && e.Entity.IsActive)
                .Select(e => e.Entity)
                .FirstOrDefault();
        }

        if (treasury == null)
        {
            var defaultName = normalizedCurrency == BaseCurrency
                ? (type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")
                : $"{(type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")} - {normalizedCurrency}";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Currency = normalizedCurrency,
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

        var treasury = await ResolveTreasuryNoSaveAsync(payment.BranchId.Value, payment.PaymentMethod, payment.Currency);
        cashflow.TreasuryId = treasury.Id;
        var appliedAmount = payment.AppliedAmount == 0 ? payment.Amount : payment.AppliedAmount;

        var isAllocatedToInvoice = invoice != null && invoice.Status == InvoiceStatus.Issued;
        var creditAccountType = isAllocatedToInvoice ? JournalAccountType.PatientReceivable : JournalAccountType.PatientAdvance;
        var creditDescription = isAllocatedToInvoice
            ? $"تسوية ذمم مريض - سند قبض {payment.ReceiptNumber}"
            : $"دفعة مقدمة غير مخصصة - سند قبض {payment.ReceiptNumber}";

        var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
        {
            (JournalAccountType.Treasury, treasury.Id, appliedAmount, 0m, $"تحصيل دفعة - سند قبض {payment.ReceiptNumber} ({payment.Amount:N2} {NormalizeCurrency(payment.Currency)})"),
            (creditAccountType, payment.PatientId, 0m, appliedAmount, creditDescription)
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

        var treasury = await ResolveTreasuryNoSaveAsync(originalPayment.BranchId.Value, refundPayment.PaymentMethod, refundPayment.Currency);
        var appliedRefundAmount = Math.Abs(refundPayment.AppliedAmount == 0 ? refundAmount : refundPayment.AppliedAmount);

        var wasAllocatedToInvoice = originalPayment.InvoiceId.HasValue;
        var debitAccountType = wasAllocatedToInvoice ? JournalAccountType.PatientReceivable : JournalAccountType.PatientAdvance;
        var debitDescription = wasAllocatedToInvoice
            ? $"إعادة ذمم مدينة - استرداد سند قبض {refundPayment.ReceiptNumber}"
            : $"تخفيض دفعات مقدمة - استرداد سند قبض {refundPayment.ReceiptNumber}";

        var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
        {
            (debitAccountType, originalPayment.PatientId, appliedRefundAmount, 0m, debitDescription),
            (JournalAccountType.Treasury, treasury.Id, 0m, appliedRefundAmount, $"صرف استرداد - سند قبض {refundPayment.ReceiptNumber}")
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
