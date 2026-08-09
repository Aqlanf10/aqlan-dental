using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "StaffOnly")]
public class LabReportsController(
    AppDbContext db,
    ICurrentUserService currentUser,
    // CORE-XMOD-001 — the same derivation the suppliers screen uses, so the two
    // cannot report a different balance for the same lab.
    SupplierBalanceReader balanceReader,
    // CORE-LAB-020 — same reader the suppliers screen uses, so the two cannot
    // report a different age for the same unpaid bill.
    SupplierAgingReader agingReader) : ControllerBase
{
    /// <summary>
    /// CORE-LAB-010: money in this module is never a single scalar. A lab order carries its own
    /// currency (YER / SAR / USD), so every total here is a LIST of per-currency amounts. Summing
    /// them into one number would produce a figure that is not money in any currency — the owner
    /// would read "إجمالي التكاليف ٤٠٠٬٠٠٠" for 300,000 YER plus 100,000 SAR, which is wrong by
    /// roughly sixty-eight times the SAR part.
    /// </summary>
    private sealed record LabCurrencyAmount(string Currency, decimal Amount);

    /// <summary>Drops zero rows so an untouched currency does not clutter every total.</summary>
    private static List<LabCurrencyAmount> Fold(IEnumerable<LabCurrencyAmount> amounts)
        => amounts
            .GroupBy(a => a.Currency)
            .Select(g => new LabCurrencyAmount(g.Key, g.Sum(a => a.Amount)))
            .Where(a => a.Amount != 0m)
            .OrderByDescending(a => a.Amount)
            .ToList();

    private Task<bool> CanViewReportsAsync() => PermissionGuard.HasAsync(db, currentUser, "lab_reports", "view");

    /// <summary>
    /// CORE-LAB-015: the thresholds that decide whether a lab is performing acceptably are a
    /// contract term the owner renegotiates per lab, not a display constant. They were literals
    /// in the reports JSX — each written twice, on the summary card and on the row badge — so
    /// editing one made the two disagree about the same lab. Served from Settings instead, once.
    /// </summary>
    private async Task<object> ReadThresholdsAsync(CancellationToken ct)
    {
        var settings = new FinanceSettingsReader(db);
        return new
        {
            RemakeRateAlarm      = await settings.GetDecimalAsync(FinanceSettingsKeys.LabRemakeRateAlarm, ct),
            RemakeRateWarn       = await settings.GetDecimalAsync(FinanceSettingsKeys.LabRemakeRateWarn, ct),
            OverdueRateAlarm     = await settings.GetDecimalAsync(FinanceSettingsKeys.LabOverdueRateAlarm, ct),
            OverdueRateWarn      = await settings.GetDecimalAsync(FinanceSettingsKeys.LabOverdueRateWarn, ct),
            TurnaroundDaysTarget = await settings.GetDecimalAsync(FinanceSettingsKeys.LabTurnaroundDaysTarget, ct),
            OnTimeRateGood       = await settings.GetDecimalAsync(FinanceSettingsKeys.LabOnTimeRateGood, ct),
            OnTimeRateWarn       = await settings.GetDecimalAsync(FinanceSettingsKeys.LabOnTimeRateWarn, ct),
        };
    }

    /// <summary>
    /// The clinic's running account with each lab, PER CURRENCY.
    /// <para>
    /// Read from <c>SupplierBill</c> rather than <c>Supplier.Balance</c> deliberately.
    /// <c>Supplier.Balance</c> is a single scalar with no currency, and the lab finance sync
    /// only moves it for YER bills — so a lab that invoices the clinic in SAR or USD shows a
    /// running balance of zero no matter how much is owed. The bills carry the currency and
    /// are the source of truth, so the account is derived from them and is correct for all
    /// three currencies. Fixing the scalar itself is a cross-module change to the suppliers
    /// entity and is recorded as a separate finding.
    /// </para>
    /// </summary>
    private async Task<List<object>> GetLabAccountsAsync(CancellationToken ct)
    {
        var labSupplierIds = await db.Suppliers
            .Where(x => x.Type == SupplierType.DentalLab)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        if (labSupplierIds.Count == 0) return [];

        var balances = await balanceReader.GetAsync(
            labSupplierIds.Select(x => x.Id).ToList(), branchId: null, ct: ct);

        return labSupplierIds
            .Select(sup => new
            {
                sup.Id,
                sup.Name,
                Rows = balances.Where(b => b.SupplierId == sup.Id).ToList(),
            })
            .Where(x => x.Rows.Count > 0)
            .Select(x => (object)new
            {
                SupplierId = x.Id,
                LabName = x.Name,
                OpenBills = x.Rows.Sum(r => r.OpenBills),
                BilledByCurrency  = Fold(x.Rows.Select(r => new LabCurrencyAmount(r.Currency, r.TotalBilled))),
                PaidByCurrency    = Fold(x.Rows.Select(r => new LabCurrencyAmount(r.Currency, r.TotalPaid))),
                BalanceByCurrency = Fold(x.Rows.Select(r => new LabCurrencyAmount(r.Currency, r.Balance))),
            })
            .ToList();
    }

    /// <summary>
    /// The clinic's account with every lab, per currency — the answer to "how much do we owe
    /// this lab" when one lab invoices in Saudi riyals and another in dollars.
    /// </summary>
    [HttpGet("lab-accounts")]
    public async Task<IActionResult> GetLabAccounts(CancellationToken ct = default)
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var accounts = await GetLabAccountsAsync(ct);
        return Ok(new { data = accounts });
    }

    /// <summary>
    /// CORE-LAB-020 — how old the clinic's unpaid lab bills are, per lab and per currency.
    /// <para>
    /// The payables screen already answers "how much do we owe this lab". It cannot answer "for
    /// how long", and that is the question behind <i>تراكم التراكيب</i>: an amount inside its
    /// credit terms is routine, the same amount ninety days past due is a lab that is about to
    /// refuse the next case. Bucket boundaries come from Settings and are echoed back, so the
    /// column headers can never describe different periods from the numbers under them.
    /// </para>
    /// <para>
    /// Hard-scoped to <see cref="SupplierType.DentalLab"/>. The same reader serves the suppliers
    /// screen for every vendor, but that screen is AdminOnly — a lab-reports viewer must not
    /// learn what the clinic owes its landlord through this route.
    /// </para>
    /// </summary>
    [HttpGet("lab-aging")]
    public async Task<IActionResult> GetLabAging([FromQuery] Guid? branchId, CancellationToken ct = default)
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var settings = new FinanceSettingsReader(db);
        var buckets = new SupplierAgingReader.AgingBuckets(
            await settings.GetIntAsync(FinanceSettingsKeys.PayablesAgingBucket1Days, ct),
            await settings.GetIntAsync(FinanceSettingsKeys.PayablesAgingBucket2Days, ct),
            await settings.GetIntAsync(FinanceSettingsKeys.PayablesAgingBucket3Days, ct));

        var asOf = ClinicTimeProvider.ClinicToday();
        var rows = await agingReader.GetAsync(asOf, buckets, SupplierType.DentalLab, branchId, ct);

        return Ok(new
        {
            asOf,
            buckets = new { buckets.Bucket1Days, buckets.Bucket2Days, buckets.Bucket3Days },
            data = rows,
            // Per-currency column totals. Folded the same way as every other total in this
            // controller, which is to say never across currencies.
            totals = rows
                .GroupBy(r => r.Currency)
                .Select(g => new
                {
                    currency = g.Key,
                    notYetDue = g.Sum(r => r.NotYetDue),
                    bucket1 = g.Sum(r => r.Bucket1),
                    bucket2 = g.Sum(r => r.Bucket2),
                    bucket3 = g.Sum(r => r.Bucket3),
                    bucket4Plus = g.Sum(r => r.Bucket4Plus),
                    noDueDate = g.Sum(r => r.NoDueDate),
                    total = g.Sum(r => r.Total),
                })
                .OrderByDescending(t => t.total)
                .ToList(),
        });
    }

    /// <summary>Lab costs report — total costs per lab, per currency.</summary>
    /// <remarks>
    /// CORE-LAB-011: costs count only COMMITTED orders. A draft was never sent, and a cancelled
    /// order is not owed, so including either inflates what the clinic believes it spent — the
    /// same boundary the supplier bill and the commission deduction already draw. Order COUNTS
    /// still describe everything, which is why they are reported separately from the money.
    /// </remarks>
    [HttpGet("lab-costs")]
    public async Task<IActionResult> GetLabCosts([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var query = db.LabOrders
            .Include(o => o.Lab)
            .Where(o => o.LabId != null)
            .AsQueryable();

        if (from.HasValue) query = query.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(o => o.CreatedAt <= to.Value);

        var grouped = await query
            .GroupBy(o => new
            {
                o.LabId,
                LabName = o.Lab != null ? o.Lab.Name : "غير محدد",
                o.Currency,
            })
            .Select(g => new
            {
                g.Key.LabId,
                g.Key.LabName,
                g.Key.Currency,
                TotalOrders = g.Count(),
                CommittedCost = g.Sum(o =>
                    !LabCostForCommission.UncommittedStatuses.Contains(o.Status)
                        ? (o.TotalCost ?? o.Cost ?? 0)
                        : 0),
                PendingOrders = g.Count(o => o.Status == "sent" || o.Status == "manufacturing"),
                ReturnedOrders = g.Count(o => o.Status == "returned"),
                RemakeOrders = g.Count(o => o.Status == "remake"),
            })
            .ToListAsync();

        var report = grouped
            .GroupBy(g => new { g.LabId, g.LabName })
            .Select(lab => new
            {
                lab.Key.LabId,
                lab.Key.LabName,
                TotalOrders = lab.Sum(x => x.TotalOrders),
                TotalCostByCurrency = Fold(lab.Select(x => new LabCurrencyAmount(x.Currency, x.CommittedCost))),
                PendingOrders = lab.Sum(x => x.PendingOrders),
                ReturnedOrders = lab.Sum(x => x.ReturnedOrders),
                RemakeOrders = lab.Sum(x => x.RemakeOrders),
            })
            .OrderByDescending(r => r.TotalOrders)
            .ToList();

        var totals = Fold(grouped.Select(g => new LabCurrencyAmount(g.Currency, g.CommittedCost)));

        return Ok(new { data = report, totalsByCurrency = totals });
    }

    /// <summary>Lab debts report — unpaid/partial payables, per currency.</summary>
    [HttpGet("lab-debts")]
    public async Task<IActionResult> GetLabDebts([FromQuery] Guid? labId)
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var query = db.LabPayables
            .Include(p => p.Lab)
            .Include(p => p.LabOrder)
            .Where(p => p.Status != "paid")
            .AsQueryable();

        if (labId.HasValue) query = query.Where(p => p.LabId == labId.Value);

        var debts = await query
            .Select(p => new
            {
                p.Id,
                p.LabId,
                LabName = p.Lab.Name,
                OrderNumber = p.LabOrder.OrderNumber,
                p.Amount,
                p.PaidAmount,
                Balance = p.Amount - p.PaidAmount,
                // CORE-LAB-012: LabPayable has no currency column of its own — the amount is
                // denominated by the supplier bill it was raised against. The frontend already
                // declared a currency field for these rows and was rendering it as undefined.
                Currency = p.SupplierBill != null ? p.SupplierBill.Currency : "YER",
                p.Status,
                DueDate = p.DueDate != null ? p.DueDate.Value.ToString("yyyy-MM-dd") : null,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .OrderByDescending(p => p.Balance)
            .ToListAsync();

        var summary = new
        {
            TotalDebtByCurrency = Fold(debts.Select(d => new LabCurrencyAmount(d.Currency, d.Balance))),
            TotalPayables = debts.Count,
            PendingCount = debts.Count(d => d.Status == "pending"),
            PartialCount = debts.Count(d => d.Status == "partial"),
        };

        return Ok(new { data = debts, summary });
    }

    /// <summary>
    /// Lab Sprint 6 — Lab performance KPIs.
    /// Returns per-lab metrics: avg execution days, remake %, overdue %, total orders.
    /// </summary>
    [HttpGet("lab-performance")]
    public async Task<IActionResult> GetLabPerformance(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var query = db.LabOrders
            .Include(o => o.Lab)
            .Where(o => o.LabId != null)
            .AsQueryable();

        if (from.HasValue) query = query.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(o => o.CreatedAt <= to.Value);

        var today = ClinicTimeProvider.ClinicToday();

        // Get raw data grouped by lab for in-memory KPI calculations
        var labGroups = await query
            .GroupBy(o => new { o.LabId, LabName = o.Lab != null ? o.Lab.Name : "غير محدد" })
            .Select(g => new
            {
                LabId = g.Key.LabId,
                LabName = g.Key.LabName,
                TotalOrders = g.Count(),
                DeliveredOrders = g.Count(o => o.Status == "delivered"),
                RemakeOrders = g.Count(o => o.Status == "remake" || o.RemakeCount > 0),
                OverdueOrders = g.Count(o =>
                    o.ExpectedDate != null &&
                    o.ExpectedDate < today &&
                    o.Status != "delivered" &&
                    o.Status != "cancelled"),
                CancelledOrders = g.Count(o => o.Status == "cancelled"),
                // For avg execution days: count only orders with both SentDate and ReceivedDate
                OrdersWithExecutionDays = g
                    .Where(o => o.SentDate != null && o.ReceivedDate != null)
                    .Select(o => (int?)(o.ReceivedDate!.Value.DayNumber - o.SentDate!.Value.DayNumber))
                    .ToList(),
            })
            .ToListAsync();

        // Money is grouped separately because it needs a currency dimension the count metrics
        // above must NOT have — splitting the count rows by currency would turn one lab into
        // several and quietly change every rate on this report.
        var costsByLab = (await query
            .GroupBy(o => new { o.LabId, o.Currency })
            .Select(g => new
            {
                g.Key.LabId,
                g.Key.Currency,
                CommittedCost = g.Sum(o =>
                    !LabCostForCommission.UncommittedStatuses.Contains(o.Status)
                        ? (o.TotalCost ?? o.Cost ?? 0)
                        : 0),
            })
            .ToListAsync())
            .GroupBy(x => x.LabId)
            .ToDictionary(
                x => x.Key,
                x => Fold(x.Select(c => new LabCurrencyAmount(c.Currency, c.CommittedCost))));

        var report = labGroups.Select(g =>
        {
            var avgDays = g.OrdersWithExecutionDays.Count > 0
                ? Math.Round(g.OrdersWithExecutionDays.Average() ?? 0, 1)
                : 0;
            var remakeRate = g.TotalOrders > 0
                ? Math.Round((double)g.RemakeOrders / g.TotalOrders * 100, 1)
                : 0;
            var overdueRate = g.TotalOrders > 0
                ? Math.Round((double)g.OverdueOrders / g.TotalOrders * 100, 1)
                : 0;
            var onTimeRate = g.DeliveredOrders > 0
                ? Math.Round((double)(g.DeliveredOrders - g.OverdueOrders) / g.DeliveredOrders * 100, 1)
                : 0;

            return new
            {
                g.LabId,
                g.LabName,
                g.TotalOrders,
                g.DeliveredOrders,
                g.RemakeOrders,
                g.OverdueOrders,
                g.CancelledOrders,
                TotalCostByCurrency = g.LabId != null && costsByLab.TryGetValue(g.LabId, out var costs)
                    ? costs
                    : [],
                AvgExecutionDays = avgDays,
                RemakeRate = remakeRate,
                OverdueRate = overdueRate,
                OnTimeRate = Math.Max(0, onTimeRate),
            };
        }).OrderByDescending(r => r.TotalOrders).ToList();

        // Overall summary
        var totalOrders = report.Sum(r => r.TotalOrders);
        var overallAvgDays = report.Count > 0
            ? Math.Round(report.Average(r => r.AvgExecutionDays), 1)
            : 0;
        var overallRemakeRate = totalOrders > 0
            ? Math.Round((double)report.Sum(r => r.RemakeOrders) / totalOrders * 100, 1)
            : 0;
        var overallOverdueRate = totalOrders > 0
            ? Math.Round((double)report.Sum(r => r.OverdueOrders) / totalOrders * 100, 1)
            : 0;

        var summary = new
        {
            TotalLabs = report.Count,
            TotalOrders = totalOrders,
            TotalDelivered = report.Sum(r => r.DeliveredOrders),
            TotalOverdue = report.Sum(r => r.OverdueOrders),
            TotalRemakes = report.Sum(r => r.RemakeOrders),
            TotalCostByCurrency = Fold(costsByLab.Values.SelectMany(v => v)),
            OverallAvgExecutionDays = overallAvgDays,
            OverallRemakeRate = overallRemakeRate,
            OverallOverdueRate = overallOverdueRate,
        };

        return Ok(new
        {
            data = report,
            summary,
            thresholds = await ReadThresholdsAsync(ct),
        });
    }

    /// <summary>
    /// Lab Sprint 6 — Lab dashboard summary.
    /// Returns overall KPIs and recent activity for the lab dashboard page.
    /// </summary>
    [HttpGet("lab-dashboard")]
    public async Task<IActionResult> GetLabDashboard(CancellationToken ct = default)
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var today = ClinicTimeProvider.ClinicToday();

        // Overall counts
        var totalOrders = await db.LabOrders.CountAsync();
        var pendingOrders = await db.LabOrders.CountAsync(o =>
            o.Status == "sent" || o.Status == "manufacturing" || o.Status == "remake");
        var readyOrders = await db.LabOrders.CountAsync(o => o.Status == "ready");
        var receivedOrders = await db.LabOrders.CountAsync(o => o.Status == "received");
        var overdueOrders = await db.LabOrders.CountAsync(o =>
            o.ExpectedDate != null &&
            o.ExpectedDate < today &&
            o.Status != "delivered" &&
            o.Status != "cancelled");
        // Named DeliveredLast30Days, not "this month": the window really is the last 30 clinic
        // days, which is also what the screen's own label says.
        var deliveredLast30Days = await db.LabOrders.CountAsync(o =>
            o.Status == "delivered" && o.DeliveredDate != null && o.DeliveredDate >= today.AddDays(-30));
        var returnedOrders = await db.LabOrders.CountAsync(o => o.Status == "returned");
        var remakeOrders = await db.LabOrders.CountAsync(o => o.Status == "remake");

        // Financial summary — per currency, committed orders only.
        var totalLabCosts = Fold((await db.LabOrders
            .Where(o => o.LabId != null && !LabCostForCommission.UncommittedStatuses.Contains(o.Status))
            .GroupBy(o => o.Currency)
            .Select(g => new { Currency = g.Key, Amount = g.Sum(o => o.TotalCost ?? o.Cost ?? 0) })
            .ToListAsync())
            .Select(x => new LabCurrencyAmount(x.Currency, x.Amount)));

        var totalDebt = Fold((await db.LabPayables
            .Where(p => p.Status != "paid")
            .GroupBy(p => p.SupplierBill != null ? p.SupplierBill.Currency : "YER")
            .Select(g => new { Currency = g.Key, Amount = g.Sum(p => p.Amount - p.PaidAmount) })
            .ToListAsync())
            .Select(x => new LabCurrencyAmount(x.Currency, x.Amount)));

        // Status distribution for chart
        var statusDistribution = await db.LabOrders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // Orders by lab (top 5 by order count)
        var labTotals = await db.LabOrders
            .Include(o => o.Lab)
            .Where(o => o.LabId != null)
            .GroupBy(o => new
            {
                o.LabId,
                LabName = o.Lab != null ? o.Lab.Name : "غير محدد",
                o.Currency,
            })
            .Select(g => new
            {
                g.Key.LabId,
                g.Key.LabName,
                g.Key.Currency,
                OrderCount = g.Count(),
                CommittedCost = g.Sum(o =>
                    !LabCostForCommission.UncommittedStatuses.Contains(o.Status)
                        ? (o.TotalCost ?? o.Cost ?? 0)
                        : 0),
            })
            .ToListAsync();

        var topLabs = labTotals
            .GroupBy(x => new { x.LabId, x.LabName })
            .Select(lab => new
            {
                lab.Key.LabId,
                lab.Key.LabName,
                OrderCount = lab.Sum(x => x.OrderCount),
                TotalCostByCurrency = Fold(lab.Select(x => new LabCurrencyAmount(x.Currency, x.CommittedCost))),
            })
            .OrderByDescending(l => l.OrderCount)
            .Take(5)
            .ToList();

        // Recent overdue orders
        var recentOverdue = await db.LabOrders
            .Include(o => o.Patient)
            .Include(o => o.Lab)
            .Where(o => o.ExpectedDate != null && o.ExpectedDate < today
                && o.Status != "delivered" && o.Status != "cancelled")
            .OrderBy(o => o.ExpectedDate)
            .Take(10)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                PatientName = o.Patient.FirstName + " " + o.Patient.LastName,
                LabName = o.Lab != null ? o.Lab.Name : o.LabName,
                o.ApplianceType,
                ExpectedDate = o.ExpectedDate != null ? o.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                DaysOverdue = o.ExpectedDate != null ? (int)(today.DayNumber - o.ExpectedDate.Value.DayNumber) : 0,
                o.Status,
                o.Priority,
            })
            .ToListAsync();

        // Monthly trend (last 6 months)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var monthlyRaw = await db.LabOrders
            .Where(o => o.CreatedAt >= sixMonthsAgo)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.Currency })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Currency,
                TotalOrders = g.Count(),
                DeliveredOrders = g.Count(o => o.Status == "delivered"),
                CommittedCost = g.Sum(o =>
                    !LabCostForCommission.UncommittedStatuses.Contains(o.Status)
                        ? (o.TotalCost ?? o.Cost ?? 0)
                        : 0),
            })
            .ToListAsync();

        var monthlyTrend = monthlyRaw
            .GroupBy(x => new { x.Year, x.Month })
            .Select(m => new
            {
                m.Key.Year,
                m.Key.Month,
                TotalOrders = m.Sum(x => x.TotalOrders),
                DeliveredOrders = m.Sum(x => x.DeliveredOrders),
                TotalCostByCurrency = Fold(m.Select(x => new LabCurrencyAmount(x.Currency, x.CommittedCost))),
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        var dashboard = new
        {
            KPIs = new
            {
                TotalOrders = totalOrders,
                PendingOrders = pendingOrders,
                ReadyOrders = readyOrders,
                ReceivedOrders = receivedOrders,
                OverdueOrders = overdueOrders,
                DeliveredLast30Days = deliveredLast30Days,
                ReturnedOrders = returnedOrders,
                RemakeOrders = remakeOrders,
                TotalLabCostsByCurrency = totalLabCosts,
                TotalDebtByCurrency = totalDebt,
            },
            StatusDistribution = statusDistribution,
            TopLabs = topLabs,
            RecentOverdue = recentOverdue,
            MonthlyTrend = monthlyTrend,
            LabAccounts = await GetLabAccountsAsync(ct),
        };

        return Ok(new { data = dashboard });
    }
}
