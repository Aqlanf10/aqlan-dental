using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    // ─── Dashboard KPIs ─────────────────────────────────────────────────────

    private async Task<IActionResult> GetDashboardSchemaTolerantAsync(string? period)
    {
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = ClinicTimeProvider.ClinicToday();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        var monthStartDate = monthStart.ToDateTime(TimeOnly.MinValue);

        var journalBranch = branchId.HasValue ? @" AND jl.""BranchId"" = @branchId" : "";
        var entryBranch = branchId.HasValue ? @" AND je.""BranchId"" = @branchId" : "";
        var treasuryBranch = branchId.HasValue ? @" AND ""BranchId"" = @branchId" : "";
        var patientBranch = branchId.HasValue ? @" AND p.""BranchId"" = @branchId" : "";
        var expenseBranch = branchId.HasValue ? @" AND ""BranchId"" = @branchId" : "";
        var transferBranch = branchId.HasValue ? @" AND dt.""BranchId"" = @branchId" : "";

        var todayInflow = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(jl.""Debit""), 0)
            FROM ""JournalLines"" jl
            JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
            WHERE jl.""AccountType""::text IN ('Treasury', '0')
              AND je.""EntryDate"" = @today
              AND je.""IsPosted"" = TRUE {journalBranch}");

        var todayOutflow = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(jl.""Credit""), 0)
            FROM ""JournalLines"" jl
            JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
            WHERE jl.""AccountType""::text IN ('Treasury', '0')
              AND je.""EntryDate"" = @today
              AND je.""IsPosted"" = TRUE {journalBranch}");

        var monthInflow = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(jl.""Debit""), 0)
            FROM ""JournalLines"" jl
            JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
            WHERE jl.""AccountType""::text IN ('Treasury', '0')
              AND je.""EntryDate"" >= @monthStart
              AND je.""IsPosted"" = TRUE {journalBranch}");

        var monthOutflow = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(jl.""Credit""), 0)
            FROM ""JournalLines"" jl
            JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
            WHERE jl.""AccountType""::text IN ('Treasury', '0')
              AND je.""EntryDate"" >= @monthStart
              AND je.""IsPosted"" = TRUE {journalBranch}");

        var todayAccruedRevenue = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(jl.""Credit"" - jl.""Debit""), 0)
            FROM ""JournalLines"" jl
            JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
            WHERE jl.""AccountType""::text IN ('Revenue', '4')
              AND je.""EntryDate"" = @today
              AND je.""IsPosted"" = TRUE {journalBranch}");

        var monthAccruedRevenue = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(jl.""Credit"" - jl.""Debit""), 0)
            FROM ""JournalLines"" jl
            JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
            WHERE jl.""AccountType""::text IN ('Revenue', '4')
              AND je.""EntryDate"" >= @monthStart
              AND je.""IsPosted"" = TRUE {journalBranch}");

        var contractOutstanding = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(c.""TotalAmount"" - c.""DiscountAmount"" - COALESCE(pay.""PaidAmount"", 0)), 0)
            FROM ""Contracts"" c
            JOIN ""Patients"" p ON p.""Id"" = c.""PatientId""
            LEFT JOIN (
                SELECT ""ContractId"", SUM(""Amount"") AS ""PaidAmount""
                FROM ""Payments""
                WHERE ""IsActive"" = TRUE AND ""ContractId"" IS NOT NULL
                GROUP BY ""ContractId""
            ) pay ON pay.""ContractId"" = c.""Id""
            WHERE c.""Status""::text IN ('Active', '0', '1')
              AND COALESCE(c.""IsActive"", TRUE) = TRUE {patientBranch}");

        var invoiceOutstanding = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(i.""TotalAmount"" - COALESCE(pay.""PaidAmount"", 0)), 0)
            FROM ""Invoices"" i
            JOIN ""Patients"" p ON p.""Id"" = i.""PatientId""
            LEFT JOIN (
                SELECT settlements.""InvoiceId"", SUM(settlements.""Amount"") AS ""PaidAmount""
                FROM (
                    SELECT ""InvoiceId"", CASE WHEN ""AppliedAmount"" = 0 THEN ""Amount"" ELSE ""AppliedAmount"" END AS ""Amount""
                    FROM ""Payments""
                    WHERE ""IsActive"" = TRUE AND ""InvoiceId"" IS NOT NULL
                    UNION ALL
                    SELECT ""InvoiceId"", ""Amount""
                    FROM ""PaymentAllocations""
                    WHERE ""IsActive"" = TRUE
                ) settlements
                GROUP BY ""InvoiceId""
            ) pay ON pay.""InvoiceId"" = i.""Id""
            WHERE i.""Status""::text IN ('Issued', '1')
              AND i.""IsActive"" = TRUE {patientBranch}");

        var journalEntryCount = await ScalarIntAsync($@"SELECT COUNT(*) FROM ""JournalEntries"" je WHERE 1=1 {entryBranch}");
        var postedEntryCount = await ScalarIntAsync($@"SELECT COUNT(*) FROM ""JournalEntries"" je WHERE je.""IsPosted"" = TRUE {entryBranch}");
        var reversalEntryCount = await ScalarIntAsync($@"SELECT COUNT(*) FROM ""JournalEntries"" je WHERE je.""IsReversal"" = TRUE {entryBranch}");
        // MULTI-CURRENCY: this dashboard figure is YER-denominated (formatted with formatYER on the
        // client), so sum ONLY the YER treasuries — never add SAR/USD balances into a YER total.
        // Foreign-currency balances are shown separately per-currency in the Treasuries tab.
        var totalTreasuryBalance = await ScalarDecimalAsync($@"SELECT COALESCE(SUM(""Balance""), 0) FROM ""Treasuries"" WHERE ""IsActive"" = TRUE AND (""Currency"" = 'YER' OR ""Currency"" IS NULL) {treasuryBranch}");
        var pendingExpenses = await ScalarIntAsync($@"SELECT COUNT(*) FROM ""OperationalExpenses"" WHERE ""ApprovalStatus""::text IN ('Pending', '1') AND ""IsActive"" = TRUE {expenseBranch}");
        var pendingTransfers = await ScalarIntAsync($@"
            SELECT COUNT(*)
            FROM ""VaultTransfers"" vt
            JOIN ""Treasuries"" dt ON dt.""Id"" = vt.""DestinationTreasuryId""
            WHERE vt.""Status""::text IN ('Pending', '0') AND vt.""IsActive"" = TRUE {transferBranch}");
        var activeContracts = await ScalarIntAsync($@"
            SELECT COUNT(*)
            FROM ""Contracts"" c
            JOIN ""Patients"" p ON p.""Id"" = c.""PatientId""
            WHERE c.""Status""::text IN ('Active', '0', '1') AND COALESCE(c.""IsActive"", TRUE) = TRUE {patientBranch}");
        var unpaidInvoicesCount = await ScalarIntAsync($@"
            SELECT COUNT(*)
            FROM ""Invoices"" i
            JOIN ""Patients"" p ON p.""Id"" = i.""PatientId""
            WHERE i.""Status""::text IN ('Issued', '1') AND i.""IsActive"" = TRUE {patientBranch}");
        var draftInvoicesCount = await ScalarIntAsync($@"
            SELECT COUNT(*)
            FROM ""Invoices"" i
            JOIN ""Patients"" p ON p.""Id"" = i.""PatientId""
            WHERE i.""Status""::text IN ('Draft', '0') AND i.""IsActive"" = TRUE {patientBranch}");
        var pendingCommissionsAmount = await ScalarDecimalAsync($@"
            SELECT COALESCE(SUM(ili.""DoctorCommissionAmount""), 0)
            FROM ""InvoiceLineItems"" ili
            JOIN ""Invoices"" i ON i.""Id"" = ili.""InvoiceId""
            JOIN ""Patients"" p ON p.""Id"" = i.""PatientId""
            WHERE ili.""IsActive"" = TRUE
              AND ili.""CommissionStatus""::text NOT IN ('Paid', '3')
              AND ili.""DoctorCommissionAmount"" > 0 {patientBranch}");

        return Ok(new
        {
            TodayInflow = todayInflow,
            TodayOutflow = todayOutflow,
            TodayNet = todayInflow - todayOutflow,
            MonthInflow = monthInflow,
            MonthOutflow = monthOutflow,
            MonthNet = monthInflow - monthOutflow,
            TotalOutstanding = contractOutstanding + invoiceOutstanding,
            ContractOutstanding = contractOutstanding,
            InvoiceOutstanding = invoiceOutstanding,
            TotalTreasuryBalance = totalTreasuryBalance,
            TodayAccruedRevenue = todayAccruedRevenue,
            MonthAccruedRevenue = monthAccruedRevenue,
            JournalEntryCount = journalEntryCount,
            PostedEntryCount = postedEntryCount,
            ReversalEntryCount = reversalEntryCount,
            DualWriteCoverage = journalEntryCount > 0 ? $"{postedEntryCount * 100m / journalEntryCount:F1}%" : "N/A",
            PendingExpenses = pendingExpenses,
            PendingTransfers = pendingTransfers,
            ActiveContracts = activeContracts,
            UnpaidInvoicesCount = unpaidInvoicesCount,
            DraftInvoicesCount = draftInvoicesCount,
            OverdueAmount = 0m,
            PendingCommissionsAmount = pendingCommissionsAmount,
            RecentPayments = Array.Empty<object>(),
            RecentInvoices = Array.Empty<object>(),
            Date = today.ToString("yyyy-MM-dd"),
            Period = period,
            IsConsolidated = !branchId.HasValue,
            SchemaTolerantFallback = true
        });

        async Task<decimal> ScalarDecimalAsync(string sql)
        {
            var result = await ExecuteScalarAsync(sql, branchId, todayDate, monthStartDate);
            return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
        }

        async Task<int> ScalarIntAsync(string sql)
        {
            var result = await ExecuteScalarAsync(sql, branchId, todayDate, monthStartDate);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
    }

    private async Task<object?> ExecuteScalarAsync(string sql, Guid? branchId, DateTime today, DateTime monthStart)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (branchId.HasValue)
        {
            var branchParam = command.CreateParameter();
            branchParam.ParameterName = "branchId";
            branchParam.DbType = DbType.Guid;
            branchParam.Value = branchId.Value;
            command.Parameters.Add(branchParam);
        }

        var todayParam = command.CreateParameter();
        todayParam.ParameterName = "today";
        todayParam.DbType = DbType.Date;
        todayParam.Value = today;
        command.Parameters.Add(todayParam);

        var monthStartParam = command.CreateParameter();
        monthStartParam.ParameterName = "monthStart";
        monthStartParam.DbType = DbType.Date;
        monthStartParam.Value = monthStart;
        command.Parameters.Add(monthStartParam);

        return await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// GET /api/finance-v3/dashboard — Returns KPI data for the Finance V3 dashboard header band.
    /// Supersedes the obsolete and removed GET /api/finance/summary endpoint from PaymentsController.
    /// Migration A: Now reads from JournalEntry/JournalLine (canonical source of truth)
    /// instead of CashFlowTransaction (transitional).
    /// Sprint 1: Admin users with Guid.Empty branchId bypass branch filter → consolidated data.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? period = "today")
    {
        if (!await CanAsync("finance.dashboard", "view")) return Deny();
        try
        {
            // Blocker 6: Branch isolation guard for non-admin users
            // Admin users with no branch (Guid.Empty) bypass the branch filter to view
            // consolidated statistics across all branches (Sprint 1 admin fallback).
            if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
                return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

            var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = ClinicTimeProvider.ClinicToday();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // ── Accrual-based KPIs (from JournalLine — canonical source of truth) ──
        // Revenue = SUM(Credit - Debit) for Revenue account type (credit-normal)
        // Expenses = SUM(Debit - Credit) for Expense account type (debit-normal)
        // Treasury is debit-normal: Debit = increase (cash received/inflow), Credit = decrease (cash paid/outflow)
        var todayTreasuryLines = db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.JournalEntry.EntryDate == today
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value));
        var monthTreasuryLines = db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.JournalEntry.EntryDate >= monthStart
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value));

        // FIX (Migration B): Treasury Debit = Inflow (money received), Credit = Outflow (money paid)
        // In double-entry: Treasury is a debit-normal asset account.
        // Debit increases the balance (cash received) → Inflow
        // Credit decreases the balance (cash paid out) → Outflow
        var todayInflow = await todayTreasuryLines.SumAsync(l => (decimal?)l.Debit) ?? 0;
        var monthInflow = await monthTreasuryLines.SumAsync(l => (decimal?)l.Debit) ?? 0;
        var todayOutflow = await todayTreasuryLines.SumAsync(l => (decimal?)l.Credit) ?? 0;
        var monthOutflow = await monthTreasuryLines.SumAsync(l => (decimal?)l.Credit) ?? 0;

        // ── Outstanding balances ──
        var contractOutstanding = await CalculateContractOutstandingAsync(branchId);
        var invoiceOutstanding = await CalculateInvoiceOutstandingAsync(branchId);

        // ── Journal Entry stats (canonical verification) ──
        var journalEntryCount = await db.JournalEntries.CountAsync(e =>
            !branchId.HasValue || e.BranchId == branchId.Value);
        var postedEntryCount = await db.JournalEntries.CountAsync(e =>
            e.IsPosted && (!branchId.HasValue || e.BranchId == branchId.Value));
        var reversalEntryCount = await db.JournalEntries.CountAsync(e =>
            e.IsReversal && (!branchId.HasValue || e.BranchId == branchId.Value));

        // ── Treasury summary ──
        // MULTI-CURRENCY: YER-denominated dashboard total — sum only YER treasuries (never mix
        // SAR/USD into a YER figure). Foreign balances are shown per-currency in the Treasuries tab.
        var treasuryQuery = db.Treasuries.Where(t => t.IsActive && (t.Currency == "YER" || t.Currency == null));
        if (branchId.HasValue) treasuryQuery = treasuryQuery.Where(t => t.BranchId == branchId.Value);
        var totalTreasuryBalance = await treasuryQuery.SumAsync(t => (decimal?)t.Balance) ?? 0;

        // ── Accrued revenue from posted JournalLines ──
        var todayAccruedRevenue = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Revenue
                && l.JournalEntry.EntryDate == today
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)(l.Credit - l.Debit)) ?? 0;

        var monthAccruedRevenue = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Revenue
                && l.JournalEntry.EntryDate >= monthStart
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)(l.Credit - l.Debit)) ?? 0;

        // ── Pending approvals ──
        var pendingExpensesQuery = db.OperationalExpenses
            .Where(e => e.ApprovalStatus == ApprovalStatus.Pending && e.IsActive);
        var pendingTransfersQuery = db.VaultTransfers
            .Where(t => t.Status == TransferStatus.Pending && t.IsActive);
        // Blocker 6: Branch filter for pending approvals
        if (branchId.HasValue)
        {
            pendingExpensesQuery = pendingExpensesQuery.Where(e => e.BranchId == branchId.Value);
            pendingTransfersQuery = pendingTransfersQuery.Where(t => t.DestinationTreasury.BranchId == branchId.Value);
        }
        var pendingExpenses = await pendingExpensesQuery.CountAsync();
        var pendingTransfers = await pendingTransfersQuery.CountAsync();

        // ── Legacy summary fields (added for daily-operations FinanceView migration) ──
        // Active contracts count
        var activeContractsQuery = db.Contracts.Where(c => c.Status == ContractStatus.Active);
        if (branchId.HasValue) activeContractsQuery = activeContractsQuery.Where(c => c.Patient.BranchId == branchId.Value);
        var activeContracts = await activeContractsQuery.CountAsync();

        // Unpaid (issued) invoices count
        var unpaidInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);
        if (branchId.HasValue) unpaidInvoicesQuery = unpaidInvoicesQuery.Where(i => i.Patient.BranchId == branchId.Value);
        var unpaidInvoicesCount = await unpaidInvoicesQuery.CountAsync();

        // Draft invoices count
        var draftInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Draft && i.IsActive);
        if (branchId.HasValue) draftInvoicesQuery = draftInvoicesQuery.Where(i => i.Patient.BranchId == branchId.Value);
        var draftInvoicesCount = await draftInvoicesQuery.CountAsync();

        // Overdue amount — contracts with installments past due
        // FIN-13: Project only the fields needed + the per-contract PaidAmount (computed
        // in SQL via correlated subquery). Previously loaded Contracts.Include(Payments)
        // into memory and then iterated with in-memory .Sum() on c.Payments per contract.
        // Branch filter moved into the WHERE clause (was previously a per-row `continue`).
        var overdueContractsQuery = db.Contracts
            .Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null);
        if (branchId.HasValue)
            overdueContractsQuery = overdueContractsQuery.Where(c => c.Patient.BranchId == branchId.Value);

        var overdueCandidates = await overdueContractsQuery
            .Select(c => new
            {
                c.StartDate,
                c.InstallmentsCount,
                c.InstallmentAmount,
                c.DownPayment,
                // MULTI-CURRENCY: settle in account currency (YER) via AppliedAmount, falling
                // back to Amount for legacy rows where AppliedAmount==0 (matches FinanceService).
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m
            })
            .ToListAsync();

        var overdueAmount = 0m;
        foreach (var c in overdueCandidates)
        {
            // Sprint 1: Null safety — use GetValueOrDefault instead of ! operator
            // to prevent NullReferenceException when StartDate is null
            var startDate = c.StartDate.GetValueOrDefault();
            if (startDate == default) continue;
            var monthsElapsed = ((today.Year - startDate.Year) * 12) + (today.Month - startDate.Month);
            if (monthsElapsed <= 0) continue;
            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var overAmt = expectedPaid - c.PaidAmount;
            if (overAmt > 0) overdueAmount += overAmt;
        }

        // Pending doctor commissions (calculated/approved/pending but not paid)
        var commissionQuery = db.InvoiceLineItems
            .Where(l => l.IsActive && l.CommissionStatus != CommissionStatus.Paid && l.DoctorCommissionAmount > 0);
        if (branchId.HasValue) commissionQuery = commissionQuery.Where(l => l.Invoice.Patient.BranchId == branchId.Value);
        var pendingCommissionsAmount = await commissionQuery.SumAsync(l => (decimal?)l.DoctorCommissionAmount) ?? 0;

        // Recent payments (last 10)
        var recentPaymentsQuery = db.Payments
            .Include(p => p.Patient)
            .Where(p => p.IsActive);
        if (branchId.HasValue) recentPaymentsQuery = recentPaymentsQuery.Where(p => p.BranchId == branchId);
        var recentPaymentsRaw = await recentPaymentsQuery
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .ToListAsync();
        var recentPayments = recentPaymentsRaw.Select(p => new
        {
            p.Id, p.Amount, PaymentDate = p.PaymentDate.ToString(),
            PatientName = (p.Patient != null ? (p.Patient.FirstName + " " + p.Patient.LastName).Trim() : ""),
            p.PaymentMethod
        }).ToList();

        // Recent invoices (last 10)
        var recentInvoicesQuery = db.Invoices
            .Include(i => i.Patient)
            .Where(i => i.IsActive);
        if (branchId.HasValue) recentInvoicesQuery = recentInvoicesQuery.Where(i => i.Patient.BranchId == branchId);
        var recentInvoicesRaw = await recentInvoicesQuery
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => new { i.Id, i.InvoiceNumber, TotalAmount = i.TotalAmount, Status = i.Status })
            .ToListAsync();
        var recentInvoices = recentInvoicesRaw.Select(i => new { i.Id, i.InvoiceNumber, i.TotalAmount, Status = i.Status.ToString() }).ToList();

        return Ok(new
        {
            // Cash Flow KPIs (from JournalLine — Treasury account type)
            TodayInflow = todayInflow,
            TodayOutflow = todayOutflow,
            TodayNet = todayInflow - todayOutflow,
            MonthInflow = monthInflow,
            MonthOutflow = monthOutflow,
            MonthNet = monthInflow - monthOutflow,

            // Outstanding
            TotalOutstanding = contractOutstanding + invoiceOutstanding,
            ContractOutstanding = contractOutstanding,
            InvoiceOutstanding = invoiceOutstanding,

            // Treasury
            TotalTreasuryBalance = totalTreasuryBalance,

            // Accrued Revenue (from posted JournalLines)
            TodayAccruedRevenue = todayAccruedRevenue,
            MonthAccruedRevenue = monthAccruedRevenue,

            // Journal Entry health
            JournalEntryCount = journalEntryCount,
            PostedEntryCount = postedEntryCount,
            ReversalEntryCount = reversalEntryCount,
            DualWriteCoverage = journalEntryCount > 0
                ? $"{postedEntryCount * 100m / journalEntryCount:F1}%"
                : "N/A",

            // Pending actions
            PendingExpenses = pendingExpenses,
            PendingTransfers = pendingTransfers,

            // Legacy summary fields (for daily-operations FinanceView migration)
            ActiveContracts = activeContracts,
            UnpaidInvoicesCount = unpaidInvoicesCount,
            DraftInvoicesCount = draftInvoicesCount,
            OverdueAmount = overdueAmount,
            PendingCommissionsAmount = pendingCommissionsAmount,
            RecentPayments = recentPayments,
            RecentInvoices = recentInvoices,

            // Period info
            Date = today.ToString("yyyy-MM-dd"),
            Period = period,

            // Consolidation flag
            IsConsolidated = !branchId.HasValue // true when admin views all branches
        });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetDashboard failed");

            try
            {
                // Production safety fallback: older Railway schemas may have enum
                // columns as varchar while EF expects integers, or the reverse. This
                // read-only dashboard fallback casts enum-like columns to text so the
                // page stays available until a verified backup + migration gate runs.
                return await GetDashboardSchemaTolerantAsync(period);
            }
            catch (Exception fallbackEx)
            {
                logger.LogError(fallbackEx, "GetDashboard schema-tolerant fallback failed");
                return StatusCode(500, new { message = "حدث خطأ أثناء تحميل البيانات" });
            }
        }
    }

    // ─── Journal Entries ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/journal-entries — List all journal entries with filtering.
    /// </summary>
    [HttpGet("journal-entries")]
    public async Task<IActionResult> GetJournalEntries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? documentType = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] bool? isPosted = null,
        [FromQuery] bool? isReversal = null)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.JournalEntries
            .Include(e => e.Lines)
            .Where(e => !branchId.HasValue || e.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(documentType) && Enum.TryParse<FinancialDocumentType>(documentType, true, out var dt))
            query = query.Where(e => e.FinancialDocumentType == dt);

        if (DateOnly.TryParse(fromDate, out var from))
            query = query.Where(e => e.EntryDate >= from);

        if (DateOnly.TryParse(toDate, out var to))
            query = query.Where(e => e.EntryDate <= to);

        if (isPosted.HasValue)
            query = query.Where(e => e.IsPosted == isPosted.Value);

        if (isReversal.HasValue)
            query = query.Where(e => e.IsReversal == isReversal.Value);

        var total = await query.CountAsync();

        var entries = await query
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.EntryNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.EntryNumber,
                DocumentType = e.FinancialDocumentType.ToString(),
                e.Description,
                EntryDate = e.EntryDate.ToString("yyyy-MM-dd"),
                e.Currency,
                e.ExchangeRateToYer,
                e.BranchId,
                e.TreasuryId,
                e.PerformedBy,
                e.IsPosted,
                e.IsReversal,
                e.ReversalOfEntryId,
                e.ReversedByEntryId,
                e.CreatedAt,
                TotalDebit = e.Lines.Sum(l => l.Debit),
                TotalCredit = e.Lines.Sum(l => l.Credit),
                LineCount = e.Lines.Count,
                Lines = e.Lines.Select(l => new
                {
                    l.Id,
                    AccountType = l.AccountType.ToString(),
                    l.AccountId,
                    l.Debit,
                    l.Credit,
                    l.Description
                }).ToList()
            })
            .ToListAsync();

        return Ok(new { data = entries, total, page, pageSize });
    }

    /// <summary>
    /// GET /api/finance-v3/journal-entries/{id} — Get a single journal entry with full detail.
    /// </summary>
    [HttpGet("journal-entries/{id:guid}")]
    public async Task<IActionResult> GetJournalEntryById(Guid id)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        // Blocker 6: Reject non-admin with null/empty BranchId
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var entry = await db.JournalEntries
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id,
                e.EntryNumber,
                DocumentType = e.FinancialDocumentType.ToString(),
                FinancialDocumentId = e.FinancialDocumentId,
                e.Description,
                EntryDate = e.EntryDate.ToString("yyyy-MM-dd"),
                e.Currency,
                e.ExchangeRateToYer,
                BranchName = e.Branch != null ? e.Branch.Name : "",
                TreasuryName = e.Treasury != null ? e.Treasury.Name : "",
                PerformedByName = e.PerformedByUser != null ? e.PerformedByUser.Username : "",
                e.IsPosted,
                e.IsReversal,
                ReversalOfEntryNumber = e.ReversalOfEntry != null ? e.ReversalOfEntry.EntryNumber : (string?)null,
                ReversedByEntryNumber = e.ReversedByEntry != null ? e.ReversedByEntry.EntryNumber : (string?)null,
                e.CashierSessionId,
                e.CreatedAt,
                TotalDebit = e.Lines.Sum(l => l.Debit),
                TotalCredit = e.Lines.Sum(l => l.Credit),
                IsBalanced = e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit),
                Lines = e.Lines.Select(l => new
                {
                    l.Id,
                    AccountType = l.AccountType.ToString(),
                    l.AccountId,
                    l.Debit,
                    l.Credit,
                    l.Description
                }).ToList(),
                BranchId = e.BranchId,
                e.PerformedBy
            })
            .FirstOrDefaultAsync();

        if (entry == null)
            return NotFound(new { message = "القيد غير موجود" });

        // Finance V3: Branch scope enforcement for non-admin users
        if (!currentUser.IsAdmin && currentUser.BranchId.HasValue && entry.BranchId != currentUser.BranchId.Value)
            return StatusCode(403, new { message = "ليس لديك صلاحية الوصول إلى قيود فرع آخر" });

        return Ok(entry);
    }

    // ─── Account Balances ────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/account-balances — Returns balances for all account types.
    /// Uses the JournalLine canonical table for balance calculations.
    /// </summary>
    [HttpGet("account-balances")]
    public async Task<IActionResult> GetAccountBalances()
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // FIX: Only include lines from POSTED journal entries in canonical balances.
        // Unposted/draft entries are excluded from official totals (Blocker 4).
        var linesQuery = db.JournalLines
            .Where(l => l.JournalEntry.IsPosted)
            .Where(l => !branchId.HasValue || l.BranchId == branchId.Value);

        // Group by AccountType and calculate net balance.
        // FIX: Reversal entries are INCLUDED here (not filtered out) so that
        // net effect of original + reversal is correctly calculated (Blocker 5).
        var accountBalances = await linesQuery
            .GroupBy(l => l.AccountType)
            .Select(g => new
            {
                AccountType = g.Key.ToString(),
                TotalDebit = (decimal?)g.Sum(l => l.Debit) ?? 0m,
                TotalCredit = (decimal?)g.Sum(l => l.Credit) ?? 0m,
                NetBalance = ((decimal?)g.Sum(l => l.Debit) ?? 0m) - ((decimal?)g.Sum(l => l.Credit) ?? 0m), // Debit-normal balance
                EntryCount = g.Select(l => l.JournalEntryId).Distinct().Count()
            })
            .ToListAsync();

        // Treasury detail breakdown
        var treasuryQuery = db.Treasuries.Where(t => t.IsActive);
        if (branchId.HasValue) treasuryQuery = treasuryQuery.Where(t => t.BranchId == branchId.Value);
        var treasuries = await treasuryQuery
            .Select(t => new
            {
                t.Id,
                t.Name,
                Type = t.Type.ToString(),
                t.Currency,
                t.Balance,
                BranchId = t.BranchId
            })
            .ToListAsync();

        return Ok(new
        {
            AccountBalances = accountBalances,
            Treasuries = treasuries,
            TotalAssets = accountBalances.Find(a => a.AccountType == "Treasury")?.NetBalance ?? 0,
            TotalRevenue = -(accountBalances.Find(a => a.AccountType == "Revenue")?.NetBalance ?? 0),
            TotalExpenses = accountBalances.Find(a => a.AccountType == "Expense")?.NetBalance ?? 0,
            TotalReceivables = accountBalances.Find(a => a.AccountType == "PatientReceivable")?.NetBalance ?? 0,
            TotalPayables = -(accountBalances.Find(a => a.AccountType == "Payable")?.NetBalance ?? 0),

            // Consolidation flag
            IsConsolidated = !branchId.HasValue // true when admin views all branches
        });
    }

    // ─── Daily Cash Summary ──────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/daily-cash-summary — Cash flow breakdown by category for a given date.
    /// Migration B: Now reads from JournalEntry/JournalLine (canonical source of truth)
    /// instead of CashFlowTransaction (transitional).
    ///
    /// Mapping rules (JournalLine → legacy CashFlowTransaction shape):
    ///   Treasury Debit line = Inflow (cash received into treasury)
    ///   Treasury Credit line = Outflow (cash paid out from treasury)
    ///   JournalEntry.FinancialDocumentType → Category mapping:
    ///     Payment → PatientPayment, Refund → Refund, Expense → OperationalExpense,
    ///     SalaryPayment → SalaryPayment, CommissionPayment → DoctorCommission,
    ///     SupplierPayment → SupplierPayment, VaultTransfer → InternalTransfer,
    ///     ContractCancellation / PaymentDeletion → Reversal
    ///   Treasury.Type → PaymentMethod: Vault → "cash", Bank → "bank_transfer"
    /// </summary>
    [HttpGet("daily-cash-summary")]
    public async Task<IActionResult> GetDailyCashSummary([FromQuery] string? date = null)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var targetDate = DateOnly.TryParse(date, out var d) ? d : ClinicTimeProvider.ClinicToday();
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // ── Read from JournalLine (Treasury account type) — canonical source of truth ──
        // Only posted entries are included in official cash figures.
        var treasuryLines = db.JournalLines
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.JournalEntry.EntryDate == targetDate
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value));

        var lines = await treasuryLines
            .Select(l => new
            {
                l.Id,
                l.Debit,
                l.Credit,
                l.JournalEntryId,
                l.JournalEntry.FinancialDocumentType,
                l.JournalEntry.IsReversal,
                TreasuryId = l.AccountId
            })
            .ToListAsync();

        // Load treasury types for PaymentMethod mapping
        var treasuryIds = lines.Select(l => l.TreasuryId).Distinct().ToList();
        var treasuryTypes = await db.Treasuries
            .Where(t => treasuryIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => (TreasuryType?)t.Type);

        // ── By Category breakdown ──
        // Map FinancialDocumentType to legacy Category strings for frontend compatibility
        var byCategory = lines
            .GroupBy(l => new
            {
                Type = l.Debit > 0 ? "Inflow" : "Outflow",
                Category = MapDocumentTypeToCategory(l.FinancialDocumentType),
                l.IsReversal
            })
            .Select(g => new
            {
                Type = g.Key.Type,
                Category = g.Key.Category,
                IsReversal = g.Key.IsReversal,
                Count = g.Count(),
                Total = g.Sum(l => l.Debit > 0 ? l.Debit : l.Credit)
            })
            .OrderByDescending(g => g.Total)
            .ToList();

        // ── By PaymentMethod breakdown ──
        // Map Treasury.Type to PaymentMethod: Vault → "cash", Bank → "bank_transfer"
        // Unknown/missing treasuries default to "cash" for safety
        var byPaymentMethod = lines
            .GroupBy(l =>
            {
                var tType = treasuryTypes.GetValueOrDefault(l.TreasuryId);
                return tType == TreasuryType.Bank ? "bank_transfer" : "cash";
            })
            .Select(g => new
            {
                PaymentMethod = g.Key,
                Count = g.Count(),
                Total = g.Sum(l => l.Debit > 0 ? l.Debit : l.Credit)
            })
            .ToList();

        // Net cash = Inflow total - Outflow total
        // Reversal entries naturally net correctly (debit↔credit are swapped)
        var totalInflow = byCategory.Where(c => c.Type == "Inflow").Sum(c => c.Total);
        var totalOutflow = byCategory.Where(c => c.Type == "Outflow").Sum(c => c.Total);
        var reversalCount = byCategory.Where(c => c.IsReversal).Sum(c => c.Count);

        // Journal entries for the same day (posted only for accurate counts)
        var journalEntries = await db.JournalEntries
            .Where(e => e.EntryDate == targetDate && e.IsPosted && (!branchId.HasValue || e.BranchId == branchId.Value))
            .CountAsync();

        return Ok(new
        {
            Date = targetDate.ToString("yyyy-MM-dd"),
            TotalInflow = totalInflow,
            TotalOutflow = totalOutflow,
            NetCash = totalInflow - totalOutflow,
            ByCategory = byCategory,
            ByPaymentMethod = byPaymentMethod,
            TransactionCount = byCategory.Sum(c => c.Count),
            ReversalCount = reversalCount,
            JournalEntryCount = journalEntries,

            // Consolidation flag
            IsConsolidated = !branchId.HasValue // true when admin views all branches
        });
    }
    // ─── Profit and Loss (Basic) ─────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/profit-loss — Basic P&L using the formulas from the Foundation Spec (Section 4.6).
    /// Migration B: ALL figures now come from posted JournalLines (canonical source of truth).
    /// - Accrued P&L: Revenue/Expense lines (accrual basis) — unchanged from Migration A
    /// - Cash-flow figures: Now derived from Treasury JournalLines instead of CashFlowTransaction
    ///
    /// Double-entry rules used:
    ///   Treasury Debit = Inflow (cash received), Treasury Credit = Outflow (cash paid)
    ///   Revenue Credit = earned revenue (credit-normal)
    ///   Expense Debit = incurred expense (debit-normal)
    ///   Reversals naturally net: they swap debit↔credit on the same account types
    /// </summary>
    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitAndLoss(
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = ClinicTimeProvider.ClinicToday();
        var from = DateOnly.TryParse(fromDate, out var f) ? f : new DateOnly(today.Year, today.Month, 1);
        var to = DateOnly.TryParse(toDate, out var t) ? t : today;

        // ── Accrued P&L from posted JournalLines (canonical, accrual basis) ──
        var accruedRevenueQuery = db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Revenue
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value));
        var accruedRevenue = await accruedRevenueQuery.SumAsync(l => (decimal?)(l.Credit - l.Debit)) ?? 0;

        var accruedExpensesQuery = db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Expense
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value));
        var accruedExpenses = await accruedExpensesQuery.SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0;

        var accruedNetProfit = accruedRevenue - accruedExpenses;

        // ── Cash Collections from Treasury JournalLines (Migration B) ──
        // Cash inflows from patient payments: Treasury Debit lines in Payment-type entries
        // (non-reversal). Treasury Debit = money received into treasury.
        var cashCollections = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.Debit > 0
                && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.Payment
                && !l.JournalEntry.IsReversal
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Debit) ?? 0;

        // Cash outflows from refunds: Treasury Credit lines in Refund-type entries
        var cashRefunds = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.Credit > 0
                && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.Refund
                && !l.JournalEntry.IsReversal
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Credit) ?? 0;

        // Payment reversals (deleted payments): Treasury Credit lines in PaymentDeletion reversal entries
        // These reverse the original payment's Treasury Debit, so they create a Treasury Credit
        var patientReversalTotal = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.Credit > 0
                && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.PaymentDeletion
                && l.JournalEntry.IsReversal
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Credit) ?? 0;

        var netCashCollections = cashCollections - cashRefunds - patientReversalTotal;

        // ── Cost categories from Treasury JournalLines (Migration B) ──
        // Each cost category = original outflows (Treasury Credit) minus its own reversal (Treasury Debit).
        // Reversals swap debit↔credit, so a reversal of an expense creates a Treasury Debit.

        // Operating Expenses: Treasury Credit from Expense entries, net of reversal Treasury Debit
        var operatingExpenses = await CalculateCashCategoryAsync(
            FinancialDocumentType.Expense, from, to, branchId);

        // Salary Payments: Treasury Credit from SalaryPayment entries, net of reversal Treasury Debit
        var salaryTotal = await CalculateCashCategoryAsync(
            FinancialDocumentType.SalaryPayment, from, to, branchId);

        // Doctor Commissions: Treasury Credit from CommissionPayment entries, net of reversal Treasury Debit
        var commissionTotal = await CalculateCashCategoryAsync(
            FinancialDocumentType.CommissionPayment, from, to, branchId);

        // Supplier Payments: Treasury Credit from SupplierPayment entries, net of reversal Treasury Debit
        var supplierTotal = await CalculateCashCategoryAsync(
            FinancialDocumentType.SupplierPayment, from, to, branchId);

        var totalCosts = operatingExpenses + salaryTotal + commissionTotal + supplierTotal;
        var cashNetProfit = netCashCollections - totalCosts;

        // ── Transaction counts for summary ──
        var revenueTransactionCount = await db.JournalEntries
            .CountAsync(e => e.FinancialDocumentType == FinancialDocumentType.Payment
                && !e.IsReversal
                && e.EntryDate >= from && e.EntryDate <= to
                && e.IsPosted
                && (!branchId.HasValue || e.BranchId == branchId.Value));

        var expenseTransactionCount = await db.JournalEntries
            .CountAsync(e => e.FinancialDocumentType == FinancialDocumentType.Expense
                && !e.IsReversal
                && e.EntryDate >= from && e.EntryDate <= to
                && e.IsPosted
                && (!branchId.HasValue || e.BranchId == branchId.Value));

        return Ok(new
        {
            Period = new { From = from.ToString("yyyy-MM-dd"), To = to.ToString("yyyy-MM-dd") },

            // Accrued P&L (from posted JournalLines — accrual basis)
            AccruedRevenue = accruedRevenue,
            AccruedExpenses = accruedExpenses,
            AccruedNetProfit = accruedNetProfit,

            // Cash-flow figures (from Treasury JournalLines — Migration B)
            CashCollections = cashCollections,
            CashRefunds = cashRefunds,
            PatientPaymentReversals = patientReversalTotal,
            NetCashCollections = netCashCollections,
            NetCashCollectionsFormula = "Treasury Debit(Payment) - Treasury Credit(Refund) - Treasury Credit(PaymentDeletion)",
            OperatingExpenses = operatingExpenses,
            SalaryPayments = salaryTotal,
            DoctorCommissions = commissionTotal,
            SupplierPayments = supplierTotal,
            OperatingExpensesFormula = "Treasury Credit(Expense non-reversal) - Treasury Debit(Expense reversal)",
            SalaryPaymentsFormula = "Treasury Credit(SalaryPayment non-reversal) - Treasury Debit(SalaryPayment reversal)",
            DoctorCommissionsFormula = "Treasury Credit(CommissionPayment non-reversal) - Treasury Debit(CommissionPayment reversal)",
            SupplierPaymentsFormula = "Treasury Credit(SupplierPayment non-reversal) - Treasury Debit(SupplierPayment reversal)",
            TotalCosts = totalCosts,
            CashNetProfit = cashNetProfit,
            ProfitMargin = netCashCollections > 0 ? cashNetProfit * 100m / netCashCollections : 0,

            // Reversal coverage status — which write paths have actual correction endpoints
            ReversalCoverage = new
            {
                OperationalExpenseReversal = "Implemented — DELETE /api/expenses/{id} creates JournalEntry reversal",
                SalaryPaymentReversal = "Implemented — PUT /api/salaries/{id}/reverse creates JournalEntry reversal",
                CommissionPaymentReversal = "Deferred — no standalone reversal endpoint yet; commission payments cannot be reversed via API",
                SupplierPaymentReversal = "Deferred — no standalone reversal endpoint yet; supplier payments cannot be reversed via API",
                InvoiceCancellationReversal = "Implemented — cancel creates JournalEntry reversal via FinanceService"
            },

            // Summary counts
            RevenueTransactionCount = revenueTransactionCount,
            ExpenseTransactionCount = expenseTransactionCount,

            // Consolidation flag
            IsConsolidated = !branchId.HasValue // true when admin views all branches
        });
    }
    // ─── Patient Balance ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/patient-balance/{patientId} — Patient financial balance per Section 4.1.
    /// Migration B: Now uses JournalLine as the canonical source for balance calculation.
    /// Balance = SUM(Debit) - SUM(Credit) for PatientReceivable + PatientAdvance lines for this patient.
    /// PatientReceivable Debit = invoiced amount (patient owes us), Credit = payment settled
    /// PatientAdvance Debit = refund/adjustment, Credit = advance payment received
    /// Entity-based fields (TotalInvoiced, TotalPaid, TotalRefunds) are kept for UI compatibility.
    /// </summary>
    [HttpGet("patient-balance/{patientId:guid}")]
    public async Task<IActionResult> GetPatientBalance(Guid patientId)
    {
        if (!await CanAsync("finance.patient_balance", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null)
            return NotFound(new { message = "المريض غير موجود" });

        // Blocker 6: Branch filter for non-admin users
        if (!currentUser.IsAdmin && patient.BranchId != currentUser.BranchId)
            return StatusCode(403, new { message = "ليس لديك صلاحية الوصول إلى بيانات مريض من فرع آخر" });

        // ── JournalLine-based balance (canonical, accrual basis) ──
        // PatientReceivable lines: Debit = invoiced (patient owes), Credit = payment settled
        // PatientAdvance lines: Credit = advance received, Debit = refund/adjustment
        // Net patient balance = SUM(Debit) - SUM(Credit) across both account types
        //   Positive = patient owes money, Negative = clinic owes patient (advance overpayment)
        var journalBalance = await db.JournalLines
            .Where(l => (l.AccountType == JournalAccountType.PatientReceivable || l.AccountType == JournalAccountType.PatientAdvance)
                && l.AccountId == patientId
                && l.JournalEntry.IsPosted
                && l.BranchId == patient.BranchId)
            .GroupBy(l => l.AccountType)
            .Select(g => new
            {
                AccountType = g.Key,
                TotalDebit = (decimal?)g.Sum(l => l.Debit) ?? 0m,
                TotalCredit = (decimal?)g.Sum(l => l.Credit) ?? 0m
            })
            .ToListAsync();

        var receivableLine = journalBalance.FirstOrDefault(b => b.AccountType == JournalAccountType.PatientReceivable);
        var advanceLine = journalBalance.FirstOrDefault(b => b.AccountType == JournalAccountType.PatientAdvance);

        var journalReceivable = (receivableLine?.TotalDebit ?? 0) - (receivableLine?.TotalCredit ?? 0);
        var journalAdvance = (advanceLine?.TotalDebit ?? 0) - (advanceLine?.TotalCredit ?? 0);
        var journalNetBalance = journalReceivable + journalAdvance;

        // ── Entity-based detail fields (for UI compatibility) ──
        // Sprint Patient-Finance-Ledger: Now includes contract totals and all active payments
        // so EntityBalance matches FinanceService.GetPatientFinanceSummaryAsync.
        var totalInvoiced = await db.Invoices
            .Where(i => i.PatientId == patientId && i.IsActive
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid))
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        // Contract-based costs (same calculation as GetPatientFinanceSummaryAsync)
        var totalContracted = await db.Contracts
            .Where(c => c.PatientId == patientId && c.IsActive)
            .SumAsync(c => (decimal?)(c.TotalAmount - c.DiscountAmount)) ?? 0;

        // MULTI-CURRENCY: patient balance is YER-denominated → settle in AppliedAmount
        // (YER-equivalent), falling back to Amount for legacy rows. Sign filters stay on
        // Amount because Amount and AppliedAmount always share the same sign.
        var totalPaid = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive && p.Amount > 0)
            .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0;

        var totalRefunds = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive && p.Amount < 0)
            .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0; // negative values

        var totalDiscounts = await db.Contracts
            .Where(c => c.PatientId == patientId && c.IsActive)
            .SumAsync(c => (decimal?)c.DiscountAmount) ?? 0;

        // QA-594: Unbilled visits — sessions with AmountDueReference > 0 and no
        // invoice linked via Invoice.VisitId. These represent performed work
        // that has not been invoiced/contracted and previously vanished from
        // the patient's balance. Included in EntityBalance so the finance
        // dashboard matches FinanceService.GetPatientFinanceSummaryAsync.
        var billedVisitIdsSet = await db.Invoices
            .Where(i => i.PatientId == patientId && i.VisitId.HasValue && i.IsActive)
            .Select(i => i.VisitId!.Value)
            .ToListAsync();
        var billedVisitHash = billedVisitIdsSet.ToHashSet();

        var unbilledVisitRows = await db.Visits
            .Where(v => v.PatientId == patientId && v.IsActive
                     && v.AmountDueReference.HasValue && v.AmountDueReference > 0)
            .ToListAsync();
        var unbilledVisitsAmount = unbilledVisitRows
            .Where(v => !billedVisitHash.Contains(v.Id))
            .Sum(v => v.AmountDueReference ?? 0m);

        var netPaid = totalPaid + totalRefunds; // refunds are negative
        // Sprint Patient-Finance-Ledger: EntityBalance now includes contract totals
        // so it matches the patient-facing outstanding balance from FinanceService
        // FIN-12 FIX: Clamp to 0 (matching FinanceService.GetPatientFinanceSummaryAsync which
        // uses Math.Max(0, totalCost - totalPaid)). Previously EntityBalance could be negative
        // (clinic owes patient) while the service returned 0 — causing the portal and the
        // finance dashboard to show different outstanding balances for the same patient.
        // QA-594: also include unbilled visits so EntityBalance reflects session debt.
        var entityBalance = Math.Max(0m, (totalInvoiced + totalContracted + unbilledVisitsAmount) - netPaid);

        // Contract outstanding
        var contractOutstanding = await db.Contracts
            .Where(c => c.PatientId == patientId && c.Status == ContractStatus.Active && c.IsActive)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount))
            .SumAsync();

        // Use JournalLine balance as the canonical Balance field
        return Ok(new
        {
            PatientId = patientId,
            PatientName = (patient.FirstName + " " + patient.LastName).Trim(),
            PatientNumber = patient.PatientNumber,
            TotalInvoiced = totalInvoiced,
            TotalContracted = totalContracted, // Sprint Patient-Finance-Ledger: new field
            UnbilledVisitsAmount = unbilledVisitsAmount, // QA-594: performed but not invoiced
            TotalPaid = totalPaid,
            TotalRefunds = Math.Abs(totalRefunds),
            NetPaid = netPaid,
            TotalDiscounts = totalDiscounts,
            Balance = journalNetBalance, // JournalLine-based canonical balance
            EntityBalance = entityBalance, // Entity-based balance for reconciliation (now includes contracts + unbilled visits)
            ContractOutstanding = contractOutstanding,
            HasOutstanding = journalNetBalance > 0 || unbilledVisitsAmount > 0,
            JournalReceivable = journalReceivable,
            JournalAdvance = journalAdvance,
            AvailableAdvance = Math.Max(0m, -journalAdvance)
        });
    }

    // ─── Treasury Detail ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/treasuries — Treasury accounts with recent transactions.
    /// </summary>
    [HttpGet("treasuries")]
    public async Task<IActionResult> GetTreasuries()
    {
        if (!await CanAsync("finance.treasuries", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Treasuries
            .Where(t => t.IsActive);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);

        var treasuries = await query
            .Select(t => new
            {
                t.Id,
                t.Name,
                Type = t.Type.ToString(),
                t.Balance,
                t.BranchId,
                LastUpdated = t.UpdatedAt
            })
            .OrderByDescending(t => t.Balance)
            .ToListAsync();

        return Ok(new { data = treasuries });
    }

    // ─── Audit Trail ─────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/audit — Recent financial audit events.
    /// Migration D: Financial detail data now derived from JournalEntry/JournalLine
    /// (canonical source of truth) instead of CashFlowTransaction. The base audit
    /// log entries still come from AuditLog, but JournalEntry enrichment provides
    /// amount, type, treasury, description, and reversal status from the ledger.
    /// CashFlowTransaction has been removed from the resource filter — it is
    /// write-only (dual-write) and should not appear in the primary audit view.
    /// </summary>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? resource = null,
        [FromQuery] string? action = null)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        // Blocker 6: Audit endpoint restricted to Admin only
        // Financial audit trail contains cross-branch sensitive data;
        // non-admin Accountant users should not access other branches' audit records.
        if (!currentUser.IsAdmin)
            return StatusCode(403, new { message = "الاطلاع على سجل المراجعة متاح للمسؤول فقط." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Migration D: Filter to finance-related audit entries only.
        // CashFlowTransaction removed — it is now write-only (dual-write) and
        // should not appear in the primary audit view. JournalEntry is the
        // canonical auditable entity for financial operations.
        var financeResources = new[] { "Payment", "OperationalExpense", "SalaryRecord",
            "AdvancePayment", "VaultTransfer", "SupplierBill", "SupplierBillPayment",
            "DoctorCommissionPayment", "Treasury", "JournalEntry" };

        var query = db.AuditLogs
            .Where(a => financeResources.Contains(a.Resource));

        if (!string.IsNullOrWhiteSpace(resource))
            query = query.Where(a => a.Resource == resource);

        if (!string.IsNullOrWhiteSpace(action) && Enum.TryParse<AuditAction>(action, true, out var actionFilter))
            query = query.Where(a => a.Action == actionFilter);

        var total = await query.CountAsync();
        var entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                Action = a.Action.ToString(),
                a.Resource,
                a.ResourceId,
                a.UserId,
                Username = a.User != null ? a.User.Username : "",
                a.CreatedAt
            })
            .ToListAsync();

        // ── Migration D: Enrich audit entries with JournalEntry/JournalLine data ──
        // For entries that reference a JournalEntry (Resource == "JournalEntry"),
        // load the corresponding JournalEntry and enrich the response with
        // financial detail derived from the canonical ledger source.
        // For non-JournalEntry entries (e.g. Payment, OperationalExpense), try
        // to find a linked JournalEntry via FinancialDocumentId.
        var jeResourceIds = entries
            .Where(e => e.Resource == "JournalEntry" && e.ResourceId.HasValue)
            .Select(e => e.ResourceId!.Value)
            .ToList();

        var nonJeResourceIds = entries
            .Where(e => e.Resource != "JournalEntry" && e.ResourceId.HasValue)
            .Select(e => new { e.Resource, e.ResourceId!.Value })
            .ToList();

        // Load JournalEntries by direct ID (for JournalEntry audit entries)
        var journalEntriesById = jeResourceIds.Any()
            ? await db.JournalEntries
                .Include(je => je.Lines)
                .Where(je => jeResourceIds.Contains(je.Id))
                .ToDictionaryAsync(je => je.Id)
            : new Dictionary<Guid, JournalEntry>();

        // Load JournalEntries by FinancialDocumentId for non-JournalEntry audit entries
        // (e.g. when a Payment or Expense was created, a JournalEntry was also created)
        // Migration D: This replaces reading from CashFlowTransaction — data now
        // comes from the JournalEntry canonical source.
        var journalEntriesByDocId = new Dictionary<Guid, JournalEntry>();
        if (nonJeResourceIds.Any())
        {
            // Map audit Resource names to FinancialDocumentType for lookup
            var docTypeLookup = new Dictionary<string, List<FinancialDocumentType>>
            {
                ["Payment"] = [FinancialDocumentType.Payment, FinancialDocumentType.AdvancePayment],
                ["AdvancePayment"] = [FinancialDocumentType.AdvancePayment],
                ["OperationalExpense"] = [FinancialDocumentType.Expense],
                ["SalaryRecord"] = [FinancialDocumentType.SalaryPayment],
                ["DoctorCommissionPayment"] = [FinancialDocumentType.CommissionPayment],
                ["VaultTransfer"] = [FinancialDocumentType.VaultTransfer],
                ["Treasury"] = [FinancialDocumentType.VaultTransfer],
                // SupplierBill: bills themselves don't create JournalEntries — only payments do
                // (FinancialDocumentType.SupplierPayment). Mapped here for completeness; enrichment
                // will be null for bill-creation audit entries since FinancialDocumentId differs.
                ["SupplierBill"] = [FinancialDocumentType.SupplierPayment],
                ["SupplierBillPayment"] = [FinancialDocumentType.SupplierPayment],
            };

            foreach (var group in nonJeResourceIds.GroupBy(x => x.Resource))
            {
                if (!docTypeLookup.TryGetValue(group.Key, out var docTypes)) continue;
                var resourceIds = group.Select(x => x.Value).ToList();
                var matched = await db.JournalEntries
                    .Include(je => je.Lines)
                    .Where(je => docTypes.Contains(je.FinancialDocumentType)
                        && resourceIds.Contains(je.FinancialDocumentId))
                    .ToListAsync();
                foreach (var je in matched)
                    journalEntriesByDocId[je.FinancialDocumentId] = je;
            }
        }

        // Build enriched response — keep the same response shape, add enrichment fields
        var enrichedEntries = entries.Select(e =>
        {
            // Find the corresponding JournalEntry for enrichment
            JournalEntry? je = null;
            if (e.Resource == "JournalEntry" && e.ResourceId.HasValue)
                journalEntriesById.TryGetValue(e.ResourceId.Value, out je);
            else if (e.ResourceId.HasValue)
                journalEntriesByDocId.TryGetValue(e.ResourceId.Value, out je);

            // Derive enrichment fields from JournalEntry/JournalLine (canonical source)
            if (je != null)
            {
                // Treasury: JournalLine where AccountType == Treasury
                var treasuryLine = je.Lines.FirstOrDefault(l => l.AccountType == JournalAccountType.Treasury);

                return new
                {
                    e.Id,
                    e.Action,
                    e.Resource,
                    e.ResourceId,
                    e.UserId,
                    e.Username,
                    e.CreatedAt,

                    // ── Migration D: Enrichment from JournalEntry (canonical source) ──
                    // These fields replace what was previously derived from CashFlowTransaction.
                    EntryDate = (string?)je.EntryDate.ToString("yyyy-MM-dd"),
                    FinancialDocumentType = (string?)je.FinancialDocumentType.ToString(),
                    Category = (string?)MapDocumentTypeToCategory(je.FinancialDocumentType),
                    Description = (string?)je.Description,
                    Amount = (decimal?)je.Lines.Sum(l => l.Debit),  // Total debit = total transaction amount
                    TreasuryId = (Guid?)treasuryLine?.AccountId,          // JournalLine.AccountId where Treasury
                    TreasuryName = (string?)null,                  // Requires separate lookup; set null with comment
                    IsReversal = (bool?)je.IsReversal,
                    ReversalOfEntryId = (Guid?)je.ReversalOfEntryId,
                    PerformedBy = (Guid?)je.PerformedBy,
                };
            }

            // No JournalEntry found — return basic audit entry with null enrichment fields
            return new
            {
                e.Id,
                e.Action,
                e.Resource,
                e.ResourceId,
                e.UserId,
                e.Username,
                e.CreatedAt,

                // ── Migration D: No JournalEntry found — enrichment fields null ──
                EntryDate = (string?)null,
                FinancialDocumentType = (string?)null,
                Category = (string?)null,
                Description = (string?)null,
                Amount = (decimal?)null,
                TreasuryId = (Guid?)null,
                TreasuryName = (string?)null,  // TreasuryName: not available without JournalEntry
                IsReversal = (bool?)null,
                ReversalOfEntryId = (Guid?)null,
                PerformedBy = (Guid?)null,
            };
        }).ToList();

        // Resolve TreasuryName for entries that have a TreasuryId
        var treasuryIdsToResolve = enrichedEntries
            .Where(e => e.TreasuryId.HasValue)
            .Select(e => e.TreasuryId!.Value)
            .Distinct()
            .ToList();
        var treasuryNames = treasuryIdsToResolve.Any()
            ? await db.Treasuries
                .Where(t => treasuryIdsToResolve.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name)
            : new Dictionary<Guid, string>();

        // Final pass: set TreasuryName from lookup
        var finalEntries = enrichedEntries.Select(e =>
        {
            var treasuryName = e.TreasuryId.HasValue
                ? treasuryNames.GetValueOrDefault(e.TreasuryId.Value)
                : null;

            return new
            {
                e.Id,
                e.Action,
                e.Resource,
                e.ResourceId,
                e.UserId,
                e.Username,
                e.CreatedAt,
                e.EntryDate,
                e.FinancialDocumentType,
                e.Category,
                e.Description,
                e.Amount,
                e.TreasuryId,
                TreasuryName = treasuryName,   // Migration D: resolved from Treasury entity
                e.IsReversal,
                e.ReversalOfEntryId,
                e.PerformedBy,
            };
        }).ToList();

        return Ok(new { data = finalEntries, total, page, pageSize });
    }

    // ─── Patient Accounts (Sub-ledger) ─────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/patient-accounts — Paginated list of patients with outstanding balances.
    /// Migration B: Now uses JournalLine aggregation as the canonical Balance source.
    /// Entity-based fields (TotalInvoiced, TotalPaid, TotalRefunds) are kept for UI compatibility.
    /// </summary>
    [HttpGet("patient-accounts")]
    public async Task<IActionResult> GetPatientAccounts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // ── Pre-compute JournalLine balances per patient ──
        // Group by AccountId (= PatientId) for PatientReceivable + PatientAdvance
        var journalBalances = await db.JournalLines
            .Where(l => (l.AccountType == JournalAccountType.PatientReceivable || l.AccountType == JournalAccountType.PatientAdvance)
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                PatientId = g.Key,
                Balance = ((decimal?)g.Sum(l => l.Debit) ?? 0m) - ((decimal?)g.Sum(l => l.Credit) ?? 0m)
            })
            .ToDictionaryAsync(b => b.PatientId, b => b.Balance);

        var query = db.Patients
            .Where(p => p.IsActive)
            .Where(p => !branchId.HasValue || p.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.FirstName.Contains(search) || p.LastName.Contains(search) || p.PatientNumber.Contains(search) || (p.Phone != null && p.Phone.Contains(search)));

        var total = await query.CountAsync();

        var patientsRaw = await query
            .OrderBy(p => p.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                PatientId = p.Id,
                p.PatientNumber,
                PatientName = (p.FirstName + " " + p.MiddleName + " " + p.LastName).Trim(),
                Phone = p.Phone,
                TotalInvoiced = db.Invoices.Where(i => i.PatientId == p.Id && i.IsActive && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid)).Sum(i => (decimal?)i.TotalAmount) ?? 0,
                TotalPaid = db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive && pay.Amount > 0).Sum(pay => (decimal?)(pay.AppliedAmount == 0 ? pay.Amount : pay.AppliedAmount)) ?? 0,
                TotalRefunds = db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive && pay.Amount < 0).Sum(pay => (decimal?)Math.Abs(pay.AppliedAmount == 0 ? pay.Amount : pay.AppliedAmount)) ?? 0,
                OutstandingInvoices = db.Invoices.Count(i => i.PatientId == p.Id && i.IsActive && i.Status == InvoiceStatus.Issued),
                ActiveContracts = db.Contracts.Count(c => c.PatientId == p.Id && c.IsActive && c.Status == ContractStatus.Active)
            })
            .ToListAsync();

        // Apply JournalLine balance in memory (can't translate dictionary lookup to SQL)
        var patients = patientsRaw.Select(p =>
        {
            var journalBal = journalBalances.GetValueOrDefault(p.PatientId);
            return new
            {
                p.PatientId,
                p.PatientNumber,
                p.PatientName,
                p.Phone,
                p.TotalInvoiced,
                p.TotalPaid,
                p.TotalRefunds,
                Balance = journalBal, // JournalLine canonical balance
                p.OutstandingInvoices,
                p.ActiveContracts,
                HasOutstanding = journalBal > 0
            };
        }).ToList();

        return Ok(new { data = patients, total, page, pageSize });
    }

    // ─── Trial Balance ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/trial-balance — Trial balance from posted JournalLines.
    /// </summary>
    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] string? asOfDate = null)
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var cutoff = DateOnly.TryParse(asOfDate, out var d) ? d : ClinicTimeProvider.ClinicToday();

        var linesQuery = db.JournalLines
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate <= cutoff)
            .Where(l => !branchId.HasValue || l.BranchId == branchId.Value);

        var accounts = await linesQuery
            .GroupBy(l => l.AccountType)
            .Select(g => new
            {
                AccountType = g.Key.ToString(),
                TotalDebit = (decimal?)g.Sum(l => l.Debit) ?? 0m,
                TotalCredit = (decimal?)g.Sum(l => l.Credit) ?? 0m,
                NetBalance = ((decimal?)g.Sum(l => l.Debit) ?? 0m) - ((decimal?)g.Sum(l => l.Credit) ?? 0m),
                EntryCount = g.Select(l => l.JournalEntryId).Distinct().Count()
            })
            .ToListAsync();

        var grandTotalDebit = accounts.Sum(a => a.TotalDebit);
        var grandTotalCredit = accounts.Sum(a => a.TotalCredit);

        return Ok(new
        {
            AsOfDate = cutoff.ToString("yyyy-MM-dd"),
            Accounts = accounts,
            GrandTotalDebit = grandTotalDebit,
            GrandTotalCredit = grandTotalCredit,
            Difference = Math.Abs(grandTotalDebit - grandTotalCredit),
            IsBalanced = Math.Abs(grandTotalDebit - grandTotalCredit) < 0.005m
        });
    }

    // ─── Active Cashier Session ─────────────────────────────────────────────
    // NOTE: GET /api/finance-v3/active-cashier-session was REMOVED (Phase 6 cleanup).
    // It was a duplicate of GET /api/finance-v3/cashier-sessions/active below.
    // Use cashier-sessions/active for the canonical Finance V3 active session endpoint.

    // ─── Payments List ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/payments — Paginated payments with method and date range filtering.
    /// Migration B: Already reads from Payment entity (not CashFlowTransaction).
    /// Added JournalEntry verification: each payment's amount is cross-referenced
    /// with the corresponding Treasury Debit JournalLine for data integrity.
    /// </summary>
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? method = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        if (!await CanAsync("finance.payments", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.IsActive && p.Amount > 0);

        if (branchId.HasValue)
            query = query.Where(p => p.Patient.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(p => p.PaymentMethod == method);

        if (DateOnly.TryParse(fromDate, out var from))
            query = query.Where(p => p.PaymentDate >= from);

        if (DateOnly.TryParse(toDate, out var to))
            query = query.Where(p => p.PaymentDate <= to);

        var total = await query.CountAsync();

        var paymentsRaw = await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.PatientId,
                p.ContractId,
                p.InvoiceId,
                p.Amount,
                p.Currency,
                p.AccountCurrency,
                p.AppliedAmount,
                p.ExchangeRateToAccountCurrency,
                PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd"),
                p.PaymentMethod,
                Specialty = p.Doctor != null ? p.Doctor.Specialty : null,
                ServiceDescription = p.Notes,
                p.ReceiptNumber,
                PatientName = (p.Patient.FirstName + " " + p.Patient.LastName).Trim(),
                PatientNumber = p.Patient.PatientNumber,
                DoctorName = p.Doctor != null ? p.Doctor.Name : null,
                p.Notes,
                p.BranchId,
                p.CreatedAt
            })
            .ToListAsync();

        // Enrich with reversal status from JournalEntry
        var paymentIds = paymentsRaw.Select(p => p.Id).ToList();
        var reversalEntries = await db.JournalEntries
            .Where(e => e.FinancialDocumentType == FinancialDocumentType.PaymentDeletion
                && e.IsReversal
                && paymentIds.Contains(e.ReversalOfEntryId ?? Guid.Empty))
            .Select(e => new { e.ReversalOfEntryId, ReversedById = e.Id })
            .ToListAsync();

        // Also find which payments have JournalEntries (for reconciliation indicator)
        var paymentJournalEntries = await db.JournalEntries
            .Where(e => e.FinancialDocumentType == FinancialDocumentType.Payment
                && !e.IsReversal
                && paymentIds.Contains(e.FinancialDocumentId))
            .Select(e => new { e.FinancialDocumentId, e.Id })
            .ToListAsync();

        var payments = paymentsRaw.Select(p =>
        {
            // Find the original JE for this payment to determine reversal status
            var originalJe = paymentJournalEntries.FirstOrDefault(je => je.FinancialDocumentId == p.Id);
            var isReversal = false;
            var reversedById = (Guid?)null;
            if (originalJe != null)
            {
                var revEntry = reversalEntries.FirstOrDefault(r => r.ReversalOfEntryId == originalJe.Id);
                if (revEntry != null)
                {
                    reversedById = revEntry.ReversedById;
                    isReversal = true;
                }
            }
            return new
            {
                p.Id,
                p.PatientId,
                p.ContractId,
                p.InvoiceId,
                p.Amount,
                p.Currency,
                p.AccountCurrency,
                p.AppliedAmount,
                p.ExchangeRateToAccountCurrency,
                p.PaymentDate,
                p.PaymentMethod,
                p.Specialty,
                p.ServiceDescription,
                p.ReceiptNumber,
                PaymentNumber = p.ReceiptNumber, // Alias: receiptNumber exposed as paymentNumber for frontend compat
                p.PatientName,
                p.PatientNumber,
                p.DoctorName,
                p.Notes,
                p.BranchId,
                p.CreatedAt,
                IsReversal = isReversal,
                ReversedById = reversedById,
                Status = isReversal ? "Reversed" : "Active",
                HasJournalEntry = originalJe != null
            };
        }).ToList();

        return Ok(new { data = payments, total, page, pageSize });
    }

    // ─── Invoices List ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/invoices — Paginated invoices with status filtering.
    /// Migration B: Already reads from Invoice entity (not CashFlowTransaction).
    /// Balance is calculated from Invoice.TotalAmount minus direct payments and active advance allocations.
    /// JournalLine enrichment not yet applied here - deferred to a future phase.
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        if (!await CanAsync("finance.invoices", "view")) return Deny();
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Invoices
            .Include(i => i.Patient)
            .Where(i => i.IsActive);

        if (branchId.HasValue)
            query = query.Where(i => i.Patient.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var s))
            query = query.Where(i => i.Status == s);

        if (DateOnly.TryParse(fromDate, out var from))
            query = query.Where(i => i.CreatedAt >= from.ToDateTime(TimeOnly.MinValue));

        if (DateOnly.TryParse(toDate, out var to))
            query = query.Where(i => i.CreatedAt <= to.ToDateTime(TimeOnly.MaxValue));

        var total = await query.CountAsync();

        var invoicesRaw = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.PatientId,
                i.Status,
                i.Subtotal,
                i.DiscountAmount,
                i.TotalAmount,
                DirectPaidAmount = i.Payments.Where(p => p.IsActive && p.Amount > 0).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
                AdvanceAllocatedAmount = i.PaymentAllocations.Where(a => a.IsActive).Sum(a => a.Amount),
                PatientName = (i.Patient.FirstName + " " + i.Patient.LastName).Trim(),
                PatientNumber = i.Patient.PatientNumber,
                IssueDate = i.CreatedAt,
                i.CreatedAt
            })
            .ToListAsync();

        var invoices = invoicesRaw.Select(i => new
        {
            i.Id,
            i.InvoiceNumber,
            i.PatientId,
            Status = i.Status.ToString(),
            i.Subtotal,
            i.DiscountAmount,
            i.TotalAmount,
            PaidAmount = i.DirectPaidAmount + i.AdvanceAllocatedAmount,
            Balance = Math.Max(0m, i.TotalAmount - i.DirectPaidAmount - i.AdvanceAllocatedAmount),
            i.AdvanceAllocatedAmount,
            i.PatientName,
            i.PatientNumber,
            i.IssueDate,
            i.CreatedAt
        }).ToList();

        return Ok(new { data = invoices, total, page, pageSize });
    }

    // ─── Contracts List ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/contracts — List contracts with branch isolation for Finance V3.
    /// Supersedes the obsolete and removed GET /api/finance/overdue endpoint from PaymentsController.
    /// </summary>
    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? patientId = null,
        [FromQuery] string? status = null)
    {
        if (!await CanAsync("finance.contracts", "view")) return Deny();
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Where(c => c.IsActive);

        if (branchId.HasValue)
            query = query.Where(c => c.Patient.BranchId == branchId.Value);

        if (patientId.HasValue)
            query = query.Where(c => c.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, true, out var s))
            query = query.Where(c => c.Status == s);

        var total = await query.CountAsync();

        var today = ClinicTimeProvider.ClinicToday();

        var contractsRaw = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.PatientId,
                PatientName = (c.Patient.FirstName + " " + c.Patient.LastName).Trim(),
                PatientNumber = c.Patient.PatientNumber,
                ContractNumber = "CTR-" + c.Id.ToString().Substring(0, 8).ToUpper(),
                c.Specialty,
                c.TotalAmount,
                c.DiscountAmount,
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
                OutstandingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
                c.Status,
                StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd") : (string?)null,
                // QA-597: carry the raw fields needed for overdue calculation (can't do it in SQL)
                DownPayment = c.DownPayment,
                InstallmentsCount = c.InstallmentsCount,
                InstallmentAmount = c.InstallmentAmount,
                RawStartDate = c.StartDate,
                RawStatus = c.Status,
                PaidAmountRaw = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)
            })
            .ToListAsync();

        // QA-597: compute IsOverdue dynamically (was hardcoded false).
        // Mirrors FinanceService.GetOverdueContractsAsync logic: a contract is
        // overdue when the expected paid (down payment + elapsed installments)
        // exceeds the actual paid amount. Only active installment contracts qualify.
        // `today` is already declared above (line ~1778).
        var contracts = contractsRaw.Select(c =>
        {
            bool isOverdue = false;
            if (c.RawStatus == ContractStatus.Active
                && c.InstallmentAmount > 0
                && c.RawStartDate.HasValue)
            {
                var monthsElapsed = ((today.Year - c.RawStartDate.Value.Year) * 12) + (today.Month - c.RawStartDate.Value.Month);
                if (monthsElapsed > 0)
                {
                    var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0m));
                    isOverdue = expectedPaid - c.PaidAmountRaw > 0;
                }
            }
            return new
            {
                c.Id,
                c.PatientId,
                c.PatientName,
                c.PatientNumber,
                c.ContractNumber,
                c.Specialty,
                c.TotalAmount,
                c.DiscountAmount,
                c.PaidAmount,
                c.OutstandingAmount,
                Status = c.RawStatus.ToString(),
                c.StartDate,
                IsOverdue = isOverdue
            };
        }).ToList();

        return Ok(new { data = contracts, total, page, pageSize });
    }

    // ─── Supplier Bills List ────────────────────────────────────────────────
    // NOTE: GET /api/finance-v3/suppliers has been moved to FinanceV3SuppliersController

    /// <summary>
    /// GET /api/finance-v3/supplier-bills — List supplier bills with branch isolation for Finance V3.
    /// </summary>
    [HttpGet("supplier-bills")]
    public async Task<IActionResult> GetSupplierBills(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null)
    {
        if (!await CanAsync("finance.expenses", "view")) return Deny();
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.SupplierBills
            .Include(b => b.Supplier)
            .Where(b => b.IsActive);

        if (branchId.HasValue)
            query = query.Where(b => b.BranchId == branchId.Value);

        if (supplierId.HasValue)
            query = query.Where(b => b.SupplierId == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BillStatus>(status, true, out var s))
            query = query.Where(b => b.Status == s);

        var total = await query.CountAsync();

        var billsRaw = await query
            .OrderByDescending(b => b.BillDate)
            .ThenByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.SupplierId,
                SupplierName = b.Supplier != null ? b.Supplier.Name : "",
                b.Description,
                b.TotalAmount,
                b.PaidAmount,
                Balance = b.TotalAmount - b.PaidAmount,
                DueDate = b.DueDate.HasValue ? b.DueDate.Value.ToString("yyyy-MM-dd") : (string?)null,
                b.Status,
                b.CreatedAt
            })
            .ToListAsync();

        var bills = billsRaw.Select(b => new
        {
            b.Id,
            b.SupplierId,
            b.SupplierName,
            b.Description,
            b.TotalAmount,
            b.PaidAmount,
            b.Balance,
            b.DueDate,
            Status = b.Status.ToString(),
            b.CreatedAt
        }).ToList();

        return Ok(new { data = bills, total, page, pageSize });
    }

    // ─── Vault Transfers List ───────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/vault-transfers — List vault transfers with branch isolation for Finance V3.
    /// </summary>
    [HttpGet("vault-transfers")]
    public async Task<IActionResult> GetVaultTransfers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        if (!await CanAsync("finance.treasuries", "view")) return Deny();
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.VaultTransfers
            .Include(t => t.SourceTreasury)
            .Include(t => t.DestinationTreasury)
            .Include(t => t.PerformedByUser)
            .Include(t => t.ApprovedByUser)
            .Where(t => t.IsActive);

        if (branchId.HasValue)
            query = query.Where(t => t.DestinationTreasury.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransferStatus>(status, true, out var s))
            query = query.Where(t => t.Status == s);

        var total = await query.CountAsync();

        var transfersRaw = await query
            .OrderByDescending(t => t.TransferDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                SourceTreasuryId = t.SourceTreasuryId,
                SourceTreasuryName = t.SourceTreasury != null ? t.SourceTreasury.Name : "إيداع خارجي",
                DestinationTreasuryId = t.DestinationTreasuryId,
                DestinationTreasuryName = t.DestinationTreasury != null ? t.DestinationTreasury.Name : "",
                t.Amount,
                DepositSource = t.DepositSource,
                t.Status,
                RequestedBy = t.PerformedByUser != null ? t.PerformedByUser.Username : "",
                RequestedAt = t.TransferDate,
                ApprovedBy = t.ApprovedByUser != null ? t.ApprovedByUser.Username : null,
                ApprovedAt = t.ApprovalDate,
                RejectedBy = (string?)null,
                RejectedAt = (DateTime?)null,
                RejectionReason = (string?)null
            })
            .ToListAsync();

        var transfers = transfersRaw.Select(t => new
        {
            t.Id,
            t.SourceTreasuryId,
            t.SourceTreasuryName,
            t.DestinationTreasuryId,
            t.DestinationTreasuryName,
            t.Amount,
            t.DepositSource,
            Status = t.Status.ToString(),
            t.RequestedBy,
            t.RequestedAt,
            t.ApprovedBy,
            t.ApprovedAt,
            t.RejectedBy,
            t.RejectedAt,
            t.RejectionReason
        }).ToList();

        return Ok(new { data = transfers, total, page, pageSize });
    }

    // ─── Expenses List ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/expenses — List operational expenses with branch isolation for Finance V3.
    /// Migration C: TreasuryId and TreasuryName now read from JournalLine (Treasury account type)
    /// instead of CashFlowTransaction. The Treasury JournalLine in the same JournalEntry
    /// identifies which treasury the expense was paid from.
    /// </summary>
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? approvalStatus = null)
    {
        if (!await CanAsync("finance.expenses", "view")) return Deny();
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.OperationalExpenses
            .Include(e => e.Supplier)
            .Where(e => e.IsActive);

        if (branchId.HasValue)
            query = query.Where(e => e.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ExpenseCategory>(category, true, out var catFilter))
            query = query.Where(e => e.Category == catFilter);

        if (!string.IsNullOrWhiteSpace(approvalStatus) && Enum.TryParse<ApprovalStatus>(approvalStatus, true, out var statusFilter))
            query = query.Where(e => e.ApprovalStatus == statusFilter);

        var total = await query.CountAsync();

        // Migration C: Load expense IDs first, then resolve Treasury info from JournalLine
        var expensePageRaw = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                Title = e.Title,
                e.Category,
                e.Amount,
                e.PaymentMethod,
                ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                e.ApprovalStatus,
                RequestedBy = e.PaidBy.ToString(),
                ApprovedBy = e.ApprovedById,
                ApprovedAt = e.ApprovedAt,
                RejectedBy = (Guid?)null,
                RejectedAt = (DateTime?)null,
                RejectionReason = e.ApprovalNotes,
                IsReversal = db.JournalEntries.Any(je => je.FinancialDocumentId == e.Id
                    && je.FinancialDocumentType == FinancialDocumentType.Expense
                    && je.IsReversal && je.IsPosted),
                JournalEntryId = db.JournalEntries
                    .Where(je => je.FinancialDocumentId == e.Id
                        && je.FinancialDocumentType == FinancialDocumentType.Expense
                        && !je.IsReversal && je.IsPosted)
                    .Select(je => (Guid?)je.Id)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var expensePage = expensePageRaw.Select(e => new
        {
            e.Id,
            e.Title,
            Category = e.Category.ToString(),
            e.Amount,
            e.PaymentMethod,
            e.ExpenseDate,
            Status = e.ApprovalStatus.ToString(),
            e.RequestedBy,
            e.ApprovedBy,
            e.ApprovedAt,
            e.RejectedBy,
            e.RejectedAt,
            e.RejectionReason,
            e.IsReversal,
            e.JournalEntryId
        }).ToList();

        // Resolve TreasuryId from JournalLine for each expense's JournalEntry
        var journalEntryIds = expensePage
            .Where(e => e.JournalEntryId.HasValue)
            .Select(e => e.JournalEntryId!.Value)
            .Distinct()
            .ToList();

        var treasuryLines = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && journalEntryIds.Contains(l.JournalEntryId)
                && l.JournalEntry.IsPosted)
            .Select(l => new { l.JournalEntryId, l.AccountId })
            .ToListAsync();

        var treasuryIds = treasuryLines.Select(l => l.AccountId).Distinct().ToList();
        var treasuryNames = await db.Treasuries
            .Where(t => treasuryIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        var expenses = expensePage.Select(e =>
        {
            // Note: Each JournalEntry should have exactly one Treasury line.
            // FirstOrDefault is used for safety; multiple Treasury lines in one entry would be unusual.
            var treasuryLine = e.JournalEntryId.HasValue
                ? treasuryLines.FirstOrDefault(l => l.JournalEntryId == e.JournalEntryId.Value)
                : null;
            var tId = treasuryLine?.AccountId;
            return new
            {
                e.Id,
                e.Title,
                e.Category,
                e.Amount,
                e.PaymentMethod,
                e.ExpenseDate,
                e.Status,
                e.RequestedBy,
                e.ApprovedBy,
                e.ApprovedAt,
                e.RejectedBy,
                e.RejectedAt,
                e.RejectionReason,
                e.IsReversal,
                TreasuryId = (Guid?)tId,
                TreasuryName = tId.HasValue && treasuryNames.ContainsKey(tId.Value)
                    ? treasuryNames[tId.Value] : (string?)null
            };
        }).ToList();

        return Ok(new { data = expenses, total, page, pageSize });
    }
}
