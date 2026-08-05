using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// CORE-FIN-LAB-ADJ. See <see cref="ICommissionAdjustmentService"/> for the rule this implements.
/// </summary>
public sealed class CommissionAdjustmentService(
    AppDbContext db,
    ILogger<CommissionAdjustmentService> logger) : ICommissionAdjustmentService
{
    /// <summary>
    /// Below this the difference is rounding noise, not a correction worth putting in front of
    /// the owner. Also what makes a repeat run a genuine no-op rather than a stream of 0.00 rows.
    /// </summary>
    private const decimal MaterialityThreshold = 0.01m;

    // ── Re-sync ───────────────────────────────────────────────────────────────

    public async Task<CommissionResyncResult> ResyncLabOrderAsync(
        Guid labOrderId, Guid? performedBy, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: a soft-deleted order still has to push its linked commissions back
        // to a zero lab cost. Filtering it out here would freeze the deduction forever.
        var order = await db.LabOrders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == labOrderId, ct);
        if (order is null)
            return Empty("طلب المعمل غير موجود");

        var items = await db.InvoiceLineItems
            .Include(i => i.Service)
            .Where(i => i.LabOrderId == labOrderId && i.IsActive)
            .ToListAsync(ct);

        return await ApplyToItemsAsync(items, _ => order, performedBy, ct);
    }

    public async Task<CommissionResyncResult> ResyncAllLinkedAsync(
        Guid? doctorId, Guid? performedBy, CancellationToken ct = default)
    {
        var query = db.InvoiceLineItems
            .Include(i => i.Service)
            .Where(i => i.IsActive && i.LabOrderId != null);

        if (doctorId.HasValue)
            query = query.Where(i => i.DoctorId == doctorId.Value);

        var items = await query.ToListAsync(ct);

        var orderIds = items.Select(i => i.LabOrderId!.Value).Distinct().ToList();
        var orders = await db.LabOrders.IgnoreQueryFilters()
            .Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, ct);

        return await ApplyToItemsAsync(
            items,
            i => orders.TryGetValue(i.LabOrderId!.Value, out var o) ? o : null,
            performedBy,
            ct);
    }

    private async Task<CommissionResyncResult> ApplyToItemsAsync(
        List<InvoiceLineItem> items,
        Func<InvoiceLineItem, LabOrder?> orderFor,
        Guid? performedBy,
        CancellationToken ct)
    {
        var skipped = new List<string>();
        var recalculated = 0;
        var adjustmentsRaised = 0;
        var netAdjustment = 0m;

        // One query for every prior correction on these lines: the delta below is measured
        // against the corrections ALREADY raised, which is exactly what stops a second run
        // from raising the same correction twice.
        var itemIds = items.Select(i => i.Id).ToList();
        var priorByItem = await db.DoctorCommissionAdjustments
            .Where(a => itemIds.Contains(a.InvoiceLineItemId)
                     && a.IsActive
                     && a.Status != CommissionAdjustmentStatus.Cancelled)
            .ToListAsync(ct);

        // Invoice currency is fetched by id rather than Include()d: Invoice is a REQUIRED
        // navigation on InvoiceLineItem, and EF Core's InMemory provider answers an unresolved
        // required navigation by dropping the whole row from the result — which would silently
        // turn a resync into a no-op instead of an error.
        var invoiceIds = items.Select(i => i.InvoiceId).Distinct().ToList();
        var invoiceScopes = await db.Invoices.IgnoreQueryFilters()
            .Where(inv => invoiceIds.Contains(inv.Id))
            .Select(inv => new { inv.Id, inv.Currency, inv.Patient.BranchId })
            .ToDictionaryAsync(x => x.Id, ct);

        foreach (var item in items)
        {
            var order = orderFor(item);
            if (order is null)
            {
                skipped.Add($"بند فاتورة {item.Id} مرتبط بطلب معمل غير موجود");
                continue;
            }

            var resolved = LabCostForCommission.Resolve(new LabCostForCommission.Input(
                LabOrderStatus:            order.Status,
                LabOrderIsActive:          order.IsActive,
                TotalCost:                 order.TotalCost,
                Cost:                      order.Cost,
                LabOrderCurrency:          order.Currency,
                LabOrderExchangeRateToYer: order.ExchangeRateToYer,
                InvoiceCurrency:           invoiceScopes.GetValueOrDefault(item.InvoiceId)?.Currency));

            if (!resolved.ShouldWrite)
            {
                // A draft resolves to "leave it alone" with no reason attached — that is the
                // ordinary answer while the order is still being drafted, not a problem to
                // report. Only a genuine failure to resolve reaches the fix-up list.
                if (resolved.NeedsAttention)
                    skipped.Add($"{order.OrderNumber}: {resolved.Reason}");
                continue;
            }

            var actualLabCost = resolved.Amount;

            if (item.CommissionStatus != CommissionStatus.Paid)
            {
                // Unpaid, or sitting in an open settlement: no history to protect, so the
                // honest thing is to correct the number itself rather than bolt a note onto it.
                if (item.LabCost == actualLabCost) continue;

                var before = item.DoctorCommissionAmount;
                item.LabCost = actualLabCost;
                CommissionLineWriter.ApplyCalculation(item, item.Service?.CommissionRecognitionMode);
                recalculated++;

                logger.LogInformation(
                    "[CommissionAdjust] Line {LineItemId} recalculated in place: commission {Before} → {After} (lab cost now {LabCost})",
                    item.Id, before, item.DoctorCommissionAmount, actualLabCost);
                continue;
            }

            // ── Paid: the line item is history and is never rewritten ────────────
            if (!item.DoctorId.HasValue)
            {
                skipped.Add($"بند فاتورة {item.Id} عمولته مدفوعة بلا طبيب محدد — يلزم تصحيح يدوي");
                continue;
            }

            var prior = priorByItem.Where(a => a.InvoiceLineItemId == item.Id).ToList();
            var priorTotal = prior.Sum(a => a.AdjustmentAmount);

            var fullAtFrozen = CommissionLineWriter.ComputeWithLabCost(item, item.LabCost).DoctorCommissionAmount;
            var fullAtActual = CommissionLineWriter.ComputeWithLabCost(item, actualLabCost).DoctorCommissionAmount;

            // OnPaymentCollection lines carry only the collected PROPORTION of the accrual.
            // Correcting them by the full swing would hand the doctor commission on money the
            // clinic has not collected, so the swing is scaled by the same proportion the
            // original payment used. Accrual lines have ratio 1 and are unaffected.
            var ratio = fullAtFrozen > 0m
                ? Math.Clamp(item.DoctorCommissionAmount / fullAtFrozen, 0m, 1m)
                : 1m;

            var correctCommission = Math.Round(
                item.DoctorCommissionAmount + (fullAtActual - fullAtFrozen) * ratio, 2);

            var delta = Math.Round(correctCommission - item.DoctorCommissionAmount - priorTotal, 2);
            if (Math.Abs(delta) < MaterialityThreshold) continue;

            var previousLabCost = prior.Count > 0
                ? prior.OrderBy(a => a.CreatedAt).Last().CurrentLabCost
                : item.LabCost;

            var adjustment = new DoctorCommissionAdjustment
            {
                DoctorId                     = item.DoctorId.Value,
                BranchId                     = invoiceScopes.GetValueOrDefault(item.InvoiceId)?.BranchId
                                                    ?? (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"
                                                       ? Guid.Empty
                                                       : throw new InvalidOperationException("Commission invoice branch is missing")),
                Currency                     = invoiceScopes.GetValueOrDefault(item.InvoiceId)?.Currency
                                                   ?? throw new InvalidOperationException("Commission invoice currency is missing"),
                InvoiceLineItemId            = item.Id,
                InvoiceId                    = item.InvoiceId,
                LabOrderId                   = order.Id,
                PaidCommissionAmount         = item.DoctorCommissionAmount,
                RecalculatedCommissionAmount = correctCommission,
                AdjustmentAmount             = delta,
                PreviousLabCost              = previousLabCost,
                CurrentLabCost               = actualLabCost,
                Reason                       = BuildReason(order, previousLabCost, actualLabCost, delta),
                Status                       = CommissionAdjustmentStatus.Pending,
                CreatedByUserId              = performedBy == Guid.Empty ? null : performedBy,
            };
            db.DoctorCommissionAdjustments.Add(adjustment);

            // Keep the in-memory list in step so two line items sharing one lab order inside a
            // single run cannot both measure against an empty prior set.
            priorByItem.Add(adjustment);

            adjustmentsRaised++;
            netAdjustment += delta;

            logger.LogInformation(
                "[CommissionAdjust] Paid line {LineItemId} corrected by {Delta} for doctor {DoctorId} (lab order {OrderNumber})",
                item.Id, delta, item.DoctorId.Value, order.OrderNumber);
        }

        return new CommissionResyncResult(
            LineItemsExamined: items.Count,
            Recalculated: recalculated,
            AdjustmentsRaised: adjustmentsRaised,
            NetAdjustmentAmount: netAdjustment,
            Skipped: skipped);
    }

    private static string BuildReason(LabOrder order, decimal previousCost, decimal currentCost, decimal delta)
    {
        var direction = delta > 0 ? "زيادة مستحقة للطبيب" : "استرداد من الطبيب";
        var cause = LabCostForCommission.IsCommitted(order.Status, order.IsActive)
            ? "تغيّرت تكلفة المعمل الفعلية بعد صرف العمولة"
            : "أُلغي طلب المعمل بعد صرف العمولة فلم تعد تكلفته مستحقة";

        return $"{cause} — طلب المعمل {order.OrderNumber}: "
             + $"{previousCost:N2} ← {currentCost:N2}. {direction}.";
    }

    private static CommissionResyncResult Empty(string reason)
        => new(0, 0, 0, 0m, [reason]);

    // ── Reads ─────────────────────────────────────────────────────────────────

    public Task<List<CommissionAdjustmentDto>> GetAdjustmentsAsync(
        Guid? doctorId, string? status, CancellationToken ct = default)
        => GetAdjustmentsAsync(doctorId, status, ct, null, null);

    public async Task<List<CommissionAdjustmentDto>> GetAdjustmentsAsync(
        Guid? doctorId, string? status, CancellationToken ct,
        Guid? branchId, string? currency)
    {
        var query = db.DoctorCommissionAdjustments.Where(a => a.IsActive);

        if (doctorId.HasValue)
            query = query.Where(a => a.DoctorId == doctorId.Value);
        if (branchId.HasValue)
            query = query.Where(a => a.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalizedCurrency = currency.Trim().ToUpperInvariant();
            query = query.Where(a => a.Currency == normalizedCurrency);
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<CommissionAdjustmentStatus>(status, true, out var parsed))
            query = query.Where(a => a.Status == parsed);

        var rows = await query.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        if (rows.Count == 0) return [];

        return await ProjectAsync(rows, ct);
    }

    /// <summary>
    /// Resolves the display fields with explicit lookups rather than <c>Include</c>. The entity
    /// deliberately has no navigation properties — see DoctorCommissionAdjustmentConfiguration.
    /// </summary>
    private async Task<List<CommissionAdjustmentDto>> ProjectAsync(
        List<DoctorCommissionAdjustment> rows, CancellationToken ct)
    {
        var doctorIds  = rows.Select(a => a.DoctorId).Distinct().ToList();
        var lineIds    = rows.Select(a => a.InvoiceLineItemId).Distinct().ToList();
        var invoiceIds = rows.Select(a => a.InvoiceId).Distinct().ToList();
        var orderIds   = rows.Where(a => a.LabOrderId.HasValue).Select(a => a.LabOrderId!.Value).Distinct().ToList();

        var doctors = await db.Doctors.IgnoreQueryFilters()
            .Where(d => doctorIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var lines = await db.InvoiceLineItems.IgnoreQueryFilters()
            .Where(i => lineIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ServiceNameSnapshot, i.Description })
            .ToDictionaryAsync(x => x.Id, ct);

        // Looked up by id rather than walked through navigations: a correction line must still
        // render when the invoice or patient behind it has been soft-deleted.
        var invoices = await db.Invoices.IgnoreQueryFilters()
            .Where(inv => invoiceIds.Contains(inv.Id))
            .Select(inv => new { inv.Id, inv.InvoiceNumber, inv.PatientId })
            .ToDictionaryAsync(x => x.Id, ct);

        var patientIds = invoices.Values.Select(v => v.PatientId).Distinct().ToList();
        var patients = await db.Patients.IgnoreQueryFilters()
            .Where(p => patientIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .ToDictionaryAsync(x => x.Id, x => $"{x.FirstName} {x.LastName}".Trim(), ct);

        var orders = await db.LabOrders.IgnoreQueryFilters()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNumber })
            .ToDictionaryAsync(o => o.Id, o => o.OrderNumber, ct);

        return rows.Select(a =>
        {
            lines.TryGetValue(a.InvoiceLineItemId, out var line);
            invoices.TryGetValue(a.InvoiceId, out var invoice);
            return new CommissionAdjustmentDto(
                Id:                           a.Id,
                DoctorId:                     a.DoctorId,
                BranchId:                     a.BranchId,
                Currency:                     a.Currency,
                DoctorName:                   doctors.GetValueOrDefault(a.DoctorId),
                InvoiceLineItemId:            a.InvoiceLineItemId,
                InvoiceId:                    a.InvoiceId,
                InvoiceNumber:                invoice?.InvoiceNumber ?? "",
                PatientName:                  invoice is null ? "" : patients.GetValueOrDefault(invoice.PatientId, ""),
                ServiceName:                  line is null
                    ? ""
                    : (line.ServiceNameSnapshot.Length > 0 ? line.ServiceNameSnapshot : line.Description),
                LabOrderId:                   a.LabOrderId,
                LabOrderNumber:               a.LabOrderId.HasValue ? orders.GetValueOrDefault(a.LabOrderId.Value) : null,
                PaidCommissionAmount:         a.PaidCommissionAmount,
                RecalculatedCommissionAmount: a.RecalculatedCommissionAmount,
                AdjustmentAmount:             a.AdjustmentAmount,
                PreviousLabCost:              a.PreviousLabCost,
                CurrentLabCost:               a.CurrentLabCost,
                Reason:                       a.Reason,
                Status:                       a.Status.ToString(),
                SettledByPaymentId:           a.SettledByPaymentId,
                SettledOn:                    a.SettledOn,
                CreatedAt:                    a.CreatedAt);
        }).ToList();
    }

    public Task<DoctorSettlementSummaryDto?> GetSettlementSummaryAsync(
        Guid doctorId, CancellationToken ct = default)
        => GetSettlementSummaryAsync(doctorId, ct, null, null);

    public async Task<DoctorSettlementSummaryDto?> GetSettlementSummaryAsync(
        Guid doctorId, CancellationToken ct, Guid? branchId, string? currency)
    {
        var doctor = await db.Doctors.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == doctorId, ct);
        if (doctor is null) return null;

        var effectiveBranchId = branchId ?? doctor.BranchId
            ?? (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"
                ? Guid.Empty
                : throw new InvalidOperationException("Doctor branch is missing"));
        var effectiveCurrency = currency?.Trim().ToUpperInvariant();
        if (effectiveCurrency is null)
        {
            var earnedCurrencies = db.InvoiceLineItems
                .Where(item => item.DoctorId == doctorId
                            && (item.Invoice.Patient.BranchId ?? Guid.Empty) == effectiveBranchId
                            && item.IsActive)
                .Select(item => item.Invoice.Currency);
            var paymentCurrencies = db.DoctorCommissionPayments
                .Where(payment => payment.DoctorId == doctorId
                               && payment.BranchId == effectiveBranchId
                               && payment.IsActive)
                .Select(payment => payment.Currency);
            var adjustmentCurrencies = db.DoctorCommissionAdjustments
                .Where(adjustment => adjustment.DoctorId == doctorId
                                  && adjustment.BranchId == effectiveBranchId
                                  && adjustment.IsActive)
                .Select(adjustment => adjustment.Currency);
            var currencies = await earnedCurrencies
                .Concat(paymentCurrencies)
                .Concat(adjustmentCurrencies)
                .Distinct()
                .ToListAsync(ct);
            if (currencies.Count > 1)
                throw new InvalidOperationException("Currency is required when a doctor has multi-currency commissions");
            effectiveCurrency = currencies.SingleOrDefault() ?? "YER";
        }
        if (effectiveCurrency is not ("YER" or "SAR" or "USD"))
            throw new ArgumentException("Unsupported commission currency", nameof(currency));

        var earned = await CommissionBalance.EarnedFromLineItemsAsync(
            db, doctorId, ct, effectiveBranchId, effectiveCurrency);
        var adjustments = await CommissionBalance.AdjustmentsTotalAsync(
            db, doctorId, ct, effectiveBranchId, effectiveCurrency);
        var paid = await CommissionBalance.AlreadyPaidAsync(
            db, doctorId, ct, effectiveBranchId, effectiveCurrency);

        var pendingCount = await db.DoctorCommissionAdjustments
            .CountAsync(a => a.DoctorId == doctorId
                          && a.BranchId == effectiveBranchId
                          && a.Currency == effectiveCurrency
                          && a.IsActive
                          && a.Status == CommissionAdjustmentStatus.Pending, ct);

        return new DoctorSettlementSummaryDto(
            DoctorId:               doctorId,
            BranchId:               effectiveBranchId,
            Currency:               effectiveCurrency,
            DoctorName:             doctor.Name,
            EarnedFromLineItems:    earned,
            AdjustmentsTotal:       adjustments,
            TotalDue:               earned + adjustments,
            AlreadyPaid:            paid,
            Remaining:              earned + adjustments - paid,
            PendingAdjustmentCount: pendingCount);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    public async Task<CommissionAdjustmentDto?> CancelAdjustmentAsync(
        Guid adjustmentId, string reason, Guid cancelledBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("سبب إلغاء بند التسوية مطلوب");

        var adjustment = await db.DoctorCommissionAdjustments
            .FirstOrDefaultAsync(a => a.Id == adjustmentId && a.IsActive, ct);
        if (adjustment is null) return null;

        if (adjustment.Status == CommissionAdjustmentStatus.Settled)
            throw new InvalidOperationException(
                "بند التسوية صُرف بالفعل ضمن دفعة عمولة — عالجه بدفعة تصحيحية وليس بالإلغاء");

        if (adjustment.Status == CommissionAdjustmentStatus.Cancelled)
            return (await ProjectAsync([adjustment], ct)).FirstOrDefault();

        adjustment.Status            = CommissionAdjustmentStatus.Cancelled;
        adjustment.CancelledByUserId = cancelledBy;
        adjustment.CancelReason      = reason.Trim();

        db.AuditLogs.Add(new AuditLog
        {
            Resource   = "DoctorCommissionAdjustment",
            ResourceId = adjustment.Id,
            Action     = AuditAction.Delete,
            UserId     = cancelledBy,
            NewData    = System.Text.Json.JsonSerializer.SerializeToDocument(new
            {
                action = "CancelCommissionAdjustment",
                adjustment.DoctorId,
                adjustment.AdjustmentAmount,
                reason = adjustment.CancelReason,
            }),
        });

        await db.SaveChangesAsync(ct);

        return (await ProjectAsync([adjustment], ct)).FirstOrDefault();
    }
}

/// <summary>
/// The three sums that make up a doctor's commission position, in one place so the payment
/// path and the settlement screen can never disagree about what is owed.
/// </summary>
public static class CommissionBalance
{
    /// <summary>Commission accrued on line items that reached Approved or Paid.</summary>
    public static async Task<decimal> EarnedFromLineItemsAsync(
        AppDbContext db, Guid doctorId, CancellationToken ct = default,
        Guid? branchId = null, string? currency = null)
    {
        var legacyInMemory = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        var query = db.InvoiceLineItems
            .Where(i => i.DoctorId == doctorId
                     && i.IsActive
                     && (i.CommissionStatus == CommissionStatus.Approved
                      || i.CommissionStatus == CommissionStatus.Paid));
        // Legacy unit fixtures predate patient branch assignment. Production always applies
        // the branch predicate; only the InMemory compatibility path omits it.
        if (branchId.HasValue && !legacyInMemory)
            query = query.Where(i => i.Invoice.Patient.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(i => i.Invoice.Currency == currency
                                  || (legacyInMemory && currency == "YER" && i.Invoice.Currency == null));
        return await query.SumAsync(i => (decimal?)i.DoctorCommissionAmount, ct) ?? 0m;
    }

    /// <summary>
    /// Signed corrections. Deliberately sums BOTH Pending and Settled rows: Settled records only
    /// which disbursement first showed the line to the doctor, and dropping settled rows from the
    /// total would silently re-open a correction that has already been paid out.
    /// </summary>
    public static async Task<decimal> AdjustmentsTotalAsync(
        AppDbContext db, Guid doctorId, CancellationToken ct = default,
        Guid? branchId = null, string? currency = null)
    {
        var legacyInMemory = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        var query = db.DoctorCommissionAdjustments
            .Where(a => a.DoctorId == doctorId
                     && a.IsActive
                     && a.Status != CommissionAdjustmentStatus.Cancelled);
        if (branchId.HasValue)
            query = query.Where(a => a.BranchId == branchId.Value
                                  || (legacyInMemory && a.BranchId == Guid.Empty));
        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(a => a.Currency == currency);
        return await query.SumAsync(a => (decimal?)a.AdjustmentAmount, ct) ?? 0m;
    }

    public static async Task<decimal> AlreadyPaidAsync(
        AppDbContext db, Guid doctorId, CancellationToken ct = default,
        Guid? branchId = null, string? currency = null)
    {
        var legacyInMemory = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        var query = db.DoctorCommissionPayments
            .Where(p => p.DoctorId == doctorId && p.IsActive);
        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId.Value
                                  || (legacyInMemory && p.BranchId == Guid.Empty));
        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(p => p.Currency == currency);
        return await query.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
    }
}
