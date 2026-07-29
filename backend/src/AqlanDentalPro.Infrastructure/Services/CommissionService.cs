using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

public class CommissionService(
    AppDbContext db,
    IJournalEntryService journalEntryService,
    ITreasuryResolutionService treasuryResolution,
    ILogger<CommissionService> logger) : ICommissionService
{
    // ── Line item commission ──────────────────────────────────────────────────

    public async Task<LineItemCommissionDto?> GetLineItemCommissionAsync(Guid lineItemId)
    {
        var item = await LoadLineItemAsync(lineItemId);
        return item == null ? null : MapLineItem(item);
    }

    public async Task<List<LineItemCommissionDto>> GetInvoiceCommissionsAsync(Guid invoiceId)
    {
        var items = await db.InvoiceLineItems
            .Include(i => i.Invoice).ThenInclude(inv => inv.Patient)
            .Include(i => i.Service)
            .Include(i => i.Doctor)
            .Where(i => i.InvoiceId == invoiceId && i.IsActive)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        return items.Select(MapLineItem).ToList();
    }

    public async Task<LineItemCommissionDto?> RecalculateAsync(Guid lineItemId)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        ApplyCalculation(item);
        await db.SaveChangesAsync();

        logger.LogInformation("Commission recalculated for line item {LineItemId}", lineItemId);
        return MapLineItem(item);
    }

    public async Task<LineItemCommissionDto?> UpdateCostsAsync(
        Guid lineItemId, UpdateLineItemCommissionRequest req, Guid updatedBy)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        if (item.CommissionStatus == CommissionStatus.Approved)
            throw new InvalidOperationException("العمولة معتمدة — يجب فتحها من قِبَل المدير قبل التعديل");
        if (item.CommissionStatus == CommissionStatus.Paid)
            throw new InvalidOperationException("العمولة مدفوعة — لا يمكن تعديل التكاليف");

        // Validate inputs
        if (req.MaterialCost is < 0 || req.LabCost is < 0 || req.OtherDirectCost is < 0)
            throw new ArgumentException("التكاليف لا يمكن أن تكون سالبة");
        if (req.DoctorCommissionPercentage is < 0 or > 100)
            throw new ArgumentException("نسبة عمولة الطبيب يجب أن تكون بين 0 و 100");

        if (req.MaterialCost.HasValue)                item.MaterialCost               = req.MaterialCost.Value;
        if (req.LabCost.HasValue)                     item.LabCost                    = req.LabCost.Value;
        if (req.OtherDirectCost.HasValue)             item.OtherDirectCost            = req.OtherDirectCost.Value;
        if (req.DoctorCommissionPercentage.HasValue)  item.DoctorCommissionPercentage = req.DoctorCommissionPercentage.Value;
        if (req.CommissionBaseRule.HasValue)          item.CommissionBaseRule         = req.CommissionBaseRule.Value;
        if (req.DoctorId.HasValue)                    item.DoctorId                   = req.DoctorId.Value;
        if (req.CommissionNotes != null)              item.CommissionNotes            = req.CommissionNotes;

        ApplyCalculation(item);
        await db.SaveChangesAsync();

        await LogAuditAsync(lineItemId, "UpdateCommissionCosts", updatedBy,
            $"MaterialCost={item.MaterialCost} LabCost={item.LabCost} Pct={item.DoctorCommissionPercentage}");

        return MapLineItem(item);
    }

    public async Task<LineItemCommissionDto?> ApproveAsync(
        Guid lineItemId, ApproveCommissionRequest req, Guid approvedBy)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        if (item.CommissionStatus == CommissionStatus.Paid)
            throw new InvalidOperationException("العمولة مدفوعة بالفعل");

        // Warn if lab order linked but lab cost is zero
        if (item.LabOrderId.HasValue && item.LabCost == 0)
            throw new InvalidOperationException("يوجد طلب معمل مرتبط ولكن تكلفة المعمل = 0. يرجى تحديث التكلفة قبل الاعتماد");

        ApplyCalculation(item);
        item.CommissionStatus     = CommissionStatus.Approved;
        item.CommissionApprovedBy = approvedBy;
        item.CommissionApprovedAt = DateTime.UtcNow;
        if (req.Notes != null) item.CommissionNotes = req.Notes;

        await db.SaveChangesAsync();
        await LogAuditAsync(lineItemId, "ApproveCommission", approvedBy,
            $"DoctorCommission={item.DoctorCommissionAmount} Net={item.NetCommissionableAmount}");

        return MapLineItem(item);
    }

    public async Task<LineItemCommissionDto?> UnlockAsync(Guid lineItemId, Guid unlockedBy)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        if (item.CommissionStatus == CommissionStatus.Paid)
            throw new InvalidOperationException("لا يمكن فتح عمولة مدفوعة");

        item.CommissionStatus     = CommissionStatus.Calculated;
        item.CommissionApprovedBy = null;
        item.CommissionApprovedAt = null;

        await db.SaveChangesAsync();
        await LogAuditAsync(lineItemId, "UnlockCommission", unlockedBy, "Commission unlocked for re-edit");

        return MapLineItem(item);
    }

    // ── Report ────────────────────────────────────────────────────────────────

    public async Task<CommissionReportResponse> GetReportAsync(
        DateOnly from, DateOnly to,
        Guid? doctorId, Guid? branchId,
        string? serviceCategory, string? commissionStatus, string? paymentStatus)
    {
        var query = db.InvoiceLineItems
            .Include(i => i.Invoice).ThenInclude(inv => inv.Patient)
            .Include(i => i.Service)
            .Include(i => i.Doctor)
            .Where(i => i.IsActive
                     && i.Invoice.IsActive
                     && i.Invoice.CreatedAt >= DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                     && i.Invoice.CreatedAt <= DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));

        if (doctorId.HasValue)
            query = query.Where(i => i.DoctorId == doctorId.Value);

        if (!string.IsNullOrWhiteSpace(commissionStatus)
            && Enum.TryParse<CommissionStatus>(commissionStatus, true, out var cs))
            query = query.Where(i => i.CommissionStatus == cs);

        var items = await query.OrderBy(i => i.Invoice.CreatedAt).ToListAsync();

        // Aggregate doctor-level payments for the same date range so the summary
        // reflects ACTUAL paid commission, not just status flags.
        var doctorIds = items.Where(i => i.DoctorId.HasValue).Select(i => i.DoctorId!.Value).Distinct().ToList();
        var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
        var paidByDoctor = await db.DoctorCommissionPayments
            .Where(p => p.IsActive
                     && doctorIds.Contains(p.DoctorId)
                     && p.PaymentDate >= DateOnly.FromDateTime(fromUtc)
                     && p.PaymentDate <= DateOnly.FromDateTime(toUtc))
            .GroupBy(p => p.DoctorId)
            .Select(g => new { DoctorId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.DoctorId, x => x.Total);

        var rows = items.Select(i => new CommissionReportRow(
            Date: i.Invoice.CreatedAt,
            PatientName: i.Invoice.Patient != null
                ? $"{i.Invoice.Patient.FirstName} {i.Invoice.Patient.LastName}".Trim()
                : "",
            InvoiceNumber: i.Invoice.InvoiceNumber,
            ServiceName: i.ServiceNameSnapshot.Length > 0 ? i.ServiceNameSnapshot : i.Description,
            DoctorName: i.Doctor?.Name,
            GrossAmount: i.TotalPrice,
            Discount: i.LineDiscountAmount,
            MaterialCost: i.MaterialCost,
            LabCost: i.LabCost,
            OtherCosts: i.OtherDirectCost,
            NetCommissionableAmount: i.NetCommissionableAmount,
            DoctorPercentage: i.DoctorCommissionPercentage,
            DoctorCommission: i.DoctorCommissionAmount,
            PaidCommission: i.CommissionStatus == CommissionStatus.Paid ? i.DoctorCommissionAmount : 0,
            RemainingCommission: i.CommissionStatus == CommissionStatus.Paid ? 0 : i.DoctorCommissionAmount,
            Status: i.CommissionStatus.ToString()
        )).ToList();

        // Summary TotalPaid uses ACTUAL DoctorCommissionPayments (more accurate
        // than per-row status flag, since payments are tracked at doctor level).
        var totalPaidActual    = paidByDoctor.Values.Sum();
        var totalEarned        = rows.Sum(r => r.DoctorCommission);
        var totalRemainingReal = Math.Max(0m, totalEarned - totalPaidActual);

        var summary = new CommissionReportSummary(
            TotalGross:           rows.Sum(r => r.GrossAmount),
            TotalDiscount:        rows.Sum(r => r.Discount),
            TotalMaterialCost:    rows.Sum(r => r.MaterialCost),
            TotalLabCost:         rows.Sum(r => r.LabCost),
            TotalOtherCosts:      rows.Sum(r => r.OtherCosts),
            TotalNet:             rows.Sum(r => r.NetCommissionableAmount),
            TotalDoctorCommission:totalEarned,
            TotalPaid:            totalPaidActual,
            TotalRemaining:       totalRemainingReal);

        return new CommissionReportResponse(summary, rows);
    }

    // ── Commission payment disbursement ───────────────────────────────────────

    public async Task<DoctorCommissionPaymentDto> RecordPaymentAsync(
        RecordCommissionPaymentRequest req, Guid recordedBy)
    {
        if (req.Amount <= 0)
            throw new ArgumentException("مبلغ الدفعة يجب أن يكون أكبر من الصفر");

        var doctor = await db.Doctors.FindAsync(req.DoctorId)
            ?? throw new ArgumentException("الطبيب غير موجود");

        // FIX: Determine valid BranchId from doctor — never write Guid.Empty
        var branchId = doctor.BranchId ?? Guid.Empty;
        if (branchId == Guid.Empty)
        {
            // Fallback: try to get branch from doctor's user account
            if (doctor.UserId != Guid.Empty)
            {
                var user = await db.Users.FindAsync(doctor.UserId);
                if (user?.BranchId.HasValue == true)
                    branchId = user.BranchId.Value;
            }
        }
        if (branchId == Guid.Empty)
            throw new ArgumentException("عذراً، لا يمكن صرف العمولة — الفرع غير محدد للطبيب. تواصل مع الإدارة.");

        // Resolve treasury by payment method
        var paymentMethod = req.PaymentMethod ?? "cash";

        // Blocker 2: Require open cashier session for cash commission payments
        CashierSession? activeSession = null;
        if (string.Equals(paymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == recordedBy && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                throw new ArgumentException("عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل صرف العمولات النقدية.");
        }

        Treasury treasury;
        try
        {
            treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, paymentMethod, null, activeSession?.Id);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        // Pre-check remaining (fast-fail before starting transaction; re-checked inside lock)
        var earned = await db.InvoiceLineItems
            .Where(i => i.DoctorId == req.DoctorId
                     && i.IsActive
                     && (i.CommissionStatus == CommissionStatus.Approved
                      || i.CommissionStatus == CommissionStatus.Paid))
            .SumAsync(i => (decimal?)i.DoctorCommissionAmount) ?? 0m;

        var alreadyPaid = await db.DoctorCommissionPayments
            .Where(p => p.DoctorId == req.DoctorId && p.IsActive)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var remaining = earned - alreadyPaid;
        if (req.Amount > remaining)
            throw new ArgumentException(
                $"المبلغ ({req.Amount:N2}) يتجاوز المتبقي المستحق للطبيب ({remaining:N2})");

        // Blocker 3: Wrap all commission payment operations in a single transaction
        // so that DoctorCommissionPayment + CashFlowTransaction + Treasury.Balance +
        // JournalEntry + InvoiceLineItem status updates all commit or roll back together.
        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            // CONCURRENCY SAFETY: Acquire advisory lock scoped to the doctor to serialize
            // all commission payments for the same doctor within the transaction.
            // Uses a stable bigint derived from the doctor Guid (not .NET GetHashCode).
            if (useTx)
            {
                var doctorLockKey = StableLockKeyHelper.StableGuidToLong(req.DoctorId);
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})", doctorLockKey);
            }

            // Re-calculate remaining approved commission inside the lock
            var lockedEarned = await db.InvoiceLineItems
                .Where(i => i.DoctorId == req.DoctorId
                         && i.IsActive
                         && (i.CommissionStatus == CommissionStatus.Approved
                          || i.CommissionStatus == CommissionStatus.Paid))
                .SumAsync(i => (decimal?)i.DoctorCommissionAmount) ?? 0m;

            var lockedAlreadyPaid = await db.DoctorCommissionPayments
                .Where(p => p.DoctorId == req.DoctorId && p.IsActive)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            remaining = lockedEarned - lockedAlreadyPaid;
            if (req.Amount > remaining)
                throw new ArgumentException(
                    $"المبلغ ({req.Amount:N2}) يتجاوز المتبقي المستحق للطبيب ({remaining:N2})");
            var payment = new DoctorCommissionPayment
            {
                DoctorId        = req.DoctorId,
                Amount          = req.Amount,
                PaymentDate     = req.PaymentDate,
                PaymentMethod   = req.PaymentMethod,
                ReferenceNumber = req.ReferenceNumber,
                Notes           = req.Notes,
                PaidBy          = recordedBy,
            };

            db.DoctorCommissionPayments.Add(payment);

            // Dual-write: CashFlowTransaction (transitional) — BranchId now resolved correctly
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var nextSeq = await db.CashFlowTransactions.CountAsync(t => t.Category == FinancialCategory.DoctorCommission) + 1;
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-COM-{nextSeq:D3}",
                Type = TransactionType.Outflow,
                Category = FinancialCategory.DoctorCommission,
                Amount = req.Amount,
                PaymentMethod = req.PaymentMethod ?? "cash",
                TransactionDate = req.PaymentDate,
                ReferenceId = payment.Id,
                ReferenceNumber = req.ReferenceNumber ?? $"COM-{doctor.Id.ToString()[..4]}",
                Description = $"صرف عمولة الطبيب: {doctor.Name}",
                PerformedBy = recordedBy,
                BranchId = branchId,
                TreasuryId = treasury.Id,
                CashierSessionId = activeSession?.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            // Dual-write: JournalEntry (canonical) — Debit Expense / Credit Treasury
            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.CommissionPayment,
                financialDocumentId: payment.Id,
                description: $"صرف عمولة طبيب: {doctor.Name}",
                entryDate: req.PaymentDate,
                branchId: branchId,
                performedBy: recordedBy,
                cashierSessionId: activeSession?.Id,
                treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.Expense, payment.Id, req.Amount, 0m, (string?)$"عمولة: {doctor.Name}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, req.Amount, (string?)$"سداد من: {treasury.Name}")
                });
            je.IsPosted = true;
            je.PostedAt = DateTime.UtcNow;

            // Blocker 1: Atomically decrement Treasury.Balance for the commission outflow
            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, paymentMethod, req.Amount, null, activeSession?.Id);

            // Mark specified line items as Paid
            if (req.LineItemIds is { Count: > 0 })
            {
                var lineItems = await db.InvoiceLineItems
                    .Where(i => req.LineItemIds.Contains(i.Id) && i.IsActive)
                    .ToListAsync();

                foreach (var item in lineItems)
                {
                    if (item.CommissionStatus == CommissionStatus.Approved)
                        item.CommissionStatus = CommissionStatus.Paid;
                }
            }

            await db.SaveChangesAsync();
            if (useTx) await tx!.CommitAsync();

            await LogAuditAsync(payment.Id, "RecordCommissionPayment", recordedBy,
                $"DoctorId={req.DoctorId} Amount={req.Amount} Method={req.PaymentMethod}");

            return new DoctorCommissionPaymentDto(
                Id: payment.Id,
                DoctorId: payment.DoctorId,
                DoctorName: doctor.Name,
                Amount: payment.Amount,
                PaymentDate: payment.PaymentDate,
                PaymentMethod: payment.PaymentMethod,
                ReferenceNumber: payment.ReferenceNumber,
                Notes: payment.Notes,
                CreatedAt: payment.CreatedAt);
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }
    }

    public async Task<List<DoctorCommissionPaymentDto>> GetPaymentsAsync(Guid? doctorId)
    {
        var query = db.DoctorCommissionPayments
            .Include(p => p.Doctor)
            .Where(p => p.IsActive);

        if (doctorId.HasValue)
            query = query.Where(p => p.DoctorId == doctorId.Value);

        var list = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();

        return list.Select(p => new DoctorCommissionPaymentDto(
            Id: p.Id,
            DoctorId: p.DoctorId,
            DoctorName: p.Doctor.Name,
            Amount: p.Amount,
            PaymentDate: p.PaymentDate,
            PaymentMethod: p.PaymentMethod,
            ReferenceNumber: p.ReferenceNumber,
            Notes: p.Notes,
            CreatedAt: p.CreatedAt)).ToList();
    }

    // ── Service commission defaults ───────────────────────────────────────────

    public async Task<ServiceCommissionDefaultsDto?> GetServiceDefaultsAsync(Guid serviceId)
    {
        var svc = await db.ClinicServices.FindAsync(serviceId);
        return svc == null ? null : MapServiceDefaults(svc);
    }

    public async Task<ServiceCommissionDefaultsDto?> UpdateServiceDefaultsAsync(
        Guid serviceId, UpdateServiceCommissionDefaultsRequest req)
    {
        if (req.DefaultMaterialCost < 0)
            throw new ArgumentException("تكلفة المواد لا يمكن أن تكون سالبة");
        if (req.DefaultLabCost < 0)
            throw new ArgumentException("تكلفة المعمل لا يمكن أن تكون سالبة");
        if (req.DefaultDoctorCommissionPercentage is < 0 or > 100)
            throw new ArgumentException("نسبة العمولة يجب أن تكون بين 0 و 100");
        if (!Enum.IsDefined(req.DefaultMaterialCostType))
            throw new ArgumentException("نوع تكلفة المواد غير صالح");
        if (!Enum.IsDefined(req.CommissionBaseRule))
            throw new ArgumentException("أساس العمولة غير صالح");
        if (!Enum.IsDefined(req.CommissionRecognitionMode))
            throw new ArgumentException("وقت احتساب العمولة غير صالح");

        var svc = await db.ClinicServices.FindAsync(serviceId);
        if (svc == null) return null;

        svc.DefaultMaterialCost               = req.DefaultMaterialCost;
        svc.DefaultMaterialCostType           = req.DefaultMaterialCostType;
        svc.DefaultLabCost                    = req.DefaultLabCost;
        svc.DefaultDoctorCommissionPercentage = req.DefaultDoctorCommissionPercentage;
        svc.CommissionBaseRule                = req.CommissionBaseRule;
        svc.CommissionRecognitionMode         = req.CommissionRecognitionMode;

        await db.SaveChangesAsync();
        return MapServiceDefaults(svc);
    }

    // ── Auto-fill from service defaults ──────────────────────────────────────

    public async Task AutoFillFromServiceAsync(Guid lineItemId)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null || item.ServiceId == null) return;

        var svc = await db.ClinicServices.FindAsync(item.ServiceId.Value);
        if (svc == null) return;

        // Resolve material cost (fixed or percentage)
        item.MaterialCost = CommissionCalculator.ResolveMaterialCost(
            item.UnitPrice,
            svc.DefaultMaterialCost,
            svc.DefaultMaterialCostType);

        item.LabCost = svc.DefaultLabCost;
        item.CommissionBaseRule = svc.CommissionBaseRule;

        // Doctor commission %: service default overrides doctor default, then the
        // configurable clinic-wide default (MS-TASK-006: this is a money rate —
        // it must come from Settings, and the key already existed unused here).
        if (svc.DefaultDoctorCommissionPercentage.HasValue)
        {
            item.DoctorCommissionPercentage = svc.DefaultDoctorCommissionPercentage.Value;
        }
        else if (item.DoctorId.HasValue)
        {
            var doctor = await db.Doctors.FindAsync(item.DoctorId.Value);
            if (doctor?.DefaultCommissionPercentage.HasValue == true)
                item.DoctorCommissionPercentage = doctor.DefaultCommissionPercentage.Value;
            else
                item.DoctorCommissionPercentage = await new FinanceSettingsReader(db)
                    .GetDecimalAsync(FinanceSettingsKeys.CommissionDefaultDoctorPercentage);
        }
        else
        {
            item.DoctorCommissionPercentage = await new FinanceSettingsReader(db)
                .GetDecimalAsync(FinanceSettingsKeys.CommissionDefaultDoctorPercentage);
        }

        // LAB-FINANCE-ROOT-CAUSE-2: nothing in the codebase ever set InvoiceLineItem.LabOrderId —
        // the field existed and the "pull actual lab cost" block below was already written to use
        // it, but every draft/manual invoice line item was created without it, so commission always
        // fell back to the service's DefaultLabCost instead of what the lab actually charged for
        // THIS order (e.g. a discounted or renegotiated cost). Resolve it here from the line item's
        // visit: a visit normally has exactly one non-cancelled lab order, so that's the one whose
        // cost belongs to this line item. Multiple candidates means the line item's single LabCost
        // field cannot unambiguously represent all of them, so it is deliberately left unlinked
        // rather than guessing — the resolver only fills the field when the match is exact.
        if (!item.LabOrderId.HasValue && item.RelatedVisitId.HasValue)
        {
            var candidateLabOrderIds = await db.LabOrders
                .Where(lo => lo.VisitId == item.RelatedVisitId.Value && lo.IsActive && lo.Status != "cancelled")
                .Select(lo => lo.Id)
                .ToListAsync();
            if (candidateLabOrderIds.Count == 1)
                item.LabOrderId = candidateLabOrderIds[0];
        }

        // If linked lab order exists, pull actual lab cost
        if (item.LabOrderId.HasValue)
        {
            var labOrder = await db.LabOrders.FindAsync(item.LabOrderId.Value);
            // CLIN-08 FIX: Prefer TotalCost (includes items + remake) over Cost (snapshot that may be stale).
            // Previously read only Cost, which LabOrdersController.Update never re-syncs — so a lab order
            // whose TotalCost grew via remakes would still report the old Cost to commission calculations,
            // inflating the doctor's commission (lab cost is a deduction from NetCommissionableAmount).
            if (labOrder != null)
            {
                item.LabCost = labOrder.TotalCost ?? labOrder.Cost ?? 0;
            }
        }

        ApplyCalculation(item);
        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<InvoiceLineItem?> LoadLineItemAsync(Guid id) =>
        await db.InvoiceLineItems
            .Include(i => i.Invoice).ThenInclude(inv => inv.Patient)
            .Include(i => i.Service)
            .Include(i => i.Doctor)
            .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);

    private static void ApplyCalculation(InvoiceLineItem item)
    {
        var result = CommissionCalculator.Calculate(new CommissionCalculator.Input(
            TotalPrice:                item.TotalPrice,
            LineDiscountAmount:        item.LineDiscountAmount,
            MaterialCost:              item.MaterialCost,
            LabCost:                   item.LabCost,
            OtherDirectCost:           item.OtherDirectCost,
            DoctorCommissionPercentage:item.DoctorCommissionPercentage,
            BaseRule:                  item.CommissionBaseRule));

        item.NetCommissionableAmount = result.NetCommissionableAmount;

        // FIN-09 FIX: When the service uses OnPaymentCollection recognition mode,
        // do NOT overwrite DoctorCommissionAmount with the full amount. TriggerOnPaymentCommissionsAsync
        // sets a proportional DoctorCommissionAmount based on collected payments; overwriting it here
        // (from Recalculate/Approve/AutoFill) would reset the doctor's commission to the full accrual
        // amount, effectively paying commission on uncollected revenue.
        if (item.Service?.CommissionRecognitionMode != CommissionRecognitionMode.OnPaymentCollection)
        {
            item.DoctorCommissionAmount  = result.DoctorCommissionAmount;
            item.CenterShareAmount       = result.CenterShareAmount;
        }
        else
        {
            // Keep the proportional DoctorCommissionAmount (set by TriggerOnPaymentCommissionsAsync).
            // Recalculate CenterShare based on the proportional doctor amount.
            item.CenterShareAmount = result.NetCommissionableAmount - item.DoctorCommissionAmount;
        }

        if (item.CommissionStatus == CommissionStatus.Pending && item.DoctorCommissionPercentage > 0)
            item.CommissionStatus = CommissionStatus.Calculated;
    }

    private static LineItemCommissionDto MapLineItem(InvoiceLineItem i) => new(
        LineItemId:                i.Id,
        InvoiceId:                 i.InvoiceId,
        InvoiceNumber:             i.Invoice.InvoiceNumber,
        PatientName:               i.Invoice.Patient != null
            ? $"{i.Invoice.Patient.FirstName} {i.Invoice.Patient.LastName}".Trim()
            : "",
        ServiceName:               i.ServiceNameSnapshot.Length > 0 ? i.ServiceNameSnapshot : i.Description,
        DoctorId:                  i.DoctorId,
        DoctorName:                i.Doctor?.Name,
        TotalPrice:                i.TotalPrice,
        LineDiscountAmount:        i.LineDiscountAmount,
        MaterialCost:              i.MaterialCost,
        LabCost:                   i.LabCost,
        OtherDirectCost:           i.OtherDirectCost,
        NetCommissionableAmount:   i.NetCommissionableAmount,
        DoctorCommissionPercentage:i.DoctorCommissionPercentage,
        DoctorCommissionAmount:    i.DoctorCommissionAmount,
        CenterShareAmount:         i.CenterShareAmount,
        CommissionStatus:          i.CommissionStatus.ToString(),
        CommissionNotes:           i.CommissionNotes,
        HasLabOrder:               i.LabOrderId.HasValue,
        LabCostMissing:            i.LabOrderId.HasValue && i.LabCost == 0,
        IsApproved:                i.CommissionStatus == CommissionStatus.Approved || i.CommissionStatus == CommissionStatus.Paid,
        CommissionApprovedAt:      i.CommissionApprovedAt,
        CreatedAt:                 i.CreatedAt);

    private static ServiceCommissionDefaultsDto MapServiceDefaults(Domain.Entities.ClinicService svc) => new(
        ServiceId:                        svc.Id,
        DefaultMaterialCost:              svc.DefaultMaterialCost,
        DefaultMaterialCostType:          svc.DefaultMaterialCostType.ToString(),
        DefaultLabCost:                   svc.DefaultLabCost,
        DefaultDoctorCommissionPercentage:svc.DefaultDoctorCommissionPercentage,
        CommissionBaseRule:               svc.CommissionBaseRule.ToString(),
        CommissionRecognitionMode:        svc.CommissionRecognitionMode.ToString());

    public async Task<Guid?> GetDoctorIdForUserAsync(Guid userId)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && d.IsActive);
        return doctor?.Id;
    }

    public async Task TriggerOnPaymentCommissionsAsync(Guid invoiceId)
    {
        var invoice = await db.Invoices
            .Include(i => i.Payments.Where(p => p.IsActive))
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.IsActive);

        if (invoice == null) return;

        if (invoice.TotalAmount <= 0) return;

        var directPayments = invoice.Payments.Sum(p => p.AppliedAmount == 0m ? p.Amount : p.AppliedAmount);
        var allocatedAdvances = await db.PaymentAllocations
            .Where(a => a.InvoiceId == invoiceId && a.IsActive)
            .SumAsync(a => (decimal?)a.Amount) ?? 0m;
        var totalPaid = directPayments + allocatedAdvances;
        var paidRatio = Math.Min(1m, totalPaid / invoice.TotalAmount);

        var items = await db.InvoiceLineItems
            .Include(i => i.Service)
            .Where(i => i.InvoiceId == invoiceId && i.IsActive
                     && i.Service != null
                     && i.Service.CommissionRecognitionMode == CommissionRecognitionMode.OnPaymentCollection
                     && i.CommissionStatus != CommissionStatus.Pending)
            .ToListAsync();

        foreach (var item in items)
        {
            // Compute the FULL commission first, then apply proportional ratio
            // to BOTH doctor share and center share so the split stays consistent
            // with the collected portion of the invoice.
            var full = CommissionCalculator.Calculate(new CommissionCalculator.Input(
                TotalPrice:                 item.TotalPrice,
                LineDiscountAmount:         item.LineDiscountAmount,
                MaterialCost:               item.MaterialCost,
                LabCost:                    item.LabCost,
                OtherDirectCost:            item.OtherDirectCost,
                DoctorCommissionPercentage: item.DoctorCommissionPercentage,
                BaseRule:                   item.CommissionBaseRule));

            item.NetCommissionableAmount = full.NetCommissionableAmount;
            item.DoctorCommissionAmount  = CommissionCalculator.ProportionalCommission(full.DoctorCommissionAmount, paidRatio);
            item.CenterShareAmount       = CommissionCalculator.ProportionalCommission(full.CenterShareAmount, paidRatio);
        }

        if (items.Count > 0)
            await db.SaveChangesAsync();
    }

    public async Task<List<LineItemCommissionDto>> GetBackfillPreviewAsync(DateOnly from, DateOnly to, Guid? doctorId)
    {
        var query = db.InvoiceLineItems
            .Include(i => i.Invoice).ThenInclude(inv => inv.Patient)
            .Include(i => i.Service)
            .Include(i => i.Doctor)
            .Where(i => i.IsActive
                     && i.Invoice.IsActive
                     && i.Invoice.CreatedAt >= DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                     && i.Invoice.CreatedAt <= DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc)
                     && i.CommissionStatus == CommissionStatus.Pending);

        if (doctorId.HasValue)
            query = query.Where(i => i.DoctorId == doctorId.Value);

        var items = await query.ToListAsync();

        // Compute what the commission WOULD be without saving
        var result = new List<LineItemCommissionDto>();
        foreach (var item in items)
        {
            var calc = CommissionCalculator.Calculate(new CommissionCalculator.Input(
                TotalPrice:                 item.TotalPrice,
                LineDiscountAmount:         item.LineDiscountAmount,
                MaterialCost:               item.MaterialCost,
                LabCost:                    item.LabCost,
                OtherDirectCost:            item.OtherDirectCost,
                DoctorCommissionPercentage: item.DoctorCommissionPercentage,
                BaseRule:                   item.CommissionBaseRule));

            // Build a preview DTO without persisting
            var preview = MapLineItem(item) with
            {
                NetCommissionableAmount    = calc.NetCommissionableAmount,
                DoctorCommissionAmount     = calc.DoctorCommissionAmount,
                CenterShareAmount          = calc.CenterShareAmount,
                CommissionStatus           = "Preview",
            };
            result.Add(preview);
        }

        return result;
    }

    private async Task LogAuditAsync(Guid entityId, string action, Guid userId, string details)
    {
        // FIN-14 FIX: Removed the try/catch that swallowed audit-log failures. For financial mutations
        // (commission approve/unlock/payment), audit failures should propagate and roll back the caller's
        // transaction — not be silently logged as a warning. A missing audit entry for a commission
        // approval is a compliance gap that enables plausible deniability.
        var auditAction = action.Contains("Approve") ? AuditAction.Approve : AuditAction.Update;
        db.AuditLogs.Add(new AuditLog
        {
            Resource   = $"InvoiceLineItem.Commission",
            ResourceId = entityId,
            Action     = auditAction,
            UserId     = userId,
            NewData    = System.Text.Json.JsonSerializer.SerializeToDocument(new { action, details }),
        });
        await db.SaveChangesAsync();
    }

    // StableGuidToLong moved to StableLockKeyHelper — shared across all finance services.
}
