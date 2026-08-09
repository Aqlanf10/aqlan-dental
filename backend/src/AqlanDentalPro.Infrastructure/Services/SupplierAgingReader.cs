using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// CORE-LAB-020 — how old the clinic's unpaid supplier bills are, per supplier and per currency.
/// <para>
/// The owner's third standing problem is <i>تراكم التراكيب</i>: lab work piling up and, with it,
/// what the clinic owes. A running balance answers "how much" but never "how long", and those are
/// different questions — 400,000 YER owed inside terms is routine, the same 400,000 ninety days
/// past due is a supplier about to stop taking orders. This buckets the outstanding amount by how
/// far past its due date each bill is, which is the shape every accounts-payable ageing report
/// takes and the one an accountant can act on.
/// </para>
/// <para>
/// Written once and read by two screens on purpose, exactly like <see cref="SupplierBalanceReader"/>:
/// the suppliers screen shows every vendor, the lab workspace shows only dental labs, and if each
/// computed its own ageing they would eventually disagree about the same supplier.
/// </para>
/// </summary>
public sealed class SupplierAgingReader(AppDbContext db)
{
    /// <summary>
    /// One ageing row. Currency is part of the identity, never summed across:
    /// a bucket holding 300,000 YER and 100,000 SAR is not 400,000 of anything.
    /// </summary>
    public sealed record SupplierAgingRow(
        Guid SupplierId,
        string SupplierName,
        SupplierType SupplierType,
        string Currency,
        /// <summary>Outstanding on bills not yet due, plus bills due exactly today.</summary>
        decimal NotYetDue,
        decimal Bucket1,
        decimal Bucket2,
        decimal Bucket3,
        decimal Bucket4Plus,
        /// <summary>
        /// Outstanding on bills that carry no due date at all. Kept apart rather than folded
        /// into "not yet due": a bill with no agreed date is not evidence that it is not
        /// overdue, and quietly treating it as current is how a real debt disappears from an
        /// ageing report.
        /// </summary>
        decimal NoDueDate,
        decimal Total,
        int OpenBills,
        /// <summary>Days past due of the oldest overdue bill; null when nothing is overdue.</summary>
        int? OldestOverdueDays);

    /// <summary>
    /// Bucket boundaries in days, read from Settings. Held as a record so a caller can echo the
    /// boundaries back to the UI — a report whose column headers disagree with the numbers under
    /// them is worse than no report.
    /// </summary>
    public sealed record AgingBuckets(int Bucket1Days, int Bucket2Days, int Bucket3Days);

    /// <summary>
    /// Ageing as of <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">
    /// The date the report is drawn to — always a clinic-local date (Asia/Aden). Passed in rather
    /// than computed here so the caller and the report header cannot disagree about "today".
    /// </param>
    /// <param name="supplierType">When set, restricts to one supplier type (the lab slice uses this).</param>
    /// <param name="branchId">When set, only that branch's bills count.</param>
    public async Task<List<SupplierAgingRow>> GetAsync(
        DateOnly asOf,
        AgingBuckets buckets,
        SupplierType? supplierType = null,
        Guid? branchId = null,
        CancellationToken ct = default)
    {
        // RemainingAmount is a computed C# property and cannot be translated, so the
        // subtraction is written out for the database to evaluate.
        var query = db.SupplierBills
            .Where(b => b.IsActive
                     && b.Status != BillStatus.Cancelled
                     && b.TotalAmount - b.PaidAmount > 0m);

        if (branchId.HasValue)
            query = query.Where(b => b.BranchId == branchId.Value);

        if (supplierType.HasValue)
            query = query.Where(b => b.Supplier!.Type == supplierType.Value);

        var bills = await query
            .Select(b => new
            {
                b.SupplierId,
                SupplierName = b.Supplier!.Name,
                SupplierType = b.Supplier.Type,
                b.Currency,
                b.DueDate,
                Outstanding = b.TotalAmount - b.PaidAmount,
            })
            .ToListAsync(ct);

        return bills
            .GroupBy(b => new { b.SupplierId, b.SupplierName, b.SupplierType, b.Currency })
            .Select(g =>
            {
                decimal notYetDue = 0m, b1 = 0m, b2 = 0m, b3 = 0m, b4 = 0m, noDue = 0m;
                int? oldest = null;

                foreach (var bill in g)
                {
                    if (bill.DueDate is null)
                    {
                        noDue += bill.Outstanding;
                        continue;
                    }

                    var daysPastDue = asOf.DayNumber - bill.DueDate.Value.DayNumber;

                    if (daysPastDue <= 0)
                    {
                        notYetDue += bill.Outstanding;
                        continue;
                    }

                    if (oldest is null || daysPastDue > oldest) oldest = daysPastDue;

                    if (daysPastDue <= buckets.Bucket1Days) b1 += bill.Outstanding;
                    else if (daysPastDue <= buckets.Bucket2Days) b2 += bill.Outstanding;
                    else if (daysPastDue <= buckets.Bucket3Days) b3 += bill.Outstanding;
                    else b4 += bill.Outstanding;
                }

                return new SupplierAgingRow(
                    g.Key.SupplierId,
                    g.Key.SupplierName,
                    g.Key.SupplierType,
                    g.Key.Currency,
                    notYetDue, b1, b2, b3, b4, noDue,
                    Total: notYetDue + b1 + b2 + b3 + b4 + noDue,
                    OpenBills: g.Count(),
                    OldestOverdueDays: oldest);
            })
            // Worst first: the supplier with the most money furthest past due is the one the
            // owner needs to see without scrolling.
            .OrderByDescending(r => r.Bucket4Plus + r.Bucket3)
            .ThenByDescending(r => r.Total)
            .ThenBy(r => r.SupplierName, StringComparer.Ordinal)
            .ToList();
    }
}
