using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Application.Services;

public class FinanceService(AppDbContext db, ICurrentUserService currentUser, INotificationService notifications, ILogger<FinanceService> logger)
{
    public async Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status)
    {
        var branchId = currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .AsQueryable();

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
            PaidAmount = c.Payments.Sum(p => p.Amount),
            RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount),
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
            var actualPaid   = c.Payments.Sum(p => p.Amount);
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
        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .AsQueryable();

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
        // H9 FIX: Generate receipt number using advisory lock + sequential pattern
        // instead of random 4-digit (which had ~50% collision probability at 95 payments/day).
        var receiptNumber = await GenerateReceiptNumberAsync();

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

        var payment = new Payment
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
            BranchId = currentUser.BranchId,
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

        await db.SaveChangesAsync();

        // Auto-transition invoice to Paid if payments cover the total
        if (invoice != null)
        {
            await TryMarkInvoicePaidAsync(invoice.Id);
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

        // Notify accountants and admins
        _ = Task.Run(async () =>
        {
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
                logger.LogWarning(ex, "[FinanceService] Non-blocking notification failed after payment {PaymentId}", payment.Id);
            }
        });

        return dto;
    }

    public async Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return null;

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
            var activePayments = contract.Payments.Where(p => p.IsActive).ToList();
            foreach (var payment in activePayments)
            {
                payment.IsActive = false;
                payment.DeletedAt = DateTime.UtcNow;
            }

            // H8 FIX: Re-evaluate linked invoice statuses after cancelling payments
            var affectedInvoiceIds = activePayments
                .Where(p => p.InvoiceId.HasValue)
                .Select(p => p.InvoiceId!.Value)
                .Distinct()
                .ToList();

            // Save the contract + payment changes first
            await db.SaveChangesAsync();

            // Then re-evaluate each affected invoice
            foreach (var invoiceId in affectedInvoiceIds)
            {
                try { await TryMarkInvoicePaidAsync(invoiceId); }
                catch (Exception ex) { logger.LogWarning(ex, "H8: Failed to re-evaluate invoice {InvoiceId} after contract cancellation", invoiceId); }
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

        var recentPayments = await db.Payments
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync();

        return new AccountStatementDto
        {
            PatientId        = patientId,
            PatientName      = patient.FirstName + " " + patient.LastName,
            PatientNumber    = patient.PatientNumber,
            TotalContracted  = contracts.Sum(c => c.TotalAmount),
            TotalDiscounts   = contracts.Sum(c => c.DiscountAmount),
            TotalPaid        = contracts.Sum(c => c.Payments.Sum(p => p.Amount)),
            TotalRemaining   = contracts.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount)),
            ActiveContracts  = contracts.Count(c => c.Status == ContractStatus.Active),
            CompletedContracts = contracts.Count(c => c.Status == ContractStatus.Completed),
            Contracts        = contracts.Select(c => new ContractStatementDto
            {
                Id              = c.Id,
                Specialty       = c.Specialty,
                TotalAmount     = c.TotalAmount,
                DiscountAmount  = c.DiscountAmount,
                PaidAmount      = c.Payments.Sum(p => p.Amount),
                RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount),
                StartDate       = c.StartDate?.ToString("yyyy-MM-dd"),
                Status          = c.Status.ToString(),
                InstallmentsCount  = c.InstallmentsCount,
                InstallmentAmount  = c.InstallmentAmount
            }).ToList(),
            RecentPayments = recentPayments.Select(p =>
            {
                p.Patient = patient;
                return MapPayment(p);
            }).ToList()
        };
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var todayCollected = await db.Payments
            .Where(p => p.PaymentDate == today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var monthCollected = await db.Payments
            .Where(p => p.PaymentDate >= monthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var totalOutstanding = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;

        var activeContracts = await db.Contracts.CountAsync(c => c.Status == ContractStatus.Active);

        var recentPayments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => MapPayment(p))
            .ToListAsync();

        return new FinanceSummaryDto
        {
            TodayCollected = todayCollected,
            MonthCollected = monthCollected,
            TotalOutstanding = totalOutstanding,
            ActiveContracts = activeContracts,
            RecentPayments = recentPayments
        };
    }

    public async Task<PaymentDto?> UpdatePaymentAsync(Guid id, UpdatePaymentRequest req)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null) return null;

        if (req.Amount.HasValue)       payment.Amount             = req.Amount.Value;
        if (req.PaymentMethod != null) payment.PaymentMethod      = req.PaymentMethod;
        if (req.ServiceDescription != null) payment.ServiceDescription = req.ServiceDescription;
        if (req.Specialty != null)     payment.Specialty          = req.Specialty;
        if (req.DoctorId.HasValue)     payment.DoctorId           = req.DoctorId;
        if (req.Notes != null)         payment.Notes              = req.Notes;
        if (!string.IsNullOrWhiteSpace(req.PaymentDate) && DateOnly.TryParse(req.PaymentDate, out var pd))
            payment.PaymentDate = pd;

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

        payment.IsActive  = false;
        payment.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // H3 FIX: Re-evaluate invoice status after deleting a payment.
        if (invoiceId.HasValue)
        {
            try { await TryMarkInvoicePaidAsync(invoiceId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "H3: Failed to re-evaluate invoice {InvoiceId} after payment deletion", invoiceId); }
        }

        // Re-evaluate contract status (Completed → Active if paid total drops below effective amount)
        if (contractId.HasValue)
        {
            try { await TryReconcileContractStatusAsync(contractId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to reconcile contract {ContractId} after payment deletion", contractId); }
        }

        return true;
    }

    public async Task<PaymentDto?> RefundPaymentAsync(Guid id, string? reason)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null || !payment.IsActive) return null;

        var refund = new Payment
        {
            PatientId          = payment.PatientId,
            ContractId         = payment.ContractId,
            InvoiceId          = payment.InvoiceId,
            Amount             = -payment.Amount,
            PaymentDate        = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod      = payment.PaymentMethod,
            ServiceDescription = $"استرداد: {payment.ServiceDescription ?? payment.ReceiptNumber}",
            Specialty          = payment.Specialty,
            DoctorId           = payment.DoctorId,
            BranchId           = payment.BranchId,
            ReceivedBy         = currentUser.UserId,
            ReceiptNumber      = await GenerateRefundReceiptNumberAsync(),
            Notes              = reason
        };

        db.Payments.Add(refund);
        await db.SaveChangesAsync();

        // H3 FIX: Re-evaluate invoice status after creating a refund.
        if (payment.InvoiceId.HasValue)
        {
            try { await TryMarkInvoicePaidAsync(payment.InvoiceId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "H3: Failed to re-evaluate invoice {InvoiceId} after refund", payment.InvoiceId); }
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
        var contracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId)
            .ToListAsync();

        var totalCost    = contracts.Sum(c => c.TotalAmount - c.DiscountAmount);
        var totalPaid    = contracts.Sum(c => c.Payments.Sum(p => p.Amount));
        var outstanding  = totalCost - totalPaid;

        var today          = DateOnly.FromDateTime(DateTime.Today);
        var overdueAmount  = 0m;
        foreach (var c in contracts.Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null))
        {
            var months   = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            var expected = c.DownPayment + Math.Min(months, c.InstallmentsCount) * (c.InstallmentAmount ?? 0);
            var paid     = c.Payments.Sum(p => p.Amount);
            if (expected > paid) overdueAmount += expected - paid;
        }

        var latestPayment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        var status = contracts.Count == 0 ? "no_plan"
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
            TotalPaymentsCount   = contracts.Sum(c => c.Payments.Count)
        };
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
        PaidAmount = c.Payments.Sum(p => p.Amount),
        RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount),
        InstallmentsCount = c.InstallmentsCount,
        InstallmentAmount = c.InstallmentAmount,
        StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
        Status = c.Status.ToString()
    };

    private static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        PatientId = p.PatientId,
        PatientName = p.Patient?.FirstName + " " + p.Patient?.LastName,
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
    /// Uses pg_advisory_xact_lock to prevent race conditions.
    /// </summary>
    private async Task<string> GenerateReceiptNumberAsync()
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"RCP-{datePart}-";

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
    /// </summary>
    private async Task<string> GenerateRefundReceiptNumberAsync()
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"REF-{datePart}-";

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
}
