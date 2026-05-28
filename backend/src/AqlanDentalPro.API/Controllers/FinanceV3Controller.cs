using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Finance V3 API — Provides data endpoints for the Finance V3 Financial Center dashboard.
/// Access is restricted to Admin and Accountant roles only (ReportsAccess policy).
///
/// MIGRATION STATUS (CashFlowTransaction → JournalEntry/JournalLine):
/// ─────────────────────────────────────────────────────────────────────
/// Migration A — COMPLETED:
///   ✅ GET /dashboard          — migrated to JournalLine (Treasury debits/credits)
///   ✅ GET /account-balances   — already reads from JournalLine (no change needed)
///
/// Migration B — COMPLETED:
///   ✅ GET /dashboard          — FIXED: inflow/outflow labels were swapped
///   ✅ GET /daily-cash-summary — migrated from CashFlowTransaction to JournalLine + Treasury
///   ✅ GET /profit-loss        — migrated cash figures from CashFlowTransaction to JournalLine
///   ✅ GET /patient-balance    — enriched with JournalLine balance calculation
///   ✅ GET /patient-accounts   — enriched with JournalLine aggregation
///   ✅ GET /payments           — already reads from Payment entity (no CashFlow dependency)
///   ✅ GET /invoices           — already reads from Invoice entity (no CashFlow dependency)
///
/// Migration C — COMPLETED:
///   ✅ GET /expenses           — TreasuryId/TreasuryName now from JournalLine (Treasury account)
///   ✅ GET /active-cashier-session — expected values from JournalLine instead of CashFlow
///   ✅ GET /cashier-sessions/active — expected values from JournalLine instead of CashFlow
///   ✅ POST cashier-sessions/close — expected values + entry linking from JournalLine
///   ✅ POST treasuries/recalculate — balance from JournalLine instead of CashFlow
///   ✅ MapDocumentTypeToCategory   — added AdvancePayment explicit mapping
///   ✅ GET /invoices comment fix   — corrected misleading Balance comment
///
/// Migration D — COMPLETED (this commit):
///   ✅ GET /audit              — CashFlowTransaction removed from resource filter;
///      audit entries now enriched with JournalEntry/JournalLine data (date, type,
///      category, amount, treasury, description, reversal status)
///   ✅ Removed IsCashMethod/IsCardMethod/IsBankMethod — unused after Migration C
///   ✅ Code cleanup — removed obsolete helpers and comment blocks
///
/// Hotfixes (same PR):
///   ✅ ExpectedClosingCard = 0 fix — merged card with bank (TODO: TreasuryType.Card)
///   ✅ Treasury opening balance — creates JournalEntry on treasury creation
///   ✅ Treasury recalculate fallback — includes CashFlow OP-BAL for legacy treasuries
///   ✅ DELETE /expenses reads — migrated from CashFlowTransaction to JournalEntry
///
/// Hotfixes 5D:
///   ✅ Fix double SaveChanges in POST /treasuries — manual JournalEntry creation
///      with IsPosted=true from the start (single SaveChanges, atomic)
///   ✅ Fix recalculate fallback too broad — now checks for "رصيد افتتاحي" in
///      description instead of any VaultTransfer; improved fallback calculation logic
///   ✅ Fix branchId=Guid.Empty causes 500 — added early BadRequest(400) validation
///      in POST /treasuries, POST /payments, POST cashier-sessions/close
///
/// Sprint 1 — Finance Stability (this commit):
///   ✅ Admin branchId fallback — when Admin user has no branch assigned (Guid.Empty),
///      GET endpoints bypass branch filter for consolidated view (already worked).
///      POST endpoints now use first active branch as fallback instead of rejecting
///      with BadRequest, so admin can still perform write operations.
///   ✅ Nullable decimal safety — all SumAsync calls use (decimal?) cast with ?? 0m
///      to prevent NullReferenceException on empty result sets.
///   ✅ Overdue calculation null safety — StartDate! removed, uses .GetValueOrDefault()
///      to prevent NullReferenceException when contract StartDate is null.
///   ✅ Helper methods — CalculateContractOutstandingAsync and
///      CalculateInvoiceOutstandingAsync now use nullable-safe aggregation.
///
/// Hotfixes 5E:
///   ✅ Eliminated all remaining CreateEntryAsync/CreateReversalEntryAsync calls —
///      replaced with manual JournalEntry+JournalLine creation (IsPosted=true from start)
///      in POST /expenses, POST /expenses/{id}/approve, DELETE /expenses,
///      POST /supplier-bills/{id}/pay — single SaveChanges per operation
///   ✅ Unified branchId validation in POST /vault-transfers — now applies to ALL
///      users (including Admin), returns BadRequest(400) instead of Forbid(403)
///
/// Phase 6 — Final Cleanup (this commit):
///   ✅ GET /dashboard — extended with legacy summary fields for daily-operations
///      FinanceView migration: ActiveContracts, UnpaidInvoicesCount, DraftInvoicesCount,
///      OverdueAmount, PendingCommissionsAmount, RecentPayments, RecentInvoices
///   ✅ Frontend migrated: useFinanceSummary now calls GET /api/finance-v3/dashboard
///   ✅ Deleted GET /api/finance/summary from PaymentsController (was [Obsolete])
///   ✅ Deleted GET /api/finance/overdue from PaymentsController (was [Obsolete])
///   ✅ Frontend link /finance/overdue → /finance-v3?tab=contracts
///   ✅ Deleted GET /api/cashier-sessions/active from CashierSessionsController (was [Obsolete])
///
/// Remaining CashFlowTransaction references (WRITE ONLY — dual-write, keep for now):
///   ✅ POST /treasuries       — creates CashFlowTransaction (OP-BAL, dual-write, preserved)
///   ✅ POST /expenses          — creates CashFlowTransaction (dual-write, preserved)
///   ✅ DELETE /expenses/{id}   — creates reversal CashFlowTransaction (dual-write, preserved)
///   ✅ POST /expenses/{id}/approve — creates CashFlowTransaction (dual-write, preserved)
///   ✅ POST /payments          — creates CashFlowTransaction (dual-write, preserved)
///   ✅ POST /supplier-bills/pay — creates CashFlowTransaction (dual-write, preserved)
///   ✅ POST cashier-sessions/close — links CashFlowTransactions (backward compat, preserved)
///   ✅ POST treasuries/recalculate — reads CashFlowTransaction for opening balance fallback
///
/// ALL READS NOW USE JournalEntry/JournalLine — CashFlowTransaction is WRITE-ONLY.
///
/// Future phases (not in scope):
///   ⏳ Balance Sheet endpoint
///   ⏳ Remove CashFlowTransaction dual-write once JournalLine is fully verified
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
    /// Migration A: Now reads from JournalEntry/JournalLine (canonical source of truth)
    /// instead of CashFlowTransaction (transitional).
    /// Sprint 1: Admin users with Guid.Empty branchId bypass branch filter → consolidated data.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? period = "today")
    {
        // Blocker 6: Branch isolation guard for non-admin users
        // Admin users with no branch (Guid.Empty) bypass the branch filter to view
        // consolidated statistics across all branches (Sprint 1 admin fallback).
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = DateOnly.FromDateTime(DateTime.Today);
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
        var overdueContracts = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null)
            .ToListAsync();
        var overdueAmount = 0m;
        foreach (var c in overdueContracts)
        {
            if (branchId.HasValue && c.Patient?.BranchId != branchId.Value) continue;
            // Sprint 1: Null safety — use GetValueOrDefault instead of ! operator
            // to prevent NullReferenceException when StartDate is null
            var startDate = c.StartDate.GetValueOrDefault();
            if (startDate == default) continue;
            var monthsElapsed = ((today.Year - startDate.Year) * 12) + (today.Month - startDate.Month);
            if (monthsElapsed <= 0) continue;
            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var actualPaid = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
            var overAmt = expectedPaid - actualPaid;
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
        var recentInvoices = await recentInvoicesQuery
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => new { i.Id, i.InvoiceNumber, TotalAmount = i.TotalAmount, Status = i.Status.ToString() })
            .ToListAsync();

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
                ? $"{(double)postedEntryCount / journalEntryCount * 100:F1}%"
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
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var targetDate = DateOnly.TryParse(date, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);
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
            JournalEntryCount = journalEntries
        });
    }

    /// <summary>
    /// Maps FinancialDocumentType to legacy CashFlowTransaction Category strings
    /// for frontend compatibility during the migration period.
    /// </summary>
    private static string MapDocumentTypeToCategory(FinancialDocumentType docType) => docType switch
    {
        FinancialDocumentType.Payment => "PatientPayment",
        FinancialDocumentType.Refund => "Refund",
        FinancialDocumentType.Expense => "OperationalExpense",
        FinancialDocumentType.SalaryPayment => "SalaryPayment",
        FinancialDocumentType.CommissionPayment => "DoctorCommission",
        FinancialDocumentType.SupplierPayment => "SupplierPayment",
        FinancialDocumentType.VaultTransfer => "InternalTransfer",
        FinancialDocumentType.ContractCancellation => "Reversal",
        FinancialDocumentType.PaymentDeletion => "Reversal",
        FinancialDocumentType.Invoice => "Revenue",
        FinancialDocumentType.AdvancePayment => "AdvancePayment",
        _ => "Other"
    };

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
            ProfitMargin = netCashCollections > 0 ? (double)(cashNetProfit / netCashCollections * 100) : 0,

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
            ExpenseTransactionCount = expenseTransactionCount
        });
    }

    /// <summary>
    /// Calculates the net cash outflow for a specific FinancialDocumentType category.
    /// Returns: original outflows (Treasury Credit) minus reversal inflows (Treasury Debit).
    /// In double-entry: Treasury Credit = cash paid out, Treasury Debit = cash received back (reversal).
    /// Reversals are identified by JournalEntry.IsReversal on entries of the same document type.
    /// </summary>
    private async Task<decimal> CalculateCashCategoryAsync(
        FinancialDocumentType docType,
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        // Original outflows: Treasury Credit lines from non-reversal entries
        var outflows = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.Credit > 0
                && l.JournalEntry.FinancialDocumentType == docType
                && !l.JournalEntry.IsReversal
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Credit) ?? 0;

        // Reversal inflows: Treasury Debit lines from reversal entries of same doc type
        var reversalInflows = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.Debit > 0
                && l.JournalEntry.FinancialDocumentType == docType
                && l.JournalEntry.IsReversal
                && l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to
                && l.JournalEntry.IsPosted
                && (!branchId.HasValue || l.BranchId == branchId.Value))
            .SumAsync(l => (decimal?)l.Debit) ?? 0;

        return outflows - reversalInflows;
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
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null)
            return NotFound(new { message = "المريض غير موجود" });

        // Blocker 6: Branch filter for non-admin users
        if (!currentUser.IsAdmin && patient.BranchId != currentUser.BranchId)
            return Forbid("ليس لديك صلاحية الوصول إلى بيانات مريض من فرع آخر");

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
                TotalDebit = g.Sum(l => l.Debit),
                TotalCredit = g.Sum(l => l.Credit)
            })
            .ToListAsync();

        var receivableLine = journalBalance.FirstOrDefault(b => b.AccountType == JournalAccountType.PatientReceivable);
        var advanceLine = journalBalance.FirstOrDefault(b => b.AccountType == JournalAccountType.PatientAdvance);

        var journalReceivable = (receivableLine?.TotalDebit ?? 0) - (receivableLine?.TotalCredit ?? 0);
        var journalAdvance = (advanceLine?.TotalDebit ?? 0) - (advanceLine?.TotalCredit ?? 0);
        var journalNetBalance = journalReceivable + journalAdvance;

        // ── Entity-based detail fields (for UI compatibility) ──
        var totalInvoiced = await db.Invoices
            .Where(i => i.PatientId == patientId && i.IsActive
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid))
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        var totalPaid = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive && p.Amount > 0)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var totalRefunds = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive && p.Amount < 0)
            .SumAsync(p => (decimal?)p.Amount) ?? 0; // negative values

        var totalDiscounts = await db.Contracts
            .Where(c => c.PatientId == patientId && c.IsActive)
            .SumAsync(c => (decimal?)c.DiscountAmount) ?? 0;

        var netPaid = totalPaid + totalRefunds; // refunds are negative
        var entityBalance = totalInvoiced - netPaid - totalDiscounts;

        // Contract outstanding
        var contractOutstanding = await db.Contracts
            .Where(c => c.PatientId == patientId && c.Status == ContractStatus.Active && c.IsActive)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount))
            .SumAsync();

        // Use JournalLine balance as the canonical Balance field
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
            Balance = journalNetBalance, // JournalLine-based canonical balance
            EntityBalance = entityBalance, // Entity-based balance for reconciliation
            ContractOutstanding = contractOutstanding,
            HasOutstanding = journalNetBalance > 0,
            JournalReceivable = journalReceivable,
            JournalAdvance = journalAdvance
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
        // Blocker 6: Audit endpoint restricted to Admin only
        // Financial audit trail contains cross-branch sensitive data;
        // non-admin Accountant users should not access other branches' audit records.
        if (!currentUser.IsAdmin)
            return Forbid("الاطلاع على سجل المراجعة متاح للمسؤول فقط.");

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
        // Blocker 6: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

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
                Balance = g.Sum(l => l.Debit) - g.Sum(l => l.Credit)
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
                TotalPaid = db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive && pay.Amount > 0).Sum(pay => (decimal?)pay.Amount) ?? 0,
                TotalRefunds = db.Payments.Where(pay => pay.PatientId == p.Id && pay.IsActive && pay.Amount < 0).Sum(pay => (decimal?)Math.Abs(pay.Amount)) ?? 0,
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
    /// Migration B: Already reads from Invoice entity (not CashFlowTransaction).
    /// Balance is calculated from Invoice.TotalAmount minus active Payments.
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

        // Migration C: Load expense IDs first, then resolve Treasury info from JournalLine
        var expensePage = await query
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
                IsReversal = db.JournalEntries.Any(je => je.FinancialDocumentId == e.Id
                    && je.FinancialDocumentType == FinancialDocumentType.Expense
                    && je.IsReversal && je.IsPosted),
                JournalEntryId = (Guid?)db.JournalEntries
                    .Where(je => je.FinancialDocumentId == e.Id
                        && je.FinancialDocumentType == FinancialDocumentType.Expense
                        && !je.IsReversal && je.IsPosted)
                    .Select(je => je.Id)
                    .FirstOrDefault()
            })
            .ToListAsync();

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

    // ─── Cashier Sessions Active (Finance V3) ──────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/cashier-sessions/active — Get the active cashier session for the current user.
    /// Returns the session with the proper shape expected by the Finance V3 frontend.
    /// Migration C: Expected values now calculated from JournalLine (Treasury account type)
    /// instead of CashFlowTransaction. Payment method is determined by Treasury.Type:
    ///   Vault → cash, Bank → bank_transfer/card.
    /// Inflow = Treasury Debit (money received), Outflow = Treasury Credit (money paid).
    /// Only posted JournalEntries within the session time range are included.
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

        // Migration C: Calculate expected values from JournalLine instead of CashFlowTransaction
        // Get all Treasury JournalLines from posted JournalEntries created by this cashier
        // during the session time range
        var sessionJournalLines = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.JournalEntry.IsPosted
                && l.JournalEntry.PerformedBy == cashierId
                && l.JournalEntry.CreatedAt >= session.OpeningTime
                && l.JournalEntry.CreatedAt <= DateTime.UtcNow)
            .Select(l => new
            {
                l.JournalEntryId,
                l.Debit,
                l.Credit,
                l.AccountId // TreasuryId
            })
            .ToListAsync();

        // Load treasury types for payment method mapping
        var treasuryIds = sessionJournalLines.Select(l => l.AccountId).Distinct().ToList();
        var treasuryTypes = await db.Treasuries
            .Where(t => treasuryIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => (TreasuryType?)t.Type);

        // Classify each line by payment method (Treasury.Type) and direction (Debit/Credit)
        // Vault-type treasury → cash, Bank-type treasury → bank/card
        decimal cashInflows = 0, cashOutflows = 0;
        decimal bankInflows = 0, bankOutflows = 0;

        foreach (var line in sessionJournalLines)
        {
            var tType = treasuryTypes.GetValueOrDefault(line.AccountId);
            var isCash = tType != TreasuryType.Bank; // Vault or unknown → cash
            var isBank = tType == TreasuryType.Bank;

            if (line.Debit > 0) // Inflow (money received into treasury)
            {
                if (isCash) cashInflows += line.Debit;
                else if (isBank) bankInflows += line.Debit;
            }
            else if (line.Credit > 0) // Outflow (money paid from treasury)
            {
                if (isCash) cashOutflows += line.Credit;
                else if (isBank) bankOutflows += line.Credit;
            }
        }

        // TODO: Separate card from bank when TreasuryType.Card is introduced
        // Currently card and bank are merged since both go through Bank-type treasuries
        var cardInflows = bankInflows;
        var cardOutflows = bankOutflows;

        var totalCollections = sessionJournalLines.Where(l => l.Debit > 0).Sum(l => l.Debit);

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
        // Sprint 1: Admin branchId fallback — if admin has no branch assigned,
        // use the first active branch instead of rejecting with BadRequest.
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. يرجى تسجيل الدخول بفرع صالح." });

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
    /// Migration C: Expected values now calculated from JournalLine (Treasury account type)
    /// instead of CashFlowTransaction. Unlinked JournalEntries are linked to the session
    /// instead of unlinked CashFlowTransactions. Payment method is determined by Treasury.Type.
    /// </summary>
    [HttpPost("cashier-sessions/close")]
    [Authorize(Policy = "CashierAccess")]
    public async Task<IActionResult> CloseCashierSession([FromBody] CloseSessionRequest req)
    {
        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. يرجى تسجيل الدخول بفرع صالح." });

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

        // Migration C: Calculate expected values from JournalLine instead of CashFlowTransaction
        var sessionJournalLines = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.JournalEntry.IsPosted
                && l.JournalEntry.PerformedBy == userId
                && l.JournalEntry.CreatedAt >= session.OpeningTime
                && l.JournalEntry.CreatedAt <= DateTime.UtcNow)
            .Select(l => new
            {
                l.JournalEntryId,
                l.Debit,
                l.Credit,
                l.AccountId // TreasuryId
            })
            .ToListAsync();

        // Load treasury types for payment method mapping
        var treasuryIds = sessionJournalLines.Select(l => l.AccountId).Distinct().ToList();
        var treasuryTypes = await db.Treasuries
            .Where(t => treasuryIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => (TreasuryType?)t.Type);

        // Classify each line by payment method (Treasury.Type) and direction (Debit/Credit)
        decimal cashInflows = 0, cashOutflows = 0;
        decimal bankInflows = 0, bankOutflows = 0;

        foreach (var line in sessionJournalLines)
        {
            var tType = treasuryTypes.GetValueOrDefault(line.AccountId);
            var isCash = tType != TreasuryType.Bank;
            var isBank = tType == TreasuryType.Bank;

            if (line.Debit > 0) // Inflow
            {
                if (isCash) cashInflows += line.Debit;
                else if (isBank) bankInflows += line.Debit;
            }
            else if (line.Credit > 0) // Outflow
            {
                if (isCash) cashOutflows += line.Credit;
                else if (isBank) bankOutflows += line.Credit;
            }
        }

        // TODO: Separate card from bank when TreasuryType.Card is introduced
        // Currently card and bank are merged since both go through Bank-type treasuries
        var cardInflows = bankInflows;
        var cardOutflows = bankOutflows;

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

        // Migration C: Link any unlinked JournalEntries (instead of CashFlowTransactions)
        // to this session for audit trail completeness
        var unlinkedEntries = await db.JournalEntries
            .Where(je => je.CashierSessionId == null
                && je.PerformedBy == userId
                && je.CreatedAt >= session.OpeningTime
                && je.IsPosted)
            .ToListAsync();
        foreach (var je in unlinkedEntries)
            je.CashierSessionId = session.Id;

        // Also link unlinked CashFlowTransactions for backward compatibility
        // (dual-write: both CashFlowTransaction and JournalEntry exist)
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

        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. لا توجد فروع نشطة في النظام." });

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

            // Fix: Create JournalEntry manually with IsPosted = true from the start.
            // This avoids the double-save problem where CreateEntryAsync calls SaveChanges
            // with IsPosted=false, then IsPosted=true is set and saved again — if the second
            // save fails, the entry remains unposted, meaning the opening balance is in
            // CashFlowTransaction but not in JournalLine. By creating everything in memory
            // and saving once, we ensure atomicity.
            var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
            var openingJe = new JournalEntry
            {
                EntryNumber = entryNumber,
                FinancialDocumentId = treasury.Id,
                FinancialDocumentType = FinancialDocumentType.VaultTransfer,
                Description = $"رصيد افتتاحي: {treasury.Name}",
                EntryDate = DateOnly.FromDateTime(DateTime.Today),
                BranchId = branchId,
                PerformedBy = currentUser.UserId ?? Guid.Empty,
                CashierSessionId = null,
                TreasuryId = treasury.Id,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                IsReversal = false,
            };
            db.JournalEntries.Add(openingJe);

            // Debit: Treasury (asset increase)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = openingJe.Id,
                AccountType = JournalAccountType.Treasury,
                AccountId = treasury.Id,
                Debit = req.OpeningBalance,
                Credit = 0m,
                Description = "رصيد افتتاحي خزينة",
                BranchId = branchId,
            });

            // Credit: OwnerEquity (source of funds)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = openingJe.Id,
                AccountType = JournalAccountType.OwnerEquity,
                AccountId = branchId,
                Debit = 0m,
                Credit = req.OpeningBalance,
                Description = "رصيد افتتاحي — حقوق الملكية",
                BranchId = branchId,
            });
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
        // Sprint 1: Admin branchId fallback
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. يرجى تسجيل الدخول بفرع صالح." });

        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. لا توجد فروع نشطة في النظام." });
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
    /// Migration C: Balance now recalculated from JournalLine (Treasury account type)
    /// instead of CashFlowTransaction. Treasury balance = SUM(Debit) - SUM(Credit)
    /// for all posted JournalLines where AccountType == Treasury and AccountId matches.
    /// In double-entry: Treasury Debit = inflow (increase), Credit = outflow (decrease).
    ///
    /// Fix: Includes a fallback for opening balances created before the JournalEntry
    /// migration. If no opening-balance JournalEntry exists for this treasury, the
    /// opening amount from CashFlowTransaction (ReferenceNumber = "OP-BAL") is added.
    /// </summary>
    [HttpPost("treasuries/{id:guid}/recalculate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RecalculateTreasuryBalance(Guid id)
    {
        var treasury = await db.Treasuries.FindAsync(id);
        if (treasury == null || !treasury.IsActive)
            return NotFound(new { message = "الخزنة غير موجودة" });

        var oldBalance = treasury.Balance;

        // Migration C: Recalculate from JournalLine instead of CashFlowTransaction
        // Balance = SUM(Debit) - SUM(Credit) for Treasury lines pointing to this treasury
        var journalBalance = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.AccountId == treasury.Id
                && l.JournalEntry.IsPosted
                && l.JournalEntry.BranchId == treasury.BranchId)
            .SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0m;

        // Fix: Fallback for opening balances created before the JournalEntry migration.
        // Search specifically for the opening balance JournalEntry for this treasury.
        // Opening balance = VaultTransfer entry with description containing "رصيد افتتاحي"
        // and a Treasury Debit line pointing to this treasury.
        // NOTE: This depends on the description pattern used when creating treasury opening
        // balances. If the description format changes, this check must be updated.
        var hasOpeningJournalEntry = await db.JournalEntries
            .AnyAsync(je => je.FinancialDocumentType == FinancialDocumentType.VaultTransfer
                && je.IsPosted
                && je.Description.Contains("رصيد افتتاحي")
                && je.Lines.Any(l => l.AccountType == JournalAccountType.Treasury
                    && l.AccountId == treasury.Id));

        decimal openingBalanceFromCashFlow = 0m;
        if (!hasOpeningJournalEntry)
        {
            // Legacy treasuries created before Migration C may only have a CashFlowTransaction
            // with ReferenceNumber = "OP-BAL" for the opening balance
            openingBalanceFromCashFlow = await db.CashFlowTransactions
                .Where(c => c.TreasuryId == treasury.Id
                    && c.ReferenceNumber == "OP-BAL"
                    && c.Category == FinancialCategory.InternalTransfer
                    && !c.IsReversal
                    && c.IsActive)
                .SumAsync(c => (decimal?)(c.Type == TransactionType.Inflow ? c.Amount : -c.Amount)) ?? 0m;
        }

        // If an opening JournalEntry exists, journalBalance already includes it.
        // If not (legacy treasury), add the CashFlow opening balance as a correction.
        var calculatedBalance = journalBalance;
        if (!hasOpeningJournalEntry && openingBalanceFromCashFlow != 0m)
        {
            calculatedBalance += openingBalanceFromCashFlow;
            logger.LogInformation(
                "Treasury {TreasuryId}: Applied opening balance fallback from CashFlowTransaction = {Fallback:F2}",
                treasury.Id, openingBalanceFromCashFlow);
        }

        var drift = calculatedBalance - oldBalance;

        if (drift != 0)
            logger.LogWarning("Treasury drift detected for {TreasuryId} ({Name}): Old={OldBalance}, New={NewBalance}, Drift={Drift}", treasury.Id, treasury.Name, oldBalance, calculatedBalance, drift);

        treasury.Balance = calculatedBalance;
        treasury.UpdatedAt = DateTime.UtcNow;

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "تعارض في تحديث رصيد الخزينة. يرجى المحاولة مرة أخرى." }); }

        await audit.LogAsync(AuditAction.Update, "Treasury", id, details: $"Recalculated via V3 (JournalLine): old={oldBalance}, new={calculatedBalance}, drift={drift}, openingFallback={openingBalanceFromCashFlow}");

        return Ok(new { treasury.Id, treasury.Name, OldBalance = oldBalance, NewBalance = calculatedBalance, Drift = drift, DriftDetected = drift != 0, OpeningFallback = openingBalanceFromCashFlow, message = drift != 0 ? $"تم إعادة حساب الرصيد. تم اكتشاف انحراف بمبلغ {drift:N0} ر.ي" : "تم إعادة حساب الرصيد. لا يوجد انحراف" });
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
        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، لا توجد فروع نشطة في النظام. لا يمكن تسجيل المصروف." });

        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل مصروف نقدي." });
        }

        Treasury treasury;
        try { treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, req.PaymentMethod, activeSession?.Id); }
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
                PaidBy = userId, BranchId = branchId,
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
                    PerformedBy = userId, BranchId = branchId,
                    CashierSessionId = activeSession?.Id, TreasuryId = treasury.Id
                };
                db.CashFlowTransactions.Add(cashflow);
                expense.IsPostedToLedger = true;
                expense.CashFlowTransactionId = cashflow.Id;

                // Create JournalEntry manually with IsPosted = true from the start.
                // Same pattern as POST /treasuries — avoids double SaveChanges from CreateEntryAsync.
                var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
                var je = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    FinancialDocumentId = expense.Id,
                    FinancialDocumentType = FinancialDocumentType.Expense,
                    Description = $"قيد مصروف تشغيلي: {expense.Title}",
                    EntryDate = expense.ExpenseDate,
                    BranchId = branchId,
                    PerformedBy = userId,
                    CashierSessionId = activeSession?.Id,
                    TreasuryId = treasury.Id,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    IsReversal = false,
                };
                db.JournalEntries.Add(je);

                // Debit: Expense (expense increase)
                db.JournalLines.Add(new JournalLine
                {
                    JournalEntryId = je.Id,
                    AccountType = JournalAccountType.Expense,
                    AccountId = expense.Id,
                    Debit = expense.Amount,
                    Credit = 0m,
                    Description = $"مصروف: {expense.Title}",
                    BranchId = branchId,
                });

                // Credit: Treasury (cash outflow)
                db.JournalLines.Add(new JournalLine
                {
                    JournalEntryId = je.Id,
                    AccountType = JournalAccountType.Treasury,
                    AccountId = treasury.Id,
                    Debit = 0m,
                    Credit = expense.Amount,
                    Description = $"سداد من: {treasury.Name}",
                    BranchId = branchId,
                });

                await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, expense.PaymentMethod, expense.Amount, activeSession?.Id);
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

            // Create JournalEntry manually with IsPosted = true from the start.
            // Same pattern as POST /treasuries — avoids double SaveChanges from CreateEntryAsync.
            var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
            var je = new JournalEntry
            {
                EntryNumber = entryNumber,
                FinancialDocumentId = expense.Id,
                FinancialDocumentType = FinancialDocumentType.Expense,
                Description = $"قيد مصروف تشغيلي (معتمد): {expense.Title}",
                EntryDate = expense.ExpenseDate,
                BranchId = branchId,
                PerformedBy = userId,
                CashierSessionId = activeSession?.Id,
                TreasuryId = treasury.Id,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                IsReversal = false,
            };
            db.JournalEntries.Add(je);

            // Debit: Expense (expense increase)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Expense,
                AccountId = expense.Id,
                Debit = expense.Amount,
                Credit = 0m,
                Description = $"مصروف معتمد: {expense.Title}",
                BranchId = branchId,
            });

            // Credit: Treasury (cash outflow)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Treasury,
                AccountId = treasury.Id,
                Debit = 0m,
                Credit = expense.Amount,
                Description = $"سداد من: {treasury.Name}",
                BranchId = branchId,
            });

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
    /// Fix: Reads from JournalEntry/JournalLine instead of CashFlowTransaction
    /// for validation checks. CashFlowTransaction reversal (dual-write) is preserved.
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

                // Fix: Read from JournalEntry/JournalLine instead of CashFlowTransaction
                // Include Lines so we can mirror them for the reversal
                var originalJe = await db.JournalEntries
                    .Include(je => je.Lines)
                    .FirstOrDefaultAsync(je => je.FinancialDocumentId == reloaded.Id
                        && je.FinancialDocumentType == FinancialDocumentType.Expense
                        && !je.IsReversal && je.IsPosted);

                if (originalJe == null)
                { await tx.RollbackAsync(); return BadRequest(new { message = "لا يوجد قيد محاسبي مرتبط بهذا المصروف" }); }

                // 1. Check if the session is still open (via JournalEntry.CashierSessionId)
                if (originalJe.CashierSessionId.HasValue)
                {
                    var linkedSession = await db.CashierSessions.FindAsync(originalJe.CashierSessionId.Value);
                    if (linkedSession != null && linkedSession.Status != SessionStatus.Open)
                    { await tx.RollbackAsync(); return BadRequest(new { message = "لا يمكن حذف مصروف مرتبط بوردية مقفلة أو مطابقة. تواصل مع المحاسب." }); }
                }

                // 2. Check if a reversal already exists
                var hasReversal = await db.JournalEntries
                    .AnyAsync(je => je.ReversalOfEntryId == originalJe.Id && je.IsReversal);
                if (hasReversal)
                { await tx.RollbackAsync(); return BadRequest(new { message = "هذا المصروف تم عكسه مسبقاً." }); }

                // 3. Get the original TreasuryId from the Treasury JournalLine
                var treasuryId = await db.JournalLines
                    .Where(l => l.JournalEntryId == originalJe.Id
                        && l.AccountType == JournalAccountType.Treasury)
                    .Select(l => l.AccountId)
                    .FirstOrDefaultAsync();

                if (treasuryId == Guid.Empty)
                { await tx.RollbackAsync(); return BadRequest(new { message = "عذراً، لا يمكن عكس القيد — القيد المحاسبي الأصلي غير مرتبط بخزينة. تواصل مع المحاسب." }); }

                var originalTreasury = await db.Treasuries.FindAsync(treasuryId);
                if (originalTreasury == null || !originalTreasury.IsActive)
                { await tx.RollbackAsync(); return BadRequest(new { message = "عذراً، الخزينة الأصلية غير موجودة أو غير مفعلة. لا يمكن عكس القيد المالي — تواصل مع المحاسب." }); }

                // Create reversal JournalEntry manually with IsPosted = true from the start.
                // Same pattern as POST /treasuries — avoids double/triple SaveChanges from
                // CreateReversalEntryAsync (which calls CreateEntryAsync + separate link save).
                // We already have the original entry with Lines loaded, so we can mirror directly.
                var reversalEntryNumber = await journalEntryService.GenerateEntryNumberAsync();
                var reversalJe = new JournalEntry
                {
                    EntryNumber = reversalEntryNumber,
                    FinancialDocumentId = originalJe.FinancialDocumentId,
                    FinancialDocumentType = originalJe.FinancialDocumentType,
                    Description = $"Reversal of {originalJe.EntryNumber}: حذف مصروف: {reloaded.Title}",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = originalJe.BranchId,
                    PerformedBy = userId,
                    CashierSessionId = originalJe.CashierSessionId,
                    TreasuryId = originalJe.TreasuryId,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    IsReversal = true,
                    ReversalOfEntryId = originalJe.Id,
                };
                db.JournalEntries.Add(reversalJe);

                // Mirror the lines: debit → credit, credit → debit
                foreach (var line in originalJe.Lines)
                {
                    db.JournalLines.Add(new JournalLine
                    {
                        JournalEntryId = reversalJe.Id,
                        AccountType = line.AccountType,
                        AccountId = line.AccountId,
                        Debit = line.Credit,   // swap
                        Credit = line.Debit,   // swap
                        Description = $"Reversal: {line.Description}",
                        BranchId = line.BranchId,
                    });
                }

                // Link the original entry to its reversal
                originalJe.ReversedByEntryId = reversalJe.Id;

                // Dual-write: Create reversal CashFlowTransaction (preserved for backward compatibility)
                CashFlowTransaction? originalCashflow = reloaded.CashFlowTransactionId.HasValue
                    ? await db.CashFlowTransactions.FindAsync(reloaded.CashFlowTransactionId.Value)
                    : await db.CashFlowTransactions.FirstOrDefaultAsync(t => t.ReferenceId == reloaded.Id && t.Category == FinancialCategory.OperationalExpense && t.IsActive);

                if (originalCashflow != null)
                {
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
                        CashierSessionId = originalCashflow.CashierSessionId, TreasuryId = treasuryId
                    };
                    db.CashFlowTransactions.Add(reversalCashflow);
                    originalCashflow.ReversedByTransactionId = reversalCashflow.Id;
                }

                await treasuryResolution.IncrementTreasuryBalanceByTreasuryIdAsync(treasuryId, reloaded.Amount);
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
        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty) return BadRequest(new { message = "عذراً، لا توجد فروع نشطة في النظام. لا يمكن تسجيل فاتورة المورد." });

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

            // Create JournalEntry manually with IsPosted = true from the start.
            // Same pattern as POST /treasuries — avoids double SaveChanges from CreateEntryAsync.
            var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
            var je = new JournalEntry
            {
                EntryNumber = entryNumber,
                FinancialDocumentId = payment.Id,
                FinancialDocumentType = FinancialDocumentType.SupplierPayment,
                Description = $"سداد فاتورة مورد: {bill.BillNumber} — {bill.Supplier?.Name ?? ""}",
                EntryDate = paymentDate,
                BranchId = branchId,
                PerformedBy = userId,
                CashierSessionId = activeSession?.Id,
                TreasuryId = treasury.Id,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                IsReversal = false,
            };
            db.JournalEntries.Add(je);

            // Debit: Payable (reduce supplier liability)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Payable,
                AccountId = bill.SupplierId,
                Debit = req.Amount,
                Credit = 0m,
                Description = $"سداد مستحقات: {bill.Supplier?.Name}",
                BranchId = branchId,
            });

            // Credit: Treasury (cash outflow)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Treasury,
                AccountId = treasury.Id,
                Debit = 0m,
                Credit = req.Amount,
                Description = $"سداد من: {treasury.Name}",
                BranchId = branchId,
            });

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

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sprint 1: Resolves the effective branch ID for the current user.
    /// - Non-admin users: uses their assigned BranchId (must be valid, else Guid.Empty).
    /// - Admin users with a valid BranchId: uses their assigned branch.
    /// - Admin users with no branch (Guid.Empty): falls back to the first active
    ///   branch in the system, allowing admin to perform write operations across branches.
    /// Returns Guid.Empty only if no active branches exist in the system.
    /// </summary>
    private async Task<Guid> ResolveBranchIdAsync()
    {
        // Non-admin: always use their assigned branch (no fallback)
        if (!currentUser.IsAdmin)
            return currentUser.BranchId ?? Guid.Empty;

        // Admin with valid branch: use their assigned branch
        if (currentUser.BranchId.HasValue && currentUser.BranchId.Value != Guid.Empty)
            return currentUser.BranchId.Value;

        // Admin without branch: fallback to first active branch in the system
        var firstBranchId = await db.Branches
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => b.Id)
            .FirstOrDefaultAsync();

        return firstBranchId; // Guid.Empty if no branches exist
    }

    private async Task<decimal> CalculateContractOutstandingAsync(Guid? branchId)
    {
        var query = db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active);

        if (branchId.HasValue)
            query = query.Where(c => c.Patient.BranchId == branchId.Value);

        var contracts = await query.ToListAsync();
        // Sprint 1: Nullable-safe aggregation — use decimal? sum with ?? 0m fallback
        // to prevent overflow or null issues on empty payment collections
        return contracts.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => (decimal?)p.Amount ?? 0m));
    }

    private async Task<decimal> CalculateInvoiceOutstandingAsync(Guid? branchId)
    {
        var query = db.Invoices
            .Include(i => i.Payments)
            .Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);

        if (branchId.HasValue)
            query = query.Where(i => i.Patient.BranchId == branchId.Value);

        var invoices = await query.ToListAsync();
        // Sprint 1: Nullable-safe aggregation
        return invoices.Sum(i => i.TotalAmount - i.Payments.Where(p => p.IsActive).Sum(p => (decimal?)p.Amount ?? 0m));
    }
}

/// <summary>
/// DTO for cancelling an invoice via Finance V3 endpoint.
/// </summary>
public sealed class CancelInvoiceRequest { public string? Notes { get; init; } }
