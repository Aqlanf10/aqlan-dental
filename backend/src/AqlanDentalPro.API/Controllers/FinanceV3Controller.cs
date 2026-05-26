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

        // ── Pending approvals ──
        var pendingExpenses = await db.OperationalExpenses
            .CountAsync(e => e.ApprovalStatus == ApprovalStatus.Pending && e.IsActive);
        var pendingTransfers = await db.VaultTransfers
            .CountAsync(t => t.Status == TransferStatus.Pending && t.IsActive);

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
    /// </summary>
    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitAndLoss(
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = DateOnly.TryParse(fromDate, out var f) ? f : new DateOnly(today.Year, today.Month, 1);
        var to = DateOnly.TryParse(toDate, out var t) ? t : today;

        // Revenue = Patient Payments (Inflow) - Refunds (Outflow)
        // FIX: Reversal CashFlowTransactions are included in the calculation
        // because they carry the opposite Type (reversal of Inflow = Outflow),
        // so they naturally reduce net amounts. We do NOT filter out IsReversal (Blocker 5).
        var revenuePayments = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Inflow
                && tx.Category == FinancialCategory.PatientPayment);
        var refundPayments = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.Refund);

        if (branchId.HasValue)
        {
            revenuePayments = revenuePayments.Where(tx => tx.BranchId == branchId.Value);
            refundPayments = refundPayments.Where(tx => tx.BranchId == branchId.Value);
        }

        var revenue = await revenuePayments.SumAsync(tx => (decimal?)tx.Amount) ?? 0;
        var refunds = await refundPayments.SumAsync(tx => (decimal?)tx.Amount) ?? 0;

        // Operating Expenses
        var expenses = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.OperationalExpense);
        if (branchId.HasValue) expenses = expenses.Where(tx => tx.BranchId == branchId.Value);
        var operatingExpenses = await expenses.SumAsync(tx => (decimal?)tx.Amount) ?? 0;

        // Salary Payments
        var salaries = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.SalaryPayment);
        if (branchId.HasValue) salaries = salaries.Where(tx => tx.BranchId == branchId.Value);
        var salaryTotal = await salaries.SumAsync(tx => (decimal?)tx.Amount) ?? 0;

        // Doctor Commissions
        var commissions = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.DoctorCommission);
        if (branchId.HasValue) commissions = commissions.Where(tx => tx.BranchId == branchId.Value);
        var commissionTotal = await commissions.SumAsync(tx => (decimal?)tx.Amount) ?? 0;

        // Supplier Payments
        var supplierPayments = db.CashFlowTransactions
            .Where(tx => tx.TransactionDate >= from && tx.TransactionDate <= to
                && tx.Type == TransactionType.Outflow
                && tx.Category == FinancialCategory.SupplierPayment);
        if (branchId.HasValue) supplierPayments = supplierPayments.Where(tx => tx.BranchId == branchId.Value);
        var supplierTotal = await supplierPayments.SumAsync(tx => (decimal?)tx.Amount) ?? 0;

        var netRevenue = revenue - refunds;
        var totalCosts = operatingExpenses + salaryTotal + commissionTotal + supplierTotal;
        var netProfit = netRevenue - totalCosts;

        // Cross-check with JournalEntry canonical data
        var jeRevenue = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Revenue
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Credit) ?? 0;

        var jeExpenses = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Expense
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Debit) ?? 0;

        return Ok(new
        {
            Period = new { From = from.ToString("yyyy-MM-dd"), To = to.ToString("yyyy-MM-dd") },

            // P&L from CashFlowTransaction (operational)
            Revenue = revenue,
            Refunds = refunds,
            NetRevenue = netRevenue,
            OperatingExpenses = operatingExpenses,
            SalaryPayments = salaryTotal,
            DoctorCommissions = commissionTotal,
            SupplierPayments = supplierTotal,
            TotalCosts = totalCosts,
            NetProfit = netProfit,
            ProfitMargin = netRevenue > 0 ? (double)(netProfit / netRevenue * 100) : 0,

            // Cross-check from JournalEntry (canonical)
            JournalEntryRevenue = jeRevenue,
            JournalEntryExpenses = jeExpenses,
            JournalEntryNetProfit = jeRevenue - jeExpenses,
            Variance = netProfit - (jeRevenue - jeExpenses),

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
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null)
            return NotFound(new { message = "المريض غير موجود" });

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
