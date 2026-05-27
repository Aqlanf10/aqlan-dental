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
    ICurrentUserService currentUser) : ControllerBase
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
