using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// CORE-LAB-001: keeps a lab order's financial trail (SupplierBill + LabPayable +
/// journal entry) in step with the order itself.
/// <para>
/// This linkage used to exist ONLY inline inside <c>LabOrdersController.Create</c>,
/// guarded by "has a lab AND cost &gt; 0". A draft saved without a lab or without a
/// cost — which the create modal allows — therefore produced no bill, no payable and
/// no journal entry, and <c>Update</c> had no equivalent code. Attaching the lab or
/// the cost afterwards left the order financially invisible for good: the supplier
/// was never credited, the expense never reached the books, and the lab cost was
/// never deducted before the doctor's earned commission.
/// </para>
/// <para>
/// Every operation here is idempotent and keyed on the single SupplierBill carrying
/// this <c>LabOrderId</c>. Calling it repeatedly must converge on one bill, one
/// payable and a correct ledger — never a duplicate.
/// </para>
/// </summary>
public sealed class LabOrderFinanceSyncService(AppDbContext db, IJournalEntryService? journalEntryService)
{
    public sealed record SyncResult(bool Ok, string? Error, bool Created, bool Updated)
    {
        public static SyncResult Failed(string error) => new(false, error, false, false);
        public static readonly SyncResult NoOp = new(true, null, false, false);
    }

    /// <summary>
    /// Creates or updates the supplier bill, payable and journal entry for a
    /// financially recognised <paramref name="order"/>. The caller deliberately
    /// skips drafts and invokes this at the transition to sent and on later edits.
    /// </summary>
    /// <remarks>
    /// Does NOT call SaveChanges — the caller owns the transaction, exactly as the
    /// create path does, so the order and its financial trail commit atomically.
    /// </remarks>
    public async Task<SyncResult> SyncAsync(
        LabOrder order,
        Guid branchId,
        Guid performedBy,
        CancellationToken ct = default)
    {
        var amount = order.TotalCost ?? order.Cost ?? 0m;

        // Nothing to post when the recognised order is still incomplete.
        if (!order.LabId.HasValue || amount <= 0m)
            return SyncResult.NoOp;

        var lab = await db.Labs.FirstOrDefaultAsync(l => l.Id == order.LabId.Value && l.IsActive, ct);
        if (lab is null)
            return SyncResult.Failed("المعمل المحدد غير موجود أو غير مفعل");

        var currency = string.IsNullOrWhiteSpace(order.Currency) ? "YER" : order.Currency.Trim().ToUpperInvariant();
        var rate = currency == "YER" ? 1m : order.ExchangeRateToYer;
        if (rate <= 0m)
            return SyncResult.Failed("سعر الصرف الفعلي إلى الريال اليمني مطلوب لتكلفة المعمل.");

        // CORE-LAB-004: refuse rather than write an unusable row. SupplierBill.BranchId is
        // non-nullable, so an unresolved branch would silently persist Guid.Empty — a bill
        // belonging to no branch, invisible to every branch-scoped finance report. The
        // create path already rejects this; the update path must not be laxer.
        if (branchId == Guid.Empty)
            return SyncResult.Failed("لا يمكن تسجيل تكلفة المعمل لطلب بلا فرع محدد.");

        // CORE-LAB-005: serialise the whole check-then-create for THIS order. Without it two
        // concurrent updates can both observe "no bill yet" and both insert one — the
        // BillNumber lock below only serialises numbering, which happens after the check, so
        // it cannot prevent a duplicate bill for the same LabOrderId.
        await AcquireOrderLockAsync(order.Id, ct);

        var existingBill = await db.SupplierBills
            .FirstOrDefaultAsync(b => b.LabOrderId == order.Id, ct);

        return existingBill is null
            ? await CreateTrailAsync(order, lab, amount, currency, rate, branchId, performedBy, ct)
            : await UpdateTrailAsync(order, lab, existingBill, amount, currency, rate, performedBy, ct);
    }

    /// <summary>
    /// Cancels an unpaid financial trail when the owning lab order is cancelled or deleted.
    /// Paid trails must be handled through the supplier refund/credit workflow instead.
    /// </summary>
    public async Task<SyncResult> CancelAsync(
        LabOrder order,
        Guid performedBy,
        CancellationToken ct = default)
    {
        await AcquireOrderLockAsync(order.Id, ct);

        var bill = await db.SupplierBills
            .FirstOrDefaultAsync(b => b.LabOrderId == order.Id, ct);
        if (bill is null)
            return SyncResult.NoOp;

        var payable = await db.LabPayables
            .FirstOrDefaultAsync(p => p.LabOrderId == order.Id, ct);
        var alreadyPaid = bill.PaidAmount > 0m
            || bill.Status is BillStatus.PartiallyPaid or BillStatus.FullyPaid
            || (payable?.PaidAmount ?? 0m) > 0m;
        if (alreadyPaid)
            return SyncResult.Failed("لا يمكن إلغاء طلب معمل له دفعات مسجلة. عالج الدفعة من وحدة الموردين أولاً.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == bill.SupplierId, ct);
        if (supplier is not null
            && string.Equals(bill.Currency, "YER", StringComparison.OrdinalIgnoreCase))
        {
            supplier.Balance -= bill.TotalAmount;
        }

        await ReverseExistingEntryAsync(
            bill.Id,
            performedBy,
            $"إلغاء طلب معمل {order.OrderNumber}",
            ct);

        var deletedAt = DateTime.UtcNow;
        bill.Status = BillStatus.Cancelled;
        bill.IsActive = false;
        bill.DeletedAt = deletedAt;
        bill.DeletedBy = performedBy == Guid.Empty ? null : performedBy;
        bill.UpdatedAt = deletedAt;

        if (payable is not null)
        {
            payable.Status = "cancelled";
            payable.IsActive = false;
            payable.DeletedAt = deletedAt;
            payable.DeletedBy = performedBy == Guid.Empty ? null : performedBy;
            payable.UpdatedAt = deletedAt;
        }

        return new SyncResult(true, null, Created: false, Updated: true);
    }

    // ── First time this order becomes financially real ────────────────────────
    private async Task<SyncResult> CreateTrailAsync(
        LabOrder order, Lab lab, decimal amount, string currency, decimal rate,
        Guid branchId, Guid performedBy, CancellationToken ct)
    {
        var supplier = await ResolveSupplierAsync(lab, ct);

        var billDate = order.SentDate ?? ClinicTimeProvider.ClinicToday();
        var billNumber = await GenerateBillNumberAsync(billDate, ct);

        var bill = new SupplierBill
        {
            BillNumber = billNumber,
            SupplierId = supplier.Id,
            Description = $"طلب معمل {order.OrderNumber} - {order.ApplianceType}",
            TotalAmount = amount,
            Currency = currency,
            ExchangeRateToYer = rate,
            ExchangeRateSource = currency == "YER" ? "same_currency" : "manual",
            Status = BillStatus.Unpaid,
            BillDate = billDate,
            DueDate = order.ExpectedDate,
            LabOrderId = order.Id,
            BranchId = branchId,
            CreatedBy = performedBy,
        };
        db.SupplierBills.Add(bill);

        // Supplier.Balance is a single YER-denominated column, so only same-currency
        // bills may move it. Foreign-currency bills stay tracked on the bill itself
        // with their own rate — mixing them into one scalar would be meaningless.
        if (currency == "YER") supplier.Balance += amount;

        db.LabPayables.Add(new LabPayable
        {
            LabOrderId = order.Id,
            LabId = order.LabId!.Value,
            SupplierBillId = bill.Id,
            Amount = amount,
            PaidAmount = 0,
            Status = "pending",
            DueDate = order.ExpectedDate?.ToDateTime(TimeOnly.MinValue),
        });

        await PostEntryAsync(order, lab, bill, supplier, amount, currency, rate, billDate, branchId, performedBy,
            $"استحقاق طلب معمل {order.OrderNumber} - {lab.Name}", ct);

        return new SyncResult(true, null, Created: true, Updated: false);
    }

    // ── The order already has a trail; bring it in line ───────────────────────
    private async Task<SyncResult> UpdateTrailAsync(
        LabOrder order, Lab lab, SupplierBill bill, decimal amount, string currency, decimal rate,
        Guid performedBy, CancellationToken ct)
    {
        var payable = await db.LabPayables.FirstOrDefaultAsync(p => p.LabOrderId == order.Id, ct);

        // Resolve the target supplier before the paid guard so a lab change is detected
        // even if a damaged historical trail is missing its LabPayable row.
        var targetSupplier = await ResolveSupplierAsync(lab, ct);
        var supplierChanged = targetSupplier.Id != bill.SupplierId;

        // Refuse rather than silently corrupt: once money has moved against this bill,
        // rewriting any financial identity would desynchronise the supplier payment,
        // supplier account and ledger.
        var alreadyPaid = bill.Status != BillStatus.Unpaid || (payable?.PaidAmount ?? 0m) > 0m;
        var amountChanged = bill.TotalAmount != amount;
        var currencyChanged = !string.Equals(bill.Currency, currency, StringComparison.OrdinalIgnoreCase);
        var rateChanged = bill.ExchangeRateToYer != rate;
        var labChanged = supplierChanged || (payable is not null && payable.LabId != order.LabId);
        if (alreadyPaid && (amountChanged || currencyChanged || rateChanged || labChanged))
            return SyncResult.Failed("لا يمكن تعديل المعمل أو التكلفة أو العملة أو سعر الصرف لطلب له دفعات مسجلة. عالج الدفعة من وحدة الموردين أولاً.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == bill.SupplierId, ct);

        var dueChanged = bill.DueDate != order.ExpectedDate;

        if (!amountChanged && !currencyChanged && !rateChanged && !dueChanged && !supplierChanged)
            return SyncResult.NoOp;

        // Unwind this bill's contribution to the old supplier's YER balance, then
        // re-apply it to whichever supplier it now belongs to.
        if (supplier is not null && string.Equals(bill.Currency, "YER", StringComparison.OrdinalIgnoreCase))
            supplier.Balance -= bill.TotalAmount;
        if (currency == "YER") targetSupplier.Balance += amount;

        bill.SupplierId = targetSupplier.Id;
        bill.Description = $"طلب معمل {order.OrderNumber} - {order.ApplianceType}";
        bill.TotalAmount = amount;
        bill.Currency = currency;
        bill.ExchangeRateToYer = rate;
        bill.ExchangeRateSource = currency == "YER" ? "same_currency" : "manual";
        bill.DueDate = order.ExpectedDate;

        if (payable is not null)
        {
            payable.LabId = order.LabId!.Value;
            payable.Amount = amount;
            payable.DueDate = order.ExpectedDate?.ToDateTime(TimeOnly.MinValue);
            payable.SupplierBillId = bill.Id;
        }
        else
        {
            // Trail was partially built (bill without payable) — complete it rather
            // than leaving the order half-linked.
            db.LabPayables.Add(new LabPayable
            {
                LabOrderId = order.Id,
                LabId = order.LabId!.Value,
                SupplierBillId = bill.Id,
                Amount = amount,
                PaidAmount = 0,
                Status = "pending",
                DueDate = order.ExpectedDate?.ToDateTime(TimeOnly.MinValue),
            });
        }

        // A posted entry is never edited in place. Reverse it and post the corrected
        // one, so the ledger keeps an auditable trail of the change.
        await ReverseExistingEntryAsync(bill.Id, performedBy,
            $"تعديل تكلفة طلب معمل {order.OrderNumber}", ct);

        await PostEntryAsync(order, lab, bill, targetSupplier, amount, currency, rate,
            bill.BillDate, bill.BranchId, performedBy,
            $"تعديل استحقاق طلب معمل {order.OrderNumber} - {lab.Name}", ct);

        return new SyncResult(true, null, Created: false, Updated: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the supplier that represents this lab, creating one if needed. Mirrors
    /// the resolution order the create path already used: explicit SupplierId, then
    /// an active dental-lab supplier of the same name, then auto-create.
    /// </summary>
    private async Task<Supplier> ResolveSupplierAsync(Lab lab, CancellationToken ct)
    {
        var supplier = lab.SupplierId.HasValue
            ? await db.Suppliers.FirstOrDefaultAsync(s => s.Id == lab.SupplierId.Value && s.IsActive, ct)
            : null;

        supplier ??= await db.Suppliers.FirstOrDefaultAsync(
            s => s.IsActive && s.Type == SupplierType.DentalLab && s.Name == lab.Name, ct);

        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = lab.Name,
                Type = SupplierType.DentalLab,
                ContactPerson = lab.ContactPerson,
                Phone = lab.Phone,
                Email = lab.Email,
                Address = lab.Address,
                Notes = "تم إنشاؤه تلقائياً من وحدة المعامل",
            };
            db.Suppliers.Add(supplier);
        }

        lab.SupplierId = supplier.Id;
        return supplier;
    }

    /// <summary>
    /// CORE-LAB-005: transaction-scoped advisory lock keyed on the lab order, so only one
    /// request at a time may decide whether that order still needs a bill. No-op on
    /// non-relational providers (InMemory tests), which are single-threaded per test.
    /// </summary>
    private async Task AcquireOrderLockAsync(Guid orderId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()
            || db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
            return;

        // StableGuidToLong, never Guid.GetHashCode(): the hash code is not stable across
        // processes, and an advisory lock that differs between replicas serialises nothing.
        // StableLockKeyHelper documents exactly this trap.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0})",
            StableLockKeyHelper.StableGuidToLong(orderId));
    }

    private async Task<string> GenerateBillNumberAsync(DateOnly billDate, CancellationToken ct)
    {
        var prefix = $"BILL-{billDate:yyyyMMdd}-";
        if (db.Database.IsRelational()
            && db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0})", StableLockKeyHelper.BillNumber);
        }

        var last = await db.SupplierBills.IgnoreQueryFilters()
            .Where(b => b.BillNumber.StartsWith(prefix))
            .OrderByDescending(b => b.BillNumber)
            .Select(b => b.BillNumber)
            .FirstOrDefaultAsync(ct);

        var sequence = 1;
        if (!string.IsNullOrWhiteSpace(last) && int.TryParse(last[prefix.Length..], out var previous))
            sequence = previous + 1;

        return $"{prefix}{sequence:D3}";
    }

    private async Task PostEntryAsync(
        LabOrder order, Lab lab, SupplierBill bill, Supplier supplier,
        decimal amount, string currency, decimal rate, DateOnly entryDate,
        Guid branchId, Guid performedBy, string description, CancellationToken ct)
    {
        if (journalEntryService is null || performedBy == Guid.Empty)
            return;

        var entry = await journalEntryService.CreateEntryAsync(
            FinancialDocumentType.SupplierBill,
            bill.Id,
            description,
            entryDate,
            branchId,
            performedBy,
            cashierSessionId: null,
            treasuryId: null,
            lines:
            [
                (JournalAccountType.Expense, bill.Id, amount, 0m, $"تكلفة طلب المعمل {order.OrderNumber}"),
                (JournalAccountType.AccountsPayable, supplier.Id, 0m, amount, $"مستحق للمعمل {lab.Name}")
            ],
            autoSave: false,
            ct: ct);

        entry.Currency = currency;
        entry.ExchangeRateToYer = rate;
        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;
    }

    private async Task ReverseExistingEntryAsync(Guid billId, Guid performedBy, string reason, CancellationToken ct)
    {
        if (journalEntryService is null || performedBy == Guid.Empty)
            return;

        var existing = await db.JournalEntries
            .Where(e => e.FinancialDocumentType == FinancialDocumentType.SupplierBill
                     && e.FinancialDocumentId == billId
                     && !e.IsReversal
                     && e.ReversedByEntryId == null)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is null) return;

        await journalEntryService.CreateReversalEntryAsync(existing.Id, reason, performedBy, ct);
    }
}
