using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// CORE-XMOD-001 — the one derivation of "what does the clinic owe this supplier".
/// <para>
/// <c>Supplier.Balance</c> cannot answer that question. It is a single scalar with no currency,
/// and the clinic deals with labs and vendors that invoice in YER, SAR and USD. Every writer
/// that understands this guards itself with <c>if (currency == "YER")</c> and leaves foreign
/// bills out — which keeps the column from becoming nonsense but means a supplier who only ever
/// invoices in riyals shows a balance of zero no matter how much is owed.
/// </para>
/// <para>
/// The bills carry the currency and are the source of truth, so the balance is derived from
/// them. The legacy column stays for the modules that still read it, but nothing new should:
/// a scalar cannot represent three currencies, and rounding them into one is exactly the
/// silent error this exists to prevent.
/// </para>
/// </summary>
public sealed class SupplierBalanceReader(AppDbContext db)
{
    public sealed record SupplierCurrencyBalance(
        Guid SupplierId,
        string Currency,
        decimal TotalBilled,
        decimal TotalPaid,
        decimal Balance,
        int OpenBills);

    /// <summary>
    /// Per-currency balances for the given suppliers. Suppliers with no bills simply return no
    /// rows — an absent balance and a zero balance are the same thing here, and inventing a
    /// "0 YER" row for a supplier who invoices in dollars would be the very confusion this
    /// class exists to remove.
    /// </summary>
    /// <param name="branchId">When set, only that branch's bills count toward the balance.</param>
    public async Task<List<SupplierCurrencyBalance>> GetAsync(
        IReadOnlyCollection<Guid> supplierIds,
        Guid? branchId = null,
        CancellationToken ct = default)
    {
        if (supplierIds.Count == 0) return [];

        var ids = supplierIds.ToList();
        var query = db.SupplierBills.Where(b => ids.Contains(b.SupplierId) && b.IsActive);

        if (branchId.HasValue)
            query = query.Where(b => b.BranchId == branchId.Value);

        var rows = await query
            .GroupBy(b => new { b.SupplierId, b.Currency })
            .Select(g => new
            {
                g.Key.SupplierId,
                g.Key.Currency,
                TotalBilled = g.Sum(b => b.TotalAmount),
                TotalPaid = g.Sum(b => b.PaidAmount),
                OpenBills = g.Count(b => b.Status != BillStatus.FullyPaid
                                      && b.Status != BillStatus.Cancelled),
            })
            .ToListAsync(ct);

        // A bill written before the currency column existed reads as empty, and the entity
        // default is YER — so an empty currency is folded into YER rather than becoming a
        // separate nameless bucket.
        return rows
            .GroupBy(r => new
            {
                r.SupplierId,
                Currency = string.IsNullOrWhiteSpace(r.Currency) ? "YER" : r.Currency.Trim().ToUpperInvariant(),
            })
            .Select(g => new SupplierCurrencyBalance(
                SupplierId:  g.Key.SupplierId,
                Currency:    g.Key.Currency,
                TotalBilled: g.Sum(x => x.TotalBilled),
                TotalPaid:   g.Sum(x => x.TotalPaid),
                Balance:     g.Sum(x => x.TotalBilled) - g.Sum(x => x.TotalPaid),
                OpenBills:   g.Sum(x => x.OpenBills)))
            .OrderBy(b => b.SupplierId).ThenBy(b => b.Currency)
            .ToList();
    }

    /// <summary>Convenience overload for a single supplier.</summary>
    public async Task<List<SupplierCurrencyBalance>> GetForSupplierAsync(
        Guid supplierId, Guid? branchId = null, CancellationToken ct = default)
        => await GetAsync([supplierId], branchId, ct);
}
