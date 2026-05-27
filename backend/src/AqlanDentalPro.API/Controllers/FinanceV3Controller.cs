using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Finance V3 API — Provides data endpoints for the Finance V3 Financial Center dashboard.
/// Access is restricted to Admin and Accountant roles only (ReportsAccess policy).
///
/// This controller reads from both CashFlowTransaction (transitional) and
/// JournalEntry + JournalLine (canonical) tables, supporting the transition period.
/// </summary>
[ApiController]
[Route("api/finance-v3")]
[Authorize(Policy = "ReportsAccess")]
public class FinanceV3Controller(
    AppDbContext db,
    ICurrentUserService currentUser,
    IFinanceService financeService,
    IAuditService audit,
    IJournalEntryService journalEntryService,
    ITreasuryResolutionService treasuryResolution,
    ILogger<FinanceV3Controller> logger) : ControllerBase
{
    // ─── Dashboard KPIs ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/dashboard — Returns KPI data for the Finance V3 dashboard header band.
    /// Uses CashFlowTransaction for real-time operational data and JournalEntry for canonical verification.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? period = "today")
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // ── Cash-based KPIs (from CashFlowTransaction) ──
        var todayInflowQuery = db.CashFlowTransactions
            .Where(t => t.TransactionDate == today && t.Type == TransactionType.Inflow);
        var todayOutflowQuery = db.CashFlowTransactions
            .Where(t => t.TransactionDate == today && t.Type == TransactionType.Outflow);
        var monthInflowQuery = db.CashFlowTransactions
            .Where(t => t.TransactionDate >= monthStart && t.Type == TransactionType.Inflow);
        var monthOutflowQuery = db.CashFlowTransactions
            .Where(t => t.TransactionDate >= monthStart && t.Type == TransactionType.Outflow);

        if (branchId.HasValue)
        {
            todayInflowQuery = todayInflowQuery.Where(t => t.BranchId == branchId.Value);
            todayOutflowQuery = todayOutflowQuery.Where(t => t.BranchId == branchId.Value);
            monthInflowQuery = monthInflowQuery.Where(t => t.BranchId == branchId.Value);
            monthOutflowQuery = monthOutflowQuery.Where(t => t.BranchId == branchId.Value);
        }

        var todayInflow = await todayInflowQuery.SumAsync(t => (decimal?)t.Amount) ?? 0;
        var todayOutflow = await todayOutflowQuery.SumAsync(t => (decimal?)t.Amount) ?? 0;
        var monthInflow = await monthInflowQuery.SumAsync(t => (decimal?)t.Amount) ?? 0;
        var monthOutflow = await monthOutflowQuery.SumAsync(t => (decimal?)t.Amount) ?? 0;

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
        var treasuryQuery = db.Treasuries.Where(t => t.IsActive);
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

        return Ok(new
        {
            // Cash Flow KPIs
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
                ? $"{(double)postedEntryCount / journalEntryCount * 100:F1}%"
                : "N/A",

            // Pending actions
            PendingExpenses = pendingExpenses,
            PendingTransfers = pendingTransfers,

            // Period info
            Date = today.ToString("yyyy-MM-dd"),
            Period = period
        });
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
        // Blocker 6: Reject non-admin with null/empty BranchId
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var entry = await db.JournalEntries
            .Include(e => e.Lines)
            .Include(e => e.Branch)
            .Include(e => e.Treasury)
            .Include(e => e.PerformedByUser)
            .Include(e => e.ReversalOfEntry)
            .Include(e => e.ReversedByEntry)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entry == null)
            return NotFound(new { message = "القيد غير موجود" });

        // Finance V3: Branch scope enforcement for non-admin users
        if (!currentUser.IsAdmin && currentUser.BranchId.HasValue && entry.BranchId != currentUser.BranchId.Value)
            return Forbid("ليس لديك صلاحية الوصول إلى قيود فرع آخر");

        return Ok(new
        {
            entry.Id,
            entry.EntryNumber,
            DocumentType = entry.FinancialDocumentType.ToString(),
            FinancialDocumentId = entry.FinancialDocumentId,
            entry.Description,
            EntryDate = entry.EntryDate.ToString("yyyy-MM-dd"),
            BranchName = entry.Branch?.Name ?? "",
            TreasuryName = entry.Treasury?.Name ?? "",
            PerformedByName = entry.PerformedByUser?.Username ?? "",
            entry.IsPosted,
            entry.IsReversal,
            ReversalOfEntryNumber = entry.ReversalOfEntry?.EntryNumber,
            ReversedByEntryNumber = entry.ReversedByEntry?.EntryNumber,
            CashierSessionId = entry.CashierSessionId,
            entry.CreatedAt,
            TotalDebit = entry.Lines.Sum(l => l.Debit),
            TotalCredit = entry.Lines.Sum(l => l.Credit),
            IsBalanced = entry.IsBalanced(),
            Lines = entry.Lines.Select(l => new
            {
                l.Id,
                AccountType = l.AccountType.ToString(),
                l.AccountId,
                l.Debit,
                l.Credit,
                l.Description
            }).ToList()
        });
    }

    // ─── Account Balances ────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/account-balances — Returns balances for all account types.
    /// Uses the JournalLine canonical table for balance calculations.
    /// </summary>
    [HttpGet("account-balances")]
    public async Task<IActionResult> GetAccountBalances()
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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
                TotalDebit = g.Sum(l => l.Debit),
                TotalCredit = g.Sum(l => l.Credit),
                NetBalance = g.Sum(l => l.Debit) - g.Sum(l => l.Credit), // Debit-normal balance
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
            TotalPayables = -(accountBalances.Find(a => a.AccountType == "Payable")?.NetBalance ?? 0)
        });
    }

    // ─── Daily Cash Summary ──────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/daily-cash-summary — Cash flow breakdown by category for a given date.
    /// </summary>
    [HttpGet("daily-cash-summary")]
    public async Task<IActionResult> GetDailyCashSummary([FromQuery] string? date = null)
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var targetDate = DateOnly.TryParse(date, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // FIX: Include reversal transactions in daily cash summary so net cash
        // is correctly calculated. Reversals of Inflow create Outflow entries
        // and vice versa, so their natural effect already nets correctly.
        // We do NOT filter out IsReversal rows (Blocker 5).
        var transactions = db.CashFlowTransactions
            .Where(t => t.TransactionDate == targetDate);
        if (branchId.HasValue)
            transactions = transactions.Where(t => t.BranchId == branchId.Value);

        var byCategory = await transactions
            .GroupBy(t => new { t.Type, t.Category, t.IsReversal })
            .Select(g => new
            {
                Type = g.Key.Type.ToString(),
                Category = g.Key.Category.ToString(),
                IsReversal = g.Key.IsReversal,
                Count = g.Count(),
                Total = g.Sum(t => t.Amount)
            })
            .OrderByDescending(g => g.Total)
            .ToListAsync();

        var byPaymentMethod = await transactions
            .GroupBy(t => t.PaymentMethod)
            .Select(g => new
            {
                PaymentMethod = g.Key,
                Count = g.Count(),
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync();

        // Net cash = Inflow total - Outflow total (reversals are naturally
        // typed as opposite direction, so they net correctly)
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
            JournalEntryCount = journalEntries
        });
    }

    // ─── Profit and Loss (Basic) ─────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/profit-loss — Basic P&L using the formulas from the Foundation Spec (Section 4.6).
    /// PRIMARY P&L numbers come from posted JournalLines (accrual basis).
    /// Cash-flow figures are labeled as Cash Collections, NOT Revenue.
    /// </summary>
    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitAndLoss(
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = DateOnly.FromDateTime(DateTime.Today);
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

        // ── Cash Collections (from CashFlowTransaction — previously mislabeled as Revenue) ──
        var revenuePayments = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Inflow
                && tx.Category == FinancialCategory.PatientPayment);
        var refundPayments = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.Refund);

        // Reversal outflows that reverse patient payments (deleted payment reversals)
        var patientPaymentReversals = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.Reversal
                && tx.IsReversal
                && tx.ReversalOfTransactionId != null
                && db.CashFlowTransactions
                    .Where(orig => orig.Category == FinancialCategory.PatientPayment)
                    .Select(orig => orig.Id)
                    .Contains(tx.ReversalOfTransactionId.Value));

        if (branchId.HasValue)
        {
            revenuePayments = revenuePayments.Where(tx => tx.BranchId == branchId.Value);
            refundPayments = refundPayments.Where(tx => tx.BranchId == branchId.Value);
            patientPaymentReversals = patientPaymentReversals.Where(tx => tx.BranchId == branchId.Value);
        }

        var cashCollections = await revenuePayments.SumAsync(tx => (decimal?)tx.Amount) ?? 0;
        var cashRefunds = await refundPayments.SumAsync(tx => (decimal?)tx.Amount) ?? 0;
        var patientReversalTotal = await patientPaymentReversals.SumAsync(tx => (decimal?)tx.Amount) ?? 0;
        var netCashCollections = cashCollections - cashRefunds - patientReversalTotal;

        // ── Cost categories: original outflows minus category-specific reversal inflows ──
        // Blocker 2: Each outgoing category must net its own reversals.
        // A reversal of OperationalExpense reduces OperatingExpenses ONLY.
        // A reversal of SalaryPayment reduces SalaryPayments ONLY, etc.
        // Transfer reversals must not affect operating costs.
        // Patient payment reversals must not affect cost categories.

        // Operating Expenses: original outflows - reversal inflows for OperationalExpense
        var expenses = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.OperationalExpense
                && !tx.IsReversal);
        var expenseReversals = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Inflow
                && tx.Category == FinancialCategory.Reversal
                && tx.IsReversal
                && tx.ReversalOfTransactionId != null
                && db.CashFlowTransactions
                    .Where(orig => orig.Category == FinancialCategory.OperationalExpense)
                    .Select(orig => orig.Id)
                    .Contains(tx.ReversalOfTransactionId.Value));
        if (branchId.HasValue)
        {
            expenses = expenses.Where(tx => tx.BranchId == branchId.Value);
            expenseReversals = expenseReversals.Where(tx => tx.BranchId == branchId.Value);
        }
        var operatingExpenses = (await expenses.SumAsync(tx => (decimal?)tx.Amount) ?? 0)
                              - (await expenseReversals.SumAsync(tx => (decimal?)tx.Amount) ?? 0);

        // Salary Payments: original outflows - reversal inflows for SalaryPayment
        var salaries = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.SalaryPayment
                && !tx.IsReversal);
        var salaryReversals = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Inflow
                && tx.Category == FinancialCategory.Reversal
                && tx.IsReversal
                && tx.ReversalOfTransactionId != null
                && db.CashFlowTransactions
                    .Where(orig => orig.Category == FinancialCategory.SalaryPayment)
                    .Select(orig => orig.Id)
                    .Contains(tx.ReversalOfTransactionId.Value));
        if (branchId.HasValue)
        {
            salaries = salaries.Where(tx => tx.BranchId == branchId.Value);
            salaryReversals = salaryReversals.Where(tx => tx.BranchId == branchId.Value);
        }
        var salaryTotal = (await salaries.SumAsync(tx => (decimal?)tx.Amount) ?? 0)
                        - (await salaryReversals.SumAsync(tx => (decimal?)tx.Amount) ?? 0);

        // Doctor Commissions: original outflows - reversal inflows for DoctorCommission
        var commissions = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.DoctorCommission
                && !tx.IsReversal);
        var commissionReversals = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Inflow
                && tx.Category == FinancialCategory.Reversal
                && tx.IsReversal
                && tx.ReversalOfTransactionId != null
                && db.CashFlowTransactions
                    .Where(orig => orig.Category == FinancialCategory.DoctorCommission)
                    .Select(orig => orig.Id)
                    .Contains(tx.ReversalOfTransactionId.Value));
        if (branchId.HasValue)
        {
            commissions = commissions.Where(tx => tx.BranchId == branchId.Value);
            commissionReversals = commissionReversals.Where(tx => tx.BranchId == branchId.Value);
        }
        var commissionTotal = (await commissions.SumAsync(tx => (decimal?)tx.Amount) ?? 0)
                            - (await commissionReversals.SumAsync(tx => (decimal?)tx.Amount) ?? 0);

        // Supplier Payments: original outflows - reversal inflows for SupplierPayment
        var supplierPayments = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.SupplierPayment
                && !tx.IsReversal);
        var supplierReversals = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Inflow
                && tx.Category == FinancialCategory.Reversal
                && tx.IsReversal
                && tx.ReversalOfTransactionId != null
                && db.CashFlowTransactions
                    .Where(orig => orig.Category == FinancialCategory.SupplierPayment)
                    .Select(orig => orig.Id)
                    .Contains(tx.ReversalOfTransactionId.Value));
        if (branchId.HasValue)
        {
            supplierPayments = supplierPayments.Where(tx => tx.BranchId == branchId.Value);
            supplierReversals = supplierReversals.Where(tx => tx.BranchId == branchId.Value);
        }
        var supplierTotal = (await supplierPayments.SumAsync(tx => (decimal?)tx.Amount) ?? 0)
                          - (await supplierReversals.SumAsync(tx => (decimal?)tx.Amount) ?? 0);

        var totalCosts = operatingExpenses + salaryTotal + commissionTotal + supplierTotal;
        var cashNetProfit = netCashCollections - totalCosts;

        return Ok(new
        {
            Period = new { From = from.ToString("yyyy-MM-dd"), To = to.ToString("yyyy-MM-dd") },

            // Accrued P&L (from posted JournalLines — accrual basis)
            // NOTE: Accrued figures only include amounts where a JournalEntry was posted.
            // Expense/Salary/Commission/Supplier JE posting was added in this PR;
            // historical records may only have CashFlowTransaction entries.
            AccruedRevenue = accruedRevenue,
            AccruedExpenses = accruedExpenses,
            AccruedNetProfit = accruedNetProfit,

            // Cash-flow figures (from CashFlowTransaction)
            CashCollections = cashCollections,
            CashRefunds = cashRefunds,
            PatientPaymentReversals = patientReversalTotal,
            NetCashCollections = netCashCollections,
            NetCashCollectionsFormula = "PatientPayment Inflows - Refund Outflows - PatientPayment Reversal Outflows",
            OperatingExpenses = operatingExpenses,
            SalaryPayments = salaryTotal,
            DoctorCommissions = commissionTotal,
            SupplierPayments = supplierTotal,
            OperatingExpensesFormula = "OperationalExpense Outflows - Reversal Inflows of OperationalExpense",
            SalaryPaymentsFormula = "SalaryPayment Outflows - Reversal Inflows of SalaryPayment",
            DoctorCommissionsFormula = "DoctorCommission Outflows - Reversal Inflows of DoctorCommission",
            SupplierPaymentsFormula = "SupplierPayment Outflows - Reversal Inflows of SupplierPayment",
            TotalCosts = totalCosts,
            CashNetProfit = cashNetProfit,
            ProfitMargin = netCashCollections > 0 ? (double)(cashNetProfit / netCashCollections * 100) : 0,

            // Reversal coverage status — which write paths have actual correction endpoints
            ReversalCoverage = new
            {
                OperationalExpenseReversal = "Implemented — DELETE /api/expenses/{id} creates CashFlow + JournalEntry reversal",
                SalaryPaymentReversal = "Implemented — PUT /api/salaries/{id}/reverse creates CashFlow + JournalEntry reversal",
                CommissionPaymentReversal = "Deferred — no standalone reversal endpoint yet; commission payments cannot be reversed via API",
                SupplierPaymentReversal = "Deferred — no standalone reversal endpoint yet; supplier payments cannot be reversed via API",
                InvoiceCancellationReversal = "Implemented — cancel creates CashFlow + JournalEntry reversal via FinanceService"
            },

            // Summary counts
            RevenueTransactionCount = await revenuePayments.CountAsync(),
            ExpenseTransactionCount = await expenses.CountAsync()
        });
    }

    // ─── Patient Balance ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/patient-balance/{patientId} — Patient financial balance per Section 4.1.
    /// Balance = Total Invoiced - Total Paid - Total Discounts
    /// </summary>
    [HttpGet("patient-balance/{patientId:guid}")]
    public async Task<IActionResult> GetPatientBalance(Guid patientId)
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null)
            return NotFound(new { message = "المريض غير موجود" });

        // Blocker 6: Branch filter for non-admin users
        if (!currentUser.IsAdmin && patient.BranchId != currentUser.BranchId)
            return Forbid("ليس لديك صلاحية الوصول إلى بيانات مريض من فرع آخر");

        // Total Invoiced = SUM(Invoice.TotalAmount) WHERE Status IN (Issued, Paid)
        var totalInvoiced = await db.Invoices
            .Where(i => i.PatientId == patientId && i.IsActive
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid))
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        // Total Paid = SUM(Payment.Amount) WHERE NOT reversed
        var totalPaid = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive && p.Amount > 0)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        // Total Refunds
        var totalRefunds = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive && p.Amount < 0)
            .SumAsync(p => (decimal?)p.Amount) ?? 0; // negative values

        // Total Discounts from contracts
        var totalDiscounts = await db.Contracts
            .Where(c => c.PatientId == patientId && c.IsActive)
            .SumAsync(c => (decimal?)c.DiscountAmount) ?? 0;

        var netPaid = totalPaid + totalRefunds; // refunds are negative
        var balance = totalInvoiced - netPaid - totalDiscounts;

        // Contract outstanding
        var contractOutstanding = await db.Contracts
            .Where(c => c.PatientId == patientId && c.Status == ContractStatus.Active && c.IsActive)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount))
            .SumAsync() ;

        return Ok(new
        {
            PatientId = patientId,
            PatientName = (patient.FirstName + " " + patient.LastName).Trim(),
            PatientNumber = patient.PatientNumber,
            TotalInvoiced = totalInvoiced,
            TotalPaid = totalPaid,
            TotalRefunds = Math.Abs(totalRefunds),
            NetPaid = netPaid,
            TotalDiscounts = totalDiscounts,
            Balance = balance,
            ContractOutstanding = contractOutstanding,
            HasOutstanding = balance > 0
        });
    }

    // ─── Treasury Detail ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/treasuries — Treasury accounts with recent transactions.
    /// </summary>
    [HttpGet("treasuries")]
    public async Task<IActionResult> GetTreasuries()
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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
    /// Uses the existing AuditLog table filtered to finance-related resources.
    /// </summary>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? resource = null,
        [FromQuery] string? action = null)
    {
        // Blocker 6: Audit endpoint restricted to Admin only
        // Financial audit trail contains cross-branch sensitive data;
        // non-admin Accountant users should not access other branches' audit records.
        if (!currentUser.IsAdmin)
            return Forbid("الاطلاع على سجل المراجعة متاح للمسؤول فقط.");

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Filter to finance-related audit entries only
        var financeResources = new[] { "Payment", "OperationalExpense", "SalaryRecord",
            "AdvancePayment", "VaultTransfer", "SupplierBill", "SupplierBillPayment",
            "DoctorCommissionPayment", "Treasury", "JournalEntry", "CashFlowTransaction" };

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

        return Ok(new { data = entries, total, page, pageSize });
    }

    // ─── Patient Accounts (Sub-ledger) ─────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/patient-accounts — Paginated list of patients with outstanding balances.
    /// </summary>
    [HttpGet("patient-accounts")]
    public async Task<IActionResult> GetPatientAccounts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Patients
            .Where(p => p.IsActive)
            .Where(p => !branchId.HasValue || p.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.FirstName.Contains(search) || p.LastName.Contains(search) || p.PatientNumber.Contains(search) || (p.Phone != null && p.Phone.Contains(search)));

        var total = await query.CountAsync();

        var patients = await query
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
                TotalPaid = db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive && pay.Amount > 0).Sum(pay => (decimal?)pay.Amount) ?? 0,
                TotalRefunds = db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive && pay.Amount < 0).Sum(pay => (decimal?)Math.Abs(pay.Amount)) ?? 0,
                Balance = (db.Invoices.Where(i => i.PatientId == p.Id && i.IsActive && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid)).Sum(i => (decimal?)i.TotalAmount) ?? 0)
                         - (db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive).Sum(pay => (decimal?)pay.Amount) ?? 0),
                OutstandingInvoices = db.Invoices.Count(i => i.PatientId == p.Id && i.IsActive && i.Status == InvoiceStatus.Issued),
                ActiveContracts = db.Contracts.Count(c => c.PatientId == p.Id && c.IsActive && c.Status == ContractStatus.Active),
                HasOutstanding = ((db.Invoices.Where(i => i.PatientId == p.Id && i.IsActive && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid)).Sum(i => (decimal?)i.TotalAmount) ?? 0)
                               - (db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive).Sum(pay => (decimal?)pay.Amount) ?? 0)) > 0
            })
            .ToListAsync();

        return Ok(new { data = patients, total, page, pageSize });
    }

    // ─── Trial Balance ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/trial-balance — Trial balance from posted JournalLines.
    /// </summary>
    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] string? asOfDate = null)
    {
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var cutoff = DateOnly.TryParse(asOfDate, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);

        var linesQuery = db.JournalLines
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate <= cutoff)
            .Where(l => !branchId.HasValue || l.BranchId == branchId.Value);

        var accounts = await linesQuery
            .GroupBy(l => l.AccountType)
            .Select(g => new
            {
                AccountType = g.Key.ToString(),
                TotalDebit = g.Sum(l => l.Debit),
                TotalCredit = g.Sum(l => l.Credit),
                NetBalance = g.Sum(l => l.Debit) - g.Sum(l => l.Credit),
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

    /// <summary>
    /// GET /api/finance-v3/active-cashier-session — Current user's active cashier session with computed expected values.
    /// </summary>
    [HttpGet("active-cashier-session")]
    public async Task<IActionResult> GetActiveCashierSession()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var cashierId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty;

        var session = await db.CashierSessions
            .Include(s => s.Treasury)
            .FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return Ok(new { hasActiveSession = false });

        // Calculate expected values from CashFlowTransactions
        var sessionTransactions = await db.CashFlowTransactions
            .Where(t => t.CashierSessionId == session.Id && !t.IsReversal)
            .ToListAsync();

        var cashInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && t.PaymentMethod == "cash").Sum(t => t.Amount);
        var cashOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && t.PaymentMethod == "cash").Sum(t => t.Amount);
        var cardInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && t.PaymentMethod == "card").Sum(t => t.Amount);
        var cardOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && t.PaymentMethod == "card").Sum(t => t.Amount);

        return Ok(new
        {
            hasActiveSession = true,
            session.Id,
            session.SessionNumber,
            session.CashierId,
            session.BranchId,
            OpeningTime = session.OpeningTime.ToString("yyyy-MM-dd HH:mm"),
            session.OpeningBalance,
            session.TreasuryId,
            TreasuryName = session.Treasury?.Name ?? "",
            Status = session.Status.ToString(),
            ExpectedCash = session.OpeningBalance + cashInflows - cashOutflows,
            ExpectedCard = cardInflows - cardOutflows,
            TransactionCount = sessionTransactions.Count
        });
    }

    // ─── Payments List ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/payments — Paginated payments with method and date range filtering.
    /// </summary>
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? method = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Amount,
                PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd"),
                p.PaymentMethod,
                Specialty = p.Doctor != null ? p.Doctor.Specialty : null,
                ServiceDescription = p.Notes,
                p.ReceiptNumber,
                PatientName = (p.Patient.FirstName + " " + p.Patient.LastName).Trim(),
                PatientNumber = p.Patient.PatientNumber,
                DoctorName = p.Doctor != null ? p.Doctor.Name : null,
                p.Notes,
                p.CreatedAt
            })
            .ToListAsync();

        return Ok(new { data = payments, total, page, pageSize });
    }

    // ─── Invoices List ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/invoices — Paginated invoices with status filtering.
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
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

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.PatientId,
                Status = i.Status.ToString(),
                i.Subtotal,
                i.DiscountAmount,
                i.TotalAmount,
                PaidAmount = i.Payments.Where(p => p.IsActive && p.Amount > 0).Sum(p => p.Amount),
                Balance = i.TotalAmount - i.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
                PatientName = (i.Patient.FirstName + " " + i.Patient.LastName).Trim(),
                PatientNumber = i.Patient.PatientNumber,
                IssueDate = i.CreatedAt,
                i.CreatedAt
            })
            .ToListAsync();

        return Ok(new { data = invoices, total, page, pageSize });
    }

    // ─── Contracts List ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/contracts — List contracts with branch isolation for Finance V3.
    /// </summary>
    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? patientId = null,
        [FromQuery] string? status = null)
    {
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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

        var today = DateOnly.FromDateTime(DateTime.Today);

        var contracts = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.PatientId,
                PatientName = (c.Patient.FirstName + " " + c.Patient.LastName).Trim(),
                PatientNumber = c.Patient.PatientNumber,
                c.TotalAmount,
                c.DiscountAmount,
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
                OutstandingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
                Status = c.Status.ToString(),
                StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd") : (string?)null,
                IsOverdue = false
            })
            .ToListAsync();

        return Ok(new { data = contracts, total, page, pageSize });
    }

    // ─── Suppliers List ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/suppliers — List suppliers with balance info for Finance V3.
    /// </summary>
    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 30;

        var query = db.Suppliers.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) ||
                                     (s.ContactPerson != null && s.ContactPerson.Contains(search)) ||
                                     (s.Phone != null && s.Phone.Contains(search)));

        var total = await query.CountAsync();

        var suppliers = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.ContactPerson,
                s.Phone,
                TotalBilled = db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.TotalAmount) ?? 0,
                TotalPaid = db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.PaidAmount) ?? 0,
                Balance = (db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.TotalAmount) ?? 0)
                         - (db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.PaidAmount) ?? 0)
            })
            .ToListAsync();

        return Ok(new { data = suppliers, total, page, pageSize });
    }

    // ─── Supplier Bills List ────────────────────────────────────────────────

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
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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

        var bills = await query
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
                Status = b.Status.ToString(),
                b.CreatedAt
            })
            .ToListAsync();

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
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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

        var transfers = await query
            .OrderByDescending(t => t.TransferDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                SourceTreasuryId = t.SourceTreasuryId,
                SourceTreasuryName = t.SourceTreasury != null ? t.SourceTreasury.Name : "إيداع خارجي",
                DestinationTreasuryId = t.DestinationTreasuryId,
                DestinationTreasuryName = t.DestinationTreasury.Name,
                t.Amount,
                DepositSource = t.DepositSource,
                Status = t.Status.ToString(),
                RequestedBy = t.PerformedByUser.Username,
                RequestedAt = t.TransferDate,
                ApprovedBy = t.ApprovedByUser != null ? t.ApprovedByUser.Username : null,
                ApprovedAt = t.ApprovalDate,
                RejectedBy = (string?)null,
                RejectedAt = (DateTime?)null,
                RejectionReason = (string?)null
            })
            .ToListAsync();

        return Ok(new { data = transfers, total, page, pageSize });
    }

    // ─── Expenses List ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/expenses — List operational expenses with branch isolation for Finance V3.
    /// </summary>
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? approvalStatus = null)
    {
        // Branch isolation guard
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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

        // Determine reversal status by checking if a reversal CashFlowTransaction exists
        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                Title = e.Title,
                Category = e.Category.ToString(),
                e.Amount,
                e.PaymentMethod,
                ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                Status = e.ApprovalStatus.ToString(),
                RequestedBy = e.PaidBy.ToString(),
                ApprovedBy = e.ApprovedById,
                ApprovedAt = e.ApprovedAt,
                RejectedBy = (Guid?)null,
                RejectedAt = (DateTime?)null,
                RejectionReason = e.ApprovalNotes,
                IsReversal = false,
                TreasuryId = e.CashFlowTransactionId.HasValue
                    ? db.CashFlowTransactions.Where(c => c.Id == e.CashFlowTransactionId.Value).Select(c => c.TreasuryId).FirstOrDefault()
                    : (Guid?)null,
                TreasuryName = e.CashFlowTransactionId.HasValue
                    ? db.CashFlowTransactions.Where(c => c.Id == e.CashFlowTransactionId.Value)
                        .Select(c => c.Treasury.Name).FirstOrDefault()
                    : (string?)null
            })
            .ToListAsync();

        return Ok(new { data = expenses, total, page, pageSize });
    }

    // ─── Cashier Sessions Active (Finance V3) ──────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/cashier-sessions/active — Get the active cashier session for the current user.
    /// Returns the session with the proper shape expected by the Finance V3 frontend.
    /// </summary>
    [HttpGet("cashier-sessions/active")]
    public async Task<IActionResult> GetActiveCashierSessionV3()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var cashierId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty;

        var session = await db.CashierSessions
            .Include(s => s.Cashier)
            .Include(s => s.Treasury)
            .FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return Ok(new { hasActiveSession = false });

        // Calculate expected values from CashFlowTransactions for the session
        var sessionTransactions = await db.CashFlowTransactions
            .Where(t => t.CashierSessionId == session.Id && t.IsActive)
            .ToListAsync();

        var cashInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && string.Equals(t.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var cashOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && string.Equals(t.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var cardInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && string.Equals(t.PaymentMethod, "card", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var cardOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && string.Equals(t.PaymentMethod, "card", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var bankInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && (string.Equals(t.PaymentMethod, "bank_transfer", StringComparison.OrdinalIgnoreCase) || string.Equals(t.PaymentMethod, "bank", StringComparison.OrdinalIgnoreCase))).Sum(t => t.Amount);
        var bankOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && (string.Equals(t.PaymentMethod, "bank_transfer", StringComparison.OrdinalIgnoreCase) || string.Equals(t.PaymentMethod, "bank", StringComparison.OrdinalIgnoreCase))).Sum(t => t.Amount);

        var totalCollections = sessionTransactions.Where(t => t.Type == TransactionType.Inflow).Sum(t => t.Amount);

        return Ok(new
        {
            hasActiveSession = true,
            session.Id,
            CashierId = session.CashierId,
            CashierName = session.Cashier?.Username ?? "",
            session.BranchId,
            OpenedAt = session.OpeningTime,
            session.ClosingTime,
            session.OpeningBalance,
            ExpectedClosingCash = session.OpeningBalance + cashInflows - cashOutflows,
            ExpectedClosingCard = cardInflows - cardOutflows,
            ExpectedClosingBank = bankInflows - bankOutflows,
            ActualClosingCash = (decimal?)session.ActualClosingCash,
            ActualClosingCard = (decimal?)session.ActualClosingCard,
            ActualClosingBank = (decimal?)session.ActualClosingBank,
            ShortageOrSurplus = (decimal?)session.ShortageOrSurplus,
            Status = session.Status.ToString(),
            session.Notes,
            session.TreasuryId,
            TotalCollections = totalCollections
        });
    }

    // ─── Write Endpoints (Finance V3) ─────────────────────────────────────

    /// <summary>
    /// POST /api/finance-v3/payments — Register a payment.
    /// Delegates to FinanceService.CreatePaymentAsync (same logic as PaymentsController).
    /// </summary>
    [HttpPost("payments")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest req)
    {
        // Amount validation: reject zero or negative amounts
        if (req.Amount <= 0)
            return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });

        // Overpayment guard: reject payment exceeding outstanding balance
        if (req.InvoiceId.HasValue)
        {
            var invoice = await db.Invoices
                .Where(i => i.Id == req.InvoiceId.Value && i.IsActive)
                .Select(i => new { i.TotalAmount, PaidAmount = i.Payments.Where(p => p.IsActive).Sum(p => p.Amount) })
                .FirstOrDefaultAsync();
            if (invoice != null)
            {
                var outstanding = invoice.TotalAmount - invoice.PaidAmount;
                if (outstanding > 0 && req.Amount > outstanding)
                    return BadRequest(new { message = $"المبلغ يتجاوز الرصيد المتبقي ({outstanding:N0} ر.ي)" });
            }
        }
        else if (req.ContractId.HasValue)
        {
            var contract = await db.Contracts
                .Where(c => c.Id == req.ContractId.Value && c.IsActive)
                .Select(c => new { c.TotalAmount, c.DiscountAmount, PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount) })
                .FirstOrDefaultAsync();
            if (contract != null)
            {
                var outstanding = contract.TotalAmount - contract.DiscountAmount - contract.PaidAmount;
                if (outstanding > 0 && req.Amount > outstanding)
                    return BadRequest(new { message = $"المبلغ يتجاوز الرصيد المتبقي ({outstanding:N0} ر.ي)" });
            }
        }

        try
        {
            var result = await financeService.CreatePaymentAsync(req);
            await audit.LogAsync(AuditAction.Create, "Payment", result.Id,
                newData: new { result.Amount, result.PatientId, result.PaymentMethod });
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Payment creation validation failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/finance-v3/payments/{id} — Delete a payment (Admin only).
    /// Delegates to FinanceService.DeletePaymentAsync (same logic as PaymentsController).
    /// </summary>
    [HttpDelete("payments/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        var payment = await financeService.GetPaymentByIdAsync(id);
        var deleted = await financeService.DeletePaymentAsync(id);
        if (deleted && payment != null)
        {
            await audit.LogAsync(AuditAction.Delete, "Payment", id,
                oldData: new { payment.Amount, payment.PatientId });
        }
        return deleted ? Ok(new { message = "تم حذف الدفعة بنجاح" }) : NotFound(new { message = "الدفعة غير موجودة" });
    }

    /// <summary>
    /// PATCH /api/finance-v3/invoices/{id}/cancel — Cancel an invoice.
    /// Reuses the same logic from InvoicesController.Cancel.
    /// </summary>
    [HttpPatch("invoices/{id:guid}/cancel")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CancelInvoice(Guid id, [FromBody] CancelInvoiceRequest? req = null)
    {
        var invoice = await db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });
        if (!invoice.IsActive)
            return BadRequest(new { message = "الفاتورة محذوفة" });

        var originalStatus = invoice.Status;

        if (originalStatus == InvoiceStatus.Paid)
            return BadRequest(new { message = "لا يمكن إلغاء فاتورة مدفوعة. يجب استرداد المدفوعات أولاً." });
        if (originalStatus == InvoiceStatus.Cancelled)
            return BadRequest(new { message = "الفاتورة ملغاة بالفعل" });

        if (originalStatus == InvoiceStatus.Issued)
        {
            var hasActivePayments = invoice.Payments.Any(p => p.IsActive);
            if (hasActivePayments)
                return BadRequest(new { message = "لا يمكن إلغاء فاتورة مصدرة بها مدفوعات نشطة. يجب استرداد أو حذف المدفوعات أولاً." });
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedBy = userId;

        if (req?.Notes != null)
            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? $"[إلغاء] {req.Notes}"
                : $"{invoice.Notes}\n[إلغاء] {req.Notes}";

        if (originalStatus == InvoiceStatus.Issued)
        {
            var existingReversal = await db.JournalEntries
                .AnyAsync(e => e.FinancialDocumentId == invoice.Id
                    && e.FinancialDocumentType == FinancialDocumentType.Invoice
                    && e.IsReversal);

            if (!existingReversal)
            {
                var useCancelTx = db.Database.IsRelational();
                var cancelTx = useCancelTx ? await db.Database.BeginTransactionAsync() : null;
                try
                {
                    await financeService.ReverseInvoiceIssuedEntryAsync(invoice.Id);
                    await db.SaveChangesAsync();
                    if (useCancelTx) await cancelTx!.CommitAsync();
                }
                catch
                {
                    if (useCancelTx) await cancelTx!.RollbackAsync();
                    await db.Entry(invoice).ReloadAsync();
                    throw;
                }
            }
            else
            {
                await db.SaveChangesAsync();
            }
        }
        else
        {
            await db.SaveChangesAsync();
        }

        await audit.LogAsync(AuditAction.Update, "Invoice", id, details: "Invoice cancelled via Finance V3");
        return Ok(new { message = "تم إلغاء الفاتورة بنجاح", invoice.Id, Status = invoice.Status.ToString() });
    }

    /// <summary>
    /// POST /api/finance-v3/cashier-sessions/close — Close the active cashier session.
    /// Reuses the same logic from CashierSessionsController.CloseSession.
    /// </summary>
    [HttpPost("cashier-sessions/close")]
    [Authorize(Policy = "CashierAccess")]
    public async Task<IActionResult> CloseCashierSession([FromBody] CloseSessionRequest req)
    {
        // Amount validation: reject negative actual closing values
        if (req.ActualClosingCash < 0)
            return BadRequest(new { message = "النقدي الفعلي لا يمكن أن يكون سالباً" });
        if (req.ActualClosingCard < 0)
            return BadRequest(new { message = "البطاقة الفعلية لا يمكن أن تكون سالبة" });
        if (req.ActualClosingBank < 0)
            return BadRequest(new { message = "البنكي الفعلي لا يمكن أن يكون سالباً" });

        var userId = currentUser.UserId ?? Guid.Empty;
        var session = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return BadRequest(new { message = "لا يوجد صندوق مفتوح حالياً لإقفاله." });

        var sessionTransactions = await db.CashFlowTransactions
            .Where(t => t.CashierSessionId == session.Id && t.IsActive)
            .ToListAsync();

        var cashInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cashOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cardInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cardOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var bankInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var bankOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);

        session.ExpectedClosingCash = session.OpeningBalance + cashInflows - cashOutflows;
        session.ExpectedClosingCard = cardInflows - cardOutflows;
        session.ExpectedClosingBank = bankInflows - bankOutflows;
        session.ActualClosingCash = req.ActualClosingCash;
        session.ActualClosingCard = req.ActualClosingCard;
        session.ActualClosingBank = req.ActualClosingBank;

        var expectedTotal = session.ExpectedClosingCash + session.ExpectedClosingCard + session.ExpectedClosingBank;
        var actualTotal = req.ActualClosingCash + req.ActualClosingCard + req.ActualClosingBank;
        session.ShortageOrSurplus = actualTotal - expectedTotal;
        session.ClosingTime = DateTime.UtcNow;
        session.Status = SessionStatus.Closed;
        session.Notes = req.Notes?.Trim();

        // Link any unlinked transactions
        var unlinkedTransactions = await db.CashFlowTransactions
            .Where(t => t.CashierSessionId == null && t.PerformedBy == userId && t.CreatedAt >= session.OpeningTime && t.IsActive)
            .ToListAsync();
        foreach (var t in unlinkedTransactions)
            t.CashierSessionId = session.Id;

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Update, "CashierSession", session.Id,
            details: $"Session closed via V3, surplus/shortage: {session.ShortageOrSurplus}");

        return Ok(new
        {
            session.Id,
            session.SessionNumber,
            session.OpeningTime,
            session.ClosingTime,
            session.OpeningBalance,
            session.ExpectedClosingCash,
            session.ActualClosingCash,
            session.ExpectedClosingCard,
            session.ActualClosingCard,
            session.ExpectedClosingBank,
            session.ActualClosingBank,
            session.ShortageOrSurplus,
            Status = session.Status.ToString(),
            message = "تم إقفال صندوق الاستقبال وترحيل المبالغ وتأمين القيود بنجاح"
        });
    }

    /// <summary>
    /// PATCH /api/finance-v3/cashier-sessions/{id}/reconcile — Reconcile a closed session.
    /// </summary>
    [HttpPatch("cashier-sessions/{id:guid}/reconcile")]
    [Authorize(Policy = "ReportsAccess")]
    public async Task<IActionResult> ReconcileCashierSession(Guid id, [FromBody] string? notes)
    {
        var session = await db.CashierSessions.FindAsync(id);
        if (session == null || !session.IsActive)
            return NotFound(new { message = "الوردية غير موجودة" });
        if (session.Status != SessionStatus.Closed)
            return BadRequest(new { message = "يمكن مطابقة الورديات المغلقة فقط" });

        session.Status = SessionStatus.Reconciled;
        if (!string.IsNullOrWhiteSpace(notes))
            session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                ? $"[مطابقة] {notes}" : $"{session.Notes}\n[مطابقة] {notes}";

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Update, "CashierSession", id, details: "Session reconciled via V3");

        return Ok(new { session.Id, session.SessionNumber, Status = session.Status.ToString(), message = "تمت المطابقة والاعتماد المحاسبي للوردية اليومية بنجاح" });
    }

    /// <summary>
    /// POST /api/finance-v3/treasuries — Create a treasury account (Admin only).
    /// Reuses logic from TreasuriesController.Create.
    /// </summary>
    [HttpPost("treasuries")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateTreasury([FromBody] CreateTreasuryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "اسم الخزنة/الحساب مطلوب" });
        if (!Enum.TryParse<TreasuryType>(req.Type, true, out var type))
            return BadRequest(new { message = "نوع الخزنة غير صالح. المتاح: Vault أو Bank" });
        if (req.OpeningBalance < 0)
            return BadRequest(new { message = "رصيد البداية لا يمكن أن يكون سالباً" });

        var branchId = currentUser.BranchId ?? Guid.Empty;
        var treasury = new Treasury
        {
            Name = req.Name.Trim(),
            Type = type,
            Balance = req.OpeningBalance,
            BranchId = branchId,
            IsActive = true
        };
        db.Treasuries.Add(treasury);

        if (req.OpeningBalance > 0)
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-IN-OP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.InternalTransfer,
                Amount = req.OpeningBalance,
                PaymentMethod = type == TreasuryType.Bank ? "bank" : "cash",
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                ReferenceId = treasury.Id,
                ReferenceNumber = "OP-BAL",
                Description = $"رصيد افتتاحي لبداية تشغيل {treasury.Name}",
                PerformedBy = currentUser.UserId ?? Guid.Empty,
                BranchId = branchId
            };
            db.CashFlowTransactions.Add(cashflow);
        }

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Create, "Treasury", treasury.Id);

        return Ok(new { treasury.Id, treasury.Name, Type = treasury.Type.ToString(), treasury.Balance, message = "تم إنشاء الخزنة/الحساب المالي بنجاح" });
    }

    /// <summary>
    /// POST /api/finance-v3/vault-transfers — Create a vault transfer.
    /// Reuses logic from VaultTransfersController.Create.
    /// </summary>
    [HttpPost("vault-transfers")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateVaultTransfer([FromBody] CreateTransferRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ التحويل أكبر من الصفر" });
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var branchId = currentUser.BranchId ?? Guid.Empty;
        var userId = currentUser.UserId ?? Guid.Empty;

        var destTreasury = await db.Treasuries.FirstOrDefaultAsync(t => t.Id == req.DestinationTreasuryId && t.BranchId == branchId && t.IsActive);
        if (destTreasury == null)
            return BadRequest(new { message = "الخزنة المستهدفة غير موجودة أو غير تابعة للفرع" });

        Treasury? sourceTreasury = null;
        if (req.SourceTreasuryId.HasValue)
        {
            sourceTreasury = await db.Treasuries.FirstOrDefaultAsync(t => t.Id == req.SourceTreasuryId.Value && t.BranchId == branchId && t.IsActive);
            if (sourceTreasury == null)
                return BadRequest(new { message = "الخزنة المصدر غير موجودة أو غير تابعة للفرع" });
            if (sourceTreasury.Balance < req.Amount)
                return BadRequest(new { message = $"عذراً، رصيد الخزنة المصدر ({sourceTreasury.Balance:N0} ر.ي) أقل من مبلغ التحويل المطلوب ({req.Amount:N0} ر.ي)" });
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = StableLockKeyHelper.VaultTransferNumber;
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"TR-{datePart}-";
            var lastTransfer = await db.VaultTransfers.IgnoreQueryFilters()
                .Where(t => t.TransferNumber.StartsWith(prefix))
                .OrderByDescending(t => t.TransferNumber).Select(t => t.TransferNumber).FirstOrDefaultAsync();
            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastTransfer) && lastTransfer.Length > prefix.Length)
            {
                var seqPart = lastTransfer[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1;
            }
            var transferNumber = $"{prefix}{nextSeq:D3}";

            var activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

            var transfer = new VaultTransfer
            {
                TransferNumber = transferNumber,
                SourceTreasuryId = req.SourceTreasuryId,
                DestinationTreasuryId = req.DestinationTreasuryId,
                CashierSessionId = activeSession?.Id,
                Amount = req.Amount,
                TransferDate = DateTime.UtcNow,
                PerformedBy = userId,
                Status = TransferStatus.Pending,
                Notes = req.Notes?.Trim(),
                DepositSource = req.DepositSource
            };

            if (sourceTreasury != null) sourceTreasury.Balance -= req.Amount;

            db.VaultTransfers.Add(transfer);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Create, "VaultTransfer", transfer.Id);
            return Ok(new { transfer.Id, transfer.TransferNumber, transfer.Amount, Status = transfer.Status.ToString(), message = "تم إنشاء طلب ترحيل السيولة بنجاح وهو قيد المراجعة والاستلام الفعلي" });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// POST /api/finance-v3/treasuries/{id}/recalculate — Recalculate treasury balance (Admin only).
    /// </summary>
    [HttpPost("treasuries/{id:guid}/recalculate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RecalculateTreasuryBalance(Guid id)
    {
        var treasury = await db.Treasuries.FindAsync(id);
        if (treasury == null || !treasury.IsActive)
            return NotFound(new { message = "الخزنة غير موجودة" });

        var oldBalance = treasury.Balance;
        bool isVaultType = treasury.Type == TreasuryType.Vault;
        var applicableMethods = isVaultType ? new[] { "cash" } : new[] { "card", "bank_transfer", "bank" };

        var inflows = await db.CashFlowTransactions.Where(t => t.IsActive && t.Type == TransactionType.Inflow && t.BranchId == treasury.BranchId && applicableMethods.Contains(t.PaymentMethod.ToLower())).SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var outflows = await db.CashFlowTransactions.Where(t => t.IsActive && t.Type == TransactionType.Outflow && t.BranchId == treasury.BranchId && applicableMethods.Contains(t.PaymentMethod.ToLower())).SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var newBalance = inflows - outflows;
        var drift = newBalance - oldBalance;

        if (drift != 0)
            logger.LogWarning("Treasury drift detected for {TreasuryId} ({Name}): Old={OldBalance}, New={NewBalance}, Drift={Drift}", treasury.Id, treasury.Name, oldBalance, newBalance, drift);

        treasury.Balance = newBalance;
        treasury.UpdatedAt = DateTime.UtcNow;

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "تعارض في تحديث رصيد الخزينة. يرجى المحاولة مرة أخرى." }); }

        await audit.LogAsync(AuditAction.Update, "Treasury", id, details: $"Recalculated via V3: old={oldBalance}, new={newBalance}, drift={drift}");

        return Ok(new { treasury.Id, treasury.Name, OldBalance = oldBalance, NewBalance = newBalance, Drift = drift, DriftDetected = drift != 0, message = drift != 0 ? $"تم إعادة حساب الرصيد. تم اكتشاف انحراف بمبلغ {drift:N0} ر.ي" : "تم إعادة حساب الرصيد. لا يوجد انحراف" });
    }

    /// <summary>
    /// POST /api/finance-v3/expenses — Create an operational expense.
    /// Reuses logic from OperationalExpensesController.Create.
    /// </summary>
    [HttpPost("expenses")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest req)
    {
        // Delegate to the existing OperationalExpensesController logic via service resolution
        // We replicate the core logic here to keep it under V3 authorization policy
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "عنوان المصروف مطلوب" });
        if (!Enum.TryParse<ExpenseCategory>(req.Category, true, out var category))
            return BadRequest(new { message = "صنف المصروف غير صالح" });
        if (req.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ المصروف أكبر من الصفر" });

        var date = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(req.ExpenseDate) && DateOnly.TryParse(req.ExpenseDate, out var parsedDate))
            date = parsedDate;

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = currentUser.BranchId;
        if (branchId == null || branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل تسجيل المصروف." });

        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل مصروف نقدي." });
        }

        Treasury treasury;
        try { treasury = await treasuryResolution.ResolveTreasuryAsync(branchId.Value, req.PaymentMethod, activeSession?.Id); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        if (req.SupplierId.HasValue)
        {
            var supplierExists = await db.Suppliers.AnyAsync(s => s.Id == req.SupplierId.Value && s.IsActive);
            if (!supplierExists) return BadRequest(new { message = "المورد المحدد غير موجود" });
        }

        const decimal ApprovalThreshold = 50_000m;
        var needsApproval = req.Amount > ApprovalThreshold;
        var approvalStatus = needsApproval ? ApprovalStatus.Pending : ApprovalStatus.NotRequired;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"EXP-{datePart}-";
            if (db.Database.IsRelational())
            {
                var lockKey = StableLockKeyHelper.ExpenseNumber;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            var lastExpense = await db.OperationalExpenses.IgnoreQueryFilters()
                .Where(e => e.ExpenseNumber.StartsWith(prefix))
                .OrderByDescending(e => e.ExpenseNumber).Select(e => e.ExpenseNumber).FirstOrDefaultAsync();
            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastExpense) && lastExpense.Length > prefix.Length)
            {
                var seqPart = lastExpense[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1;
            }
            var expenseNumber = $"{prefix}{nextSeq:D3}";

            var expense = new OperationalExpense
            {
                ExpenseNumber = expenseNumber, Title = req.Title.Trim(), Category = category,
                Amount = req.Amount, ExpenseDate = date, PaymentMethod = req.PaymentMethod,
                SupplierId = req.SupplierId, LabOrderId = req.LabOrderId,
                Notes = req.Notes?.Trim(), ReceiptAttachmentUrl = req.ReceiptAttachmentUrl,
                PaidBy = userId, BranchId = branchId.Value,
                ApprovalStatus = approvalStatus, IsPostedToLedger = false
            };
            db.OperationalExpenses.Add(expense);

            if (!needsApproval)
            {
                var cashflow = new CashFlowTransaction
                {
                    TransactionNumber = $"TX-{datePart}-OUT-{nextSeq:D3}",
                    Type = TransactionType.Outflow, Category = FinancialCategory.OperationalExpense,
                    Amount = expense.Amount, PaymentMethod = expense.PaymentMethod,
                    TransactionDate = expense.ExpenseDate, ReferenceId = expense.Id,
                    ReferenceNumber = expense.ExpenseNumber,
                    Description = $"قيد مصروف تشغيلي: {expense.Title}",
                    PerformedBy = userId, BranchId = branchId.Value,
                    CashierSessionId = activeSession?.Id, TreasuryId = treasury.Id
                };
                db.CashFlowTransactions.Add(cashflow);
                expense.IsPostedToLedger = true;
                expense.CashFlowTransactionId = cashflow.Id;

                var je = await journalEntryService.CreateEntryAsync(
                    documentType: FinancialDocumentType.Expense, financialDocumentId: expense.Id,
                    description: $"قيد مصروف تشغيلي: {expense.Title}", entryDate: expense.ExpenseDate,
                    branchId: branchId.Value, performedBy: userId,
                    cashierSessionId: activeSession?.Id, treasuryId: treasury.Id,
                    lines: new[]
                    {
                        (JournalAccountType.Expense, expense.Id, expense.Amount, 0m, (string?)$"مصروف: {expense.Title}"),
                        (JournalAccountType.Treasury, treasury.Id, 0m, expense.Amount, (string?)$"سداد من: {treasury.Name}")
                    });
                je.IsPosted = true; je.PostedAt = DateTime.UtcNow;

                await treasuryResolution.DecrementTreasuryBalanceAsync(branchId.Value, expense.PaymentMethod, expense.Amount, activeSession?.Id);
            }

            if (req.LabOrderId.HasValue)
            {
                var labOrder = await db.LabOrders.FindAsync(req.LabOrderId.Value);
                if (labOrder != null) labOrder.Status = "paid";
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            await audit.LogAsync(AuditAction.Create, "OperationalExpense", expense.Id);

            return Created($"/api/finance-v3/expenses/{expense.Id}", new
            {
                expense.Id, expense.ExpenseNumber, expense.Title,
                Category = expense.Category.ToString(), expense.Amount,
                ExpenseDate = expense.ExpenseDate.ToString("yyyy-MM-dd"), expense.PaymentMethod,
                ApprovalStatus = expense.ApprovalStatus.ToString(), expense.IsPostedToLedger,
                message = needsApproval
                    ? $"تم تسجيل المصروف بنجاح. المبلغ ({req.Amount:N0} ريال) يتجاوز حد الاعتماد — في انتظار موافقة الإدارة قبل الترحيل."
                    : "تم تسجيل المصروف والترحيل المالي بنجاح"
            });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// POST /api/finance-v3/expenses/{id}/approve — Approve a pending expense (Admin only).
    /// </summary>
    [HttpPost("expenses/{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApproveExpense(Guid id, [FromBody] ApproveExpenseRequest req)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var expenseSnapshot = await db.OperationalExpenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
        if (expenseSnapshot == null) return NotFound(new { message = "المصروف غير موجود" });
        if (expenseSnapshot.ApprovalStatus != ApprovalStatus.Pending)
            return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" });

        var branchId = expenseSnapshot.BranchId;
        if (branchId == Guid.Empty) return BadRequest(new { message = "عذراً، الفرع غير محدد لهذا المصروف. تواصل مع الإدارة." });

        CashierSession? activeSession = null;
        if (string.Equals(expenseSnapshot.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null) return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير أولاً قبل اعتماد المصروف النقدي." });
        }

        Treasury treasury;
        try { treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, expenseSnapshot.PaymentMethod, activeSession?.Id); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlRawAsync(@"SELECT 1 FROM ""OperationalExpenses"" WHERE ""Id"" = {0} FOR UPDATE", id);

            var expense = await db.OperationalExpenses.FindAsync(id);
            if (expense == null || !expense.IsActive) { await tx.RollbackAsync(); return NotFound(new { message = "المصروف غير موجود" }); }
            if (expense.ApprovalStatus != ApprovalStatus.Pending) { await tx.RollbackAsync(); return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" }); }

            expense.ApprovalStatus = ApprovalStatus.Approved;
            expense.ApprovedById = userId;
            expense.ApprovedAt = DateTime.UtcNow;
            expense.ApprovalNotes = req.Notes?.Trim();

            var datePart = expense.ExpenseDate.ToString("yyyyMMdd");
            var seqSuffix = expense.ExpenseNumber.Split('-').LastOrDefault() ?? "001";
            if (!int.TryParse(seqSuffix, out var seq)) seq = 1;

            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-OUT-{seq:D3}",
                Type = TransactionType.Outflow, Category = FinancialCategory.OperationalExpense,
                Amount = expense.Amount, PaymentMethod = expense.PaymentMethod,
                TransactionDate = expense.ExpenseDate, ReferenceId = expense.Id,
                ReferenceNumber = expense.ExpenseNumber,
                Description = $"قيد مصروف تشغيلي (معتمد): {expense.Title}",
                PerformedBy = userId, BranchId = branchId,
                CashierSessionId = activeSession?.Id, TreasuryId = treasury.Id
            };
            db.CashFlowTransactions.Add(cashflow);
            expense.IsPostedToLedger = true;
            expense.CashFlowTransactionId = cashflow.Id;

            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.Expense, financialDocumentId: expense.Id,
                description: $"قيد مصروف تشغيلي (معتمد): {expense.Title}", entryDate: expense.ExpenseDate,
                branchId: branchId, performedBy: userId,
                cashierSessionId: activeSession?.Id, treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.Expense, expense.Id, expense.Amount, 0m, (string?)$"مصروف معتمد: {expense.Title}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, expense.Amount, (string?)$"سداد من: {treasury.Name}")
                });
            je.IsPosted = true; je.PostedAt = DateTime.UtcNow;

            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, expense.PaymentMethod, expense.Amount, activeSession?.Id);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Approve, "OperationalExpense", id);
            return Ok(new { message = "تم اعتماد المصروف وترحيله للأستاذ العام بنجاح", expense.Id, expense.ExpenseNumber, ApprovalStatus = expense.ApprovalStatus.ToString() });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// POST /api/finance-v3/expenses/{id}/reject — Reject a pending expense (Admin only).
    /// </summary>
    [HttpPost("expenses/{id:guid}/reject")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RejectExpense(Guid id, [FromBody] RejectExpenseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) return BadRequest(new { message = "سبب الرفض مطلوب" });
        var expense = await db.OperationalExpenses.FindAsync(id);
        if (expense == null || !expense.IsActive) return NotFound(new { message = "المصروف غير موجود" });
        if (expense.ApprovalStatus != ApprovalStatus.Pending) return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" });

        var userId = currentUser.UserId ?? Guid.Empty;
        expense.ApprovalStatus = ApprovalStatus.Rejected;
        expense.ApprovedById = userId;
        expense.ApprovedAt = DateTime.UtcNow;
        expense.ApprovalNotes = req.Reason.Trim();
        expense.IsActive = false;
        expense.DeletedAt = DateTime.UtcNow;
        expense.DeletedBy = userId;

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Update, "OperationalExpense", id, details: "Expense rejected via V3");
        return Ok(new { message = "تم رفض المصروف وإلغاؤه بنجاح", expense.Id, expense.ExpenseNumber, ApprovalStatus = expense.ApprovalStatus.ToString() });
    }

    /// <summary>
    /// DELETE /api/finance-v3/expenses/{id} — Delete/reverse an expense.
    /// Delegates to the reversal logic from OperationalExpensesController.
    /// </summary>
    [HttpDelete("expenses/{id:guid}")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        var expense = await db.OperationalExpenses.FindAsync(id);
        if (expense == null || !expense.IsActive) return NotFound(new { message = "المصروف غير موجود" });

        if (expense.ApprovalStatus == ApprovalStatus.Approved && expense.IsPostedToLedger && !currentUser.IsAdmin)
            return Forbid();

        var userId = currentUser.UserId ?? Guid.Empty;

        if (expense.IsPostedToLedger)
        {
            // Reversal path — replicate OperationalExpensesController.ReversePostedExpenseAsync
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                if (db.Database.IsRelational())
                    await db.Database.ExecuteSqlRawAsync(@"SELECT 1 FROM ""OperationalExpenses"" WHERE ""Id"" = {0} FOR UPDATE", expense.Id);

                var reloaded = await db.OperationalExpenses.FindAsync(expense.Id);
                if (reloaded == null || !reloaded.IsActive) { await tx.RollbackAsync(); return NotFound(new { message = "المصروف غير موجود" }); }

                CashFlowTransaction? originalCashflow = reloaded.CashFlowTransactionId.HasValue
                    ? await db.CashFlowTransactions.FindAsync(reloaded.CashFlowTransactionId.Value)
                    : await db.CashFlowTransactions.FirstOrDefaultAsync(t => t.ReferenceId == reloaded.Id && t.Category == FinancialCategory.OperationalExpense && t.IsActive);

                if (originalCashflow != null && originalCashflow.CashierSessionId.HasValue)
                {
                    var linkedSession = await db.CashierSessions.FindAsync(originalCashflow.CashierSessionId.Value);
                    if (linkedSession != null && linkedSession.Status != SessionStatus.Open)
                    { await tx.RollbackAsync(); return BadRequest(new { message = "لا يمكن حذف مصروف مرتبط بوردية مقفلة أو مطابقة. تواصل مع المحاسب." }); }
                }

                if (originalCashflow?.ReversedByTransactionId != null)
                { await tx.RollbackAsync(); return BadRequest(new { message = "هذا المصروف تم عكسه مسبقاً." }); }

                if (originalCashflow == null || originalCashflow.TreasuryId == null || originalCashflow.TreasuryId == Guid.Empty)
                { await tx.RollbackAsync(); return BadRequest(new { message = "عذراً، لا يمكن عكس القيد — سجل التدفق النقدي الأصلي غير مرتبط بخزينة. تواصل مع المحاسب." }); }

                var originalTreasuryId = originalCashflow.TreasuryId.Value;
                var originalTreasury = await db.Treasuries.FindAsync(originalTreasuryId);
                if (originalTreasury == null || !originalTreasury.IsActive)
                { await tx.RollbackAsync(); return BadRequest(new { message = "عذراً، الخزينة الأصلية غير موجودة أو غير مفعلة. لا يمكن عكس القيد المالي — تواصل مع المحاسب." }); }

                var reversalCashflow = new CashFlowTransaction
                {
                    TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-OUT-REV-{Guid.NewGuid().ToString()[..8]}",
                    Type = TransactionType.Inflow, Category = FinancialCategory.Reversal,
                    Amount = reloaded.Amount, PaymentMethod = reloaded.PaymentMethod,
                    TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                    ReferenceId = reloaded.Id, ReferenceNumber = reloaded.ExpenseNumber,
                    Description = $"عكس قيد مصروف تشغيلي: {reloaded.Title}",
                    PerformedBy = userId, BranchId = reloaded.BranchId,
                    IsReversal = true, ReversalOfTransactionId = originalCashflow.Id,
                    CashierSessionId = originalCashflow.CashierSessionId, TreasuryId = originalTreasuryId
                };
                db.CashFlowTransactions.Add(reversalCashflow);
                originalCashflow.ReversedByTransactionId = reversalCashflow.Id;

                var originalJe = await db.JournalEntries.FirstOrDefaultAsync(e => e.FinancialDocumentId == reloaded.Id && e.FinancialDocumentType == FinancialDocumentType.Expense && !e.IsReversal);
                if (originalJe != null)
                {
                    var reversalJe = await journalEntryService.CreateReversalEntryAsync(originalEntryId: originalJe.Id, reason: $"حذف مصروف: {reloaded.Title}", performedBy: userId);
                    reversalJe.IsPosted = true; reversalJe.PostedAt = DateTime.UtcNow;
                }

                await treasuryResolution.IncrementTreasuryBalanceByTreasuryIdAsync(originalTreasuryId, reloaded.Amount);
                reloaded.IsActive = false; reloaded.DeletedAt = DateTime.UtcNow; reloaded.DeletedBy = userId;

                if (reloaded.LabOrderId.HasValue)
                {
                    var labOrder = await db.LabOrders.FindAsync(reloaded.LabOrderId.Value);
                    if (labOrder != null) labOrder.Status = "received";
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();
                await audit.LogAsync(AuditAction.Update, "OperationalExpense", reloaded.Id, details: "Posted expense reversed via V3");
                return Ok(new { message = "تم عكس قيد المصروف وترحيل القيد العكسي بنجاح" });
            }
            catch { await tx.RollbackAsync(); throw; }
        }

        // Unposted expense: safe to soft-delete
        expense.IsActive = false; expense.DeletedAt = DateTime.UtcNow; expense.DeletedBy = userId;
        if (expense.LabOrderId.HasValue)
        {
            var labOrder = await db.LabOrders.FindAsync(expense.LabOrderId.Value);
            if (labOrder != null) labOrder.Status = "received";
        }
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف قيد المصروف بنجاح" });
    }

    /// <summary>
    /// POST /api/finance-v3/supplier-bills — Create a supplier bill.
    /// </summary>
    [HttpPost("supplier-bills")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateSupplierBill([FromBody] CreateSupplierBillRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Description)) return BadRequest(new { message = "وصف الفاتورة مطلوب" });
        if (req.TotalAmount <= 0) return BadRequest(new { message = "يجب أن يكون إجمالي الفاتورة أكبر من الصفر" });

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == req.SupplierId && s.IsActive);
        if (supplier == null) return BadRequest(new { message = "المورد المحدد غير موجود" });

        var billDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(req.BillDate) && DateOnly.TryParse(req.BillDate, out var parsedBill)) billDate = parsedBill;
        DateOnly? dueDate = null;
        if (!string.IsNullOrWhiteSpace(req.DueDate) && DateOnly.TryParse(req.DueDate, out var parsedDue)) dueDate = parsedDue;

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = currentUser.BranchId ?? Guid.Empty;
        if (branchId == Guid.Empty) return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل تسجيل فاتورة المورد." });

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"BILL-{datePart}-";
            if (db.Database.IsRelational())
            {
                var lockKey = StableLockKeyHelper.BillNumber;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            var lastBill = await db.SupplierBills.IgnoreQueryFilters()
                .Where(b => b.BillNumber.StartsWith(prefix))
                .OrderByDescending(b => b.BillNumber).Select(b => b.BillNumber).FirstOrDefaultAsync();
            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastBill) && lastBill.Length > prefix.Length)
            { var seqPart = lastBill[prefix.Length..]; if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1; }
            var billNumber = $"{prefix}{nextSeq:D3}";

            var bill = new SupplierBill
            {
                BillNumber = billNumber, SupplierId = req.SupplierId,
                Description = req.Description.Trim(), TotalAmount = req.TotalAmount,
                PaidAmount = 0, Status = BillStatus.Unpaid, BillDate = billDate,
                DueDate = dueDate, PurchaseOrderId = req.PurchaseOrderId,
                LabOrderId = req.LabOrderId, AttachmentUrl = req.AttachmentUrl,
                Notes = req.Notes?.Trim(), BranchId = branchId, CreatedBy = userId
            };
            db.SupplierBills.Add(bill);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Create, "SupplierBill", bill.Id);
            return Created($"/api/finance-v3/supplier-bills/{bill.Id}", new
            {
                bill.Id, bill.BillNumber, SupplierName = supplier.Name,
                bill.Description, bill.TotalAmount, bill.PaidAmount,
                RemainingAmount = bill.TotalAmount, Status = bill.Status.ToString(),
                BillDate = bill.BillDate.ToString("yyyy-MM-dd"),
                DueDate = bill.DueDate?.ToString("yyyy-MM-dd"),
                message = "تم تسجيل فاتورة المورد بنجاح"
            });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// POST /api/finance-v3/supplier-bills/{id}/pay — Pay a supplier bill installment.
    /// </summary>
    [HttpPost("supplier-bills/{id:guid}/pay")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> PaySupplierBill(Guid id, [FromBody] PayBillInstallmentRequest req)
    {
        if (req.Amount <= 0) return BadRequest(new { message = "يجب أن يكون مبلغ الدفعة أكبر من الصفر" });

        var billSnapshot = await db.SupplierBills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
        if (billSnapshot == null) return NotFound(new { message = "الفاتورة غير موجودة" });
        if (billSnapshot.Status == BillStatus.FullyPaid) return BadRequest(new { message = "هذه الفاتورة مدفوعة بالكامل بالفعل" });
        if (billSnapshot.Status == BillStatus.Cancelled) return BadRequest(new { message = "هذه الفاتورة ملغاة" });

        var paymentDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(req.PaymentDate) && DateOnly.TryParse(req.PaymentDate, out var parsedDate)) paymentDate = parsedDate;

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = billSnapshot.BranchId;
        if (branchId == Guid.Empty) return BadRequest(new { message = "عذراً، الفرع غير محدد لفاتورة المورد. تواصل مع الإدارة." });

        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null) return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير أولاً قبل سداد فواتير المورد النقدية." });
        }

        Treasury treasury;
        try { treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, req.PaymentMethod, activeSession?.Id); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlRawAsync(@"SELECT 1 FROM ""SupplierBills"" WHERE ""Id"" = {0} FOR UPDATE", id);

            var bill = await db.SupplierBills.Include(b => b.Supplier).FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
            if (bill == null) { await tx.RollbackAsync(); return NotFound(new { message = "الفاتورة غير موجودة" }); }
            if (bill.Status == BillStatus.FullyPaid) { await tx.RollbackAsync(); return BadRequest(new { message = "هذه الفاتورة مدفوعة بالكامل بالفعل" }); }
            if (bill.Status == BillStatus.Cancelled) { await tx.RollbackAsync(); return BadRequest(new { message = "هذه الفاتورة ملغاة" }); }

            var remaining = bill.TotalAmount - bill.PaidAmount;
            if (req.Amount > remaining) { await tx.RollbackAsync(); return BadRequest(new { message = $"مبلغ الدفعة ({req.Amount:N0}) يتجاوز المبلغ المتبقي ({remaining:N0} ريال)" }); }

            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-BILL-{DateTime.UtcNow:HHmmss}",
                Type = TransactionType.Outflow, Category = FinancialCategory.SupplierPayment,
                Amount = req.Amount, PaymentMethod = req.PaymentMethod,
                TransactionDate = paymentDate, ReferenceId = bill.Id,
                ReferenceNumber = bill.BillNumber,
                Description = $"دفعة على فاتورة مورد {bill.BillNumber} — {bill.Supplier?.Name ?? ""}",
                PerformedBy = userId, BranchId = branchId,
                CashierSessionId = activeSession?.Id, TreasuryId = treasury.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            var payment = new SupplierBillPayment
            {
                SupplierBillId = bill.Id, Amount = req.Amount,
                PaymentMethod = req.PaymentMethod, PaymentDate = paymentDate,
                ReferenceNumber = req.ReferenceNumber, Notes = req.Notes?.Trim(),
                PaidBy = userId, CashFlowTransactionId = cashflow.Id
            };
            db.SupplierBillPayments.Add(payment);

            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.SupplierPayment, financialDocumentId: payment.Id,
                description: $"سداد فاتورة مورد: {bill.BillNumber} — {bill.Supplier?.Name ?? ""}",
                entryDate: paymentDate, branchId: branchId, performedBy: userId,
                cashierSessionId: activeSession?.Id, treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.Payable, bill.SupplierId, req.Amount, 0m, (string?)$"سداد مستحقات: {bill.Supplier?.Name}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, req.Amount, (string?)$"سداد من: {treasury.Name}")
                });
            je.IsPosted = true; je.PostedAt = DateTime.UtcNow;

            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, req.PaymentMethod, req.Amount, activeSession?.Id);

            bill.PaidAmount += req.Amount;
            bill.Status = bill.PaidAmount >= bill.TotalAmount ? BillStatus.FullyPaid : BillStatus.PartiallyPaid;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Create, "SupplierBillPayment", payment.Id, details: $"Bill {id} partial payment via V3");

            return Ok(new
            {
                message = bill.Status == BillStatus.FullyPaid
                    ? "تم سداد الفاتورة بالكامل! تم ترحيل القيد للأستاذ العام."
                    : $"تم تسجيل الدفعة بنجاح. المبلغ المتبقي: {bill.TotalAmount - bill.PaidAmount:N0} ريال",
                bill.Id, bill.BillNumber, bill.PaidAmount,
                RemainingAmount = bill.TotalAmount - bill.PaidAmount,
                Status = bill.Status.ToString()
            });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // ─── Cashier Session Helpers (shared with CashierSessionsController) ──

    private static bool IsCashMethod(string method) =>
        string.Equals(method, "cash", StringComparison.OrdinalIgnoreCase);

    private static bool IsCardMethod(string method) =>
        string.Equals(method, "card", StringComparison.OrdinalIgnoreCase);

    private static bool IsBankMethod(string method) =>
        string.Equals(method, "bank_transfer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "bank", StringComparison.OrdinalIgnoreCase);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<decimal> CalculateContractOutstandingAsync(Guid? branchId)
    {
        var query = db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active);

        if (branchId.HasValue)
            query = query.Where(c => c.Patient.BranchId == branchId.Value);

        var contracts = await query.ToListAsync();
        return contracts.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount));
    }

    private async Task<decimal> CalculateInvoiceOutstandingAsync(Guid? branchId)
    {
        var query = db.Invoices
            .Include(i => i.Payments)
            .Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);

        if (branchId.HasValue)
            query = query.Where(i => i.Patient.BranchId == branchId.Value);

        var invoices = await query.ToListAsync();
        return invoices.Sum(i => i.TotalAmount - i.Payments.Where(p => p.IsActive).Sum(p => p.Amount));
    }
}

/// <summary>
/// DTO for cancelling an invoice via Finance V3 endpoint.
/// </summary>
public sealed class CancelInvoiceRequest { public string? Notes { get; init; } }
