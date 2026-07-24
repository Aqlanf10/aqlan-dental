using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Read-side finance service — extracted from <c>FinanceService</c> as part of TD-021 PR A2.
/// Owns all read-only aggregation queries (statements, summaries, overdue computation).
///
/// This is a pure code move (no logic change). The previous implementation lived in
/// <c>FinanceService.GetAccountStatementAsync</c>, <c>GetSummaryAsync</c>,
/// <c>GetPatientFinanceSummaryAsync</c>, and <c>GetOverdueContractsAsync</c>.
///
/// Shared mapping helpers (<see cref="FinanceMappers.MapPayment"/>,
/// <see cref="FinanceMappers.NormalizeCurrency"/>) were extracted to a separate
/// static class so both FinanceService (write) and FinanceReadService (read) use
/// the same code without duplication.
/// </summary>
public class FinanceReadService(
    AppDbContext db,
    ICurrentUserService currentUser) : IFinanceReadService
{
    /// <summary>
    /// QA-596: Shared helper — computes the sum of Visit.AmountDueReference for
    /// visits that have no linked invoice (via Invoice.VisitId). This represents
    /// performed work that has not been invoiced/contracted and previously
    /// vanished from every balance calculation. Used by GetAccountStatementAsync,
    /// GetSummaryAsync, and any other balance site that needs to reflect
    /// provisional debt from unbilled sessions.
    /// </summary>
    /// <param name="patientId">If provided, scopes to one patient. If null, aggregates across all patients (with optional branch filter).</param>
    /// <param name="branchId">If provided, filters visits via Patient.BranchId.</param>
    private async Task<decimal> GetUnbilledVisitsAmountAsync(Guid? patientId = null, Guid? branchId = null)
    {
        // Get the set of visit IDs that have a linked invoice (those are already
        // counted via invoice totals — must NOT double-count).
        var billedVisitIdsQuery = db.Invoices
            .Where(i => i.VisitId.HasValue && i.IsActive);
        if (patientId.HasValue)
            billedVisitIdsQuery = billedVisitIdsQuery.Where(i => i.PatientId == patientId.Value);
        var billedVisitIds = await billedVisitIdsQuery
            .Select(i => i.VisitId!.Value)
            .ToListAsync();
        var billedVisitSet = billedVisitIds.ToHashSet();

        var unbilledVisitsQuery = db.Visits
            .Where(v => v.IsActive
                     && v.AmountDueReference.HasValue && v.AmountDueReference > 0);
        if (patientId.HasValue)
            unbilledVisitsQuery = unbilledVisitsQuery.Where(v => v.PatientId == patientId.Value);

        var unbilledVisitRows = await unbilledVisitsQuery
            .Include(v => v.Patient)
            .ToListAsync();

        if (branchId.HasValue)
            unbilledVisitRows = unbilledVisitRows.Where(v => v.Patient.BranchId == branchId.Value).ToList();

        return unbilledVisitRows
            .Where(v => !billedVisitSet.Contains(v.Id))
            .Sum(v => v.AmountDueReference ?? 0m);
    }

    public async Task<List<OverdueContractDto>> GetOverdueContractsAsync()
    {
        var today = ClinicTimeProvider.ClinicToday();

        // FIN-13: Project only the fields needed for the overdue calculation + the
        // per-contract PaidAmount (computed in SQL via correlated subquery).
        // Previously this loaded Contracts.Include(c => c.Payments).ToListAsync()
        // which fetched every Payment row for every active installment contract.
        // The month-since-StartDate calc remains in C# (EF can't translate DateOnly
        // year/month arithmetic cleanly) but operates on a tiny projected set.
        var candidates = await db.Contracts
            .Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null)
            .Select(c => new
            {
                c.Id,
                c.PatientId,
                PatientName   = c.Patient.FirstName + " " + c.Patient.LastName,
                PatientNumber = c.Patient.PatientNumber,
                Phone         = c.Patient.Phone,
                c.Specialty,
                c.TotalAmount,
                c.DiscountAmount,
                c.DownPayment,
                c.StartDate,
                c.InstallmentsCount,
                c.InstallmentAmount,
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)
            })
            .ToListAsync();

        var overdue = new List<OverdueContractDto>();

        foreach (var c in candidates)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            if (monthsElapsed <= 0) continue;

            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var overdueAmt   = expectedPaid - c.PaidAmount;

            if (overdueAmt > 0)
            {
                overdue.Add(new OverdueContractDto
                {
                    ContractId     = c.Id,
                    PatientId      = c.PatientId,
                    PatientName    = c.PatientName,
                    PatientNumber  = c.PatientNumber,
                    Phone          = c.Phone,
                    Specialty      = c.Specialty,
                    TotalAmount    = c.TotalAmount,
                    PaidAmount     = c.PaidAmount,
                    OverdueAmount  = overdueAmt,
                    RemainingAmount= c.TotalAmount - c.DiscountAmount - c.PaidAmount,
                    MonthsElapsed  = monthsElapsed,
                    StartDate      = c.StartDate?.ToString("yyyy-MM-dd")
                });
            }
        }

        return overdue.OrderByDescending(o => o.OverdueAmount).ToList();
    }

    public async Task<AccountStatementDto?> GetAccountStatementAsync(Guid patientId)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return null;

        // FIN-13: All summary aggregations now execute server-side as SQL SUM(...)
        // (replaces in-memory .Sum() over ToListAsync-loaded entities). Each query
        // returns a single scalar, so we move less data across the wire.

        // Sprint Patient-Finance-Ledger: Exclude Draft invoices — same as GetPatientFinanceSummaryAsync
        var invoicesPredicate = new Func<IQueryable<Invoice>, IQueryable<Invoice>>(q => q
            .Where(i => i.PatientId == patientId
                     && i.Status != InvoiceStatus.Cancelled
                     && i.Status != InvoiceStatus.Draft
                     && i.IsActive));

        // CORE-PAT-012: same cancelled-contract exclusion as
        // GetPatientFinanceSummaryAsync — the statement and the summary must agree.
        var totalContractedFromContracts = await db.Contracts
            .Where(c => c.PatientId == patientId
                     && c.Status != ContractStatus.Cancelled)
            .SumAsync(c => (decimal?)c.TotalAmount) ?? 0m;

        var totalContractedFromInvoices = await invoicesPredicate(db.Invoices)
            .SumAsync(i => (decimal?)(i.Subtotal + (i.TaxAmount ?? 0m))) ?? 0m;

        var totalContracted = totalContractedFromContracts + totalContractedFromInvoices;

        var totalDiscountsFromContracts = await db.Contracts
            .Where(c => c.PatientId == patientId
                     && c.Status != ContractStatus.Cancelled)
            .SumAsync(c => (decimal?)c.DiscountAmount) ?? 0m;

        var totalDiscountsFromInvoices = await invoicesPredicate(db.Invoices)
            .SumAsync(i => (decimal?)i.DiscountAmount) ?? 0m;

        var totalDiscounts = totalDiscountsFromContracts + totalDiscountsFromInvoices;

        // FIX: Calculate totalPaid from ALL active payments for the patient,
        // not just contract-linked ones. Unlinked/orphan payments must still
        // count in the overall patient summary so the balance is accurate.
        // Each payment is counted exactly once via the direct Payments query.
        var totalPaid = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive)
            .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;

        // QA-596: include unbilled visits so the account statement matches
        // GetPatientFinanceSummaryAsync. Without this, a patient with a 50k
        // session (no invoice) sees TotalRemaining=0 on their statement.
        var unbilledVisitsAmount = await GetUnbilledVisitsAmountAsync(patientId);

        var totalRemaining = Math.Max(0m, totalContracted + unbilledVisitsAmount - totalDiscounts - totalPaid);

        // ── Contracts list with per-contract paid/remaining computed in SQL ──
        // FIN-13: Project ContractStatementDto directly. The correlated subquery
        // `c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)` is translated to
        // `(SELECT COALESCE(SUM(p.Amount), 0) FROM Payments p WHERE p.ContractId = c.Id AND p.IsActive)`
        // — avoids loading any Payment rows into memory.
        var contracts = await db.Contracts
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContractStatementDto
            {
                Id              = c.Id,
                Specialty       = c.Specialty,
                TotalAmount     = c.TotalAmount,
                DiscountAmount  = c.DiscountAmount,
                PaidAmount      = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
                RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
                StartDate       = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd") : null,
                Status          = c.Status.ToString(),
                InstallmentsCount  = c.InstallmentsCount,
                InstallmentAmount  = c.InstallmentAmount
            })
            .ToListAsync();

        var activeContracts     = await db.Contracts.CountAsync(c => c.PatientId == patientId && c.Status == ContractStatus.Active);
        var completedContracts  = await db.Contracts.CountAsync(c => c.PatientId == patientId && c.Status == ContractStatus.Completed);

        // FIX: Filter recentPayments to active only — inactive/refunded/cancelled
        // payments should not appear in the recent list.
        var recentPayments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync();

        return new AccountStatementDto
        {
            PatientId        = patientId,
            PatientName      = patient.FirstName + " " + patient.LastName,
            PatientNumber    = patient.PatientNumber,
            TotalContracted  = totalContracted,
            TotalDiscounts   = totalDiscounts,
            TotalPaid        = totalPaid,
            TotalRemaining   = totalRemaining,
            UnbilledVisitsAmount = unbilledVisitsAmount,
            ActiveContracts  = activeContracts,
            CompletedContracts = completedContracts,
            Contracts        = contracts,
            RecentPayments   = recentPayments.Select(p => FinanceMappers.MapPayment(p)).ToList()
        };
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync()
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var today = ClinicTimeProvider.ClinicToday();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var todayQuery = db.Payments.Where(p => p.PaymentDate == today && p.IsActive && (p.AccountCurrency == null || p.AccountCurrency == "YER"));
        if (branchId.HasValue) todayQuery = todayQuery.Where(p => p.BranchId == branchId);
        var todayCollected = await todayQuery.SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0;

        var monthQuery = db.Payments.Where(p => p.PaymentDate >= monthStart && p.IsActive && (p.AccountCurrency == null || p.AccountCurrency == "YER"));
        if (branchId.HasValue) monthQuery = monthQuery.Where(p => p.BranchId == branchId);
        var monthCollected = await monthQuery.SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0;

        // Contract-based outstanding
        var contractQuery = db.Contracts.Include(c => c.Payments).Where(c => c.Status == ContractStatus.Active);
        if (branchId.HasValue) contractQuery = contractQuery.Where(c => c.Patient.BranchId == branchId);
        var contractOutstanding = await contractQuery
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount))
            .SumAsync(r => (decimal?)r) ?? 0;

        // Invoice-based outstanding (Issued invoices not fully paid)
        var invoiceQuery = db.Invoices.Include(i => i.Payments)
            .Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);
        if (branchId.HasValue) invoiceQuery = invoiceQuery.Where(i => i.Patient.BranchId == branchId);
        var invoiceOutstanding = await invoiceQuery
            .Select(i => i.TotalAmount - i.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount))
            .SumAsync(r => (decimal?)r) ?? 0;

        var activeContractsQuery = db.Contracts.Where(c => c.Status == ContractStatus.Active);
        if (branchId.HasValue) activeContractsQuery = activeContractsQuery.Where(c => c.Patient.BranchId == branchId);
        var activeContracts = await activeContractsQuery.CountAsync();

        // ── Extended Sprint 1 Dashboard Stats ──
        var unpaidInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);
        if (branchId.HasValue) unpaidInvoicesQuery = unpaidInvoicesQuery.Where(i => i.Patient.BranchId == branchId);
        var unpaidInvoicesCount = await unpaidInvoicesQuery.CountAsync();

        var draftInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Draft && i.IsActive);
        if (branchId.HasValue) draftInvoicesQuery = draftInvoicesQuery.Where(i => i.Patient.BranchId == branchId);
        var draftInvoicesCount = await draftInvoicesQuery.CountAsync();

        // Overdue amount
        var overdueContracts = await GetOverdueContractsAsync();
        var overdueAmount = overdueContracts.Sum(o => o.OverdueAmount);

        // Pending doctor commissions (calculated or approved or pending, but not paid)
        var commissionQuery = db.InvoiceLineItems
            .Where(l => l.IsActive && l.CommissionStatus != CommissionStatus.Paid && l.DoctorCommissionAmount > 0);
        if (branchId.HasValue) commissionQuery = commissionQuery.Where(l => l.Invoice.Patient.BranchId == branchId);
        var pendingCommissionsAmount = await commissionQuery.SumAsync(l => l.DoctorCommissionAmount);

        var recentQuery = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.IsActive);
        if (branchId.HasValue) recentQuery = recentQuery.Where(p => p.BranchId == branchId);
        var recentPayments = await recentQuery
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => FinanceMappers.MapPayment(p))
            .ToListAsync();

        // Recent invoices
        var recentInvoicesQuery = db.Invoices
            .Include(i => i.Patient)
            .Where(i => i.IsActive);
        if (branchId.HasValue) recentInvoicesQuery = recentInvoicesQuery.Where(i => i.Patient.BranchId == branchId);
        var recentInvoices = await recentInvoicesQuery
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => new RecentInvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                PatientName = i.Patient != null ? (i.Patient.FirstName + " " + i.Patient.LastName).Trim() : "مريض",
                TotalAmount = i.TotalAmount,
                Status = i.Status.ToString(),
                StatusArabic = i.Status == InvoiceStatus.Draft ? "مسودة" :
                               i.Status == InvoiceStatus.Issued ? "مصدرة" :
                               i.Status == InvoiceStatus.Paid ? "مدفوعة" : "ملغاة",
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        // QA-596: include unbilled visits (sessions with AmountDueReference but no invoice)
        // so the finance dashboard's TotalOutstanding reflects provisional debt.
        var unbilledVisitsAmount = await GetUnbilledVisitsAmountAsync(patientId: null, branchId);

        return new FinanceSummaryDto
        {
            TodayCollected = todayCollected,
            MonthCollected = monthCollected,
            TotalOutstanding = contractOutstanding + invoiceOutstanding + unbilledVisitsAmount,
            ActiveContracts = activeContracts,
            UnpaidInvoicesCount = unpaidInvoicesCount,
            DraftInvoicesCount = draftInvoicesCount,
            OverdueAmount = overdueAmount,
            PendingCommissionsAmount = pendingCommissionsAmount,
            RecentPayments = recentPayments,
            RecentInvoices = recentInvoices
        };
    }

    public async Task<PatientFinanceSummaryDto> GetPatientFinanceSummaryAsync(Guid patientId)
    {
        // ── Contract-based cost (server-side aggregation, FIN-13) ───────────
        // Previously: loaded all contracts (+ Payments collection) into memory then
        //             `contracts.Sum(c => c.TotalAmount - c.DiscountAmount)`.
        // Now:        single SQL `SELECT SUM(TotalAmount - DiscountAmount) FROM Contracts
        //             WHERE PatientId = @patientId` (returns 0 for empty set).
        // CORE-PAT-012: exclude CANCELLED contracts from cost — a cancelled
        // treatment plan is not an obligation (invoices already exclude
        // Cancelled below; contracts inconsistently did not, so a patient with
        // an abandoned plan showed phantom debt). Payments stay counted in
        // totalPaid regardless — received money is received money.
        var contractCost = await db.Contracts
            .Where(c => c.PatientId == patientId
                     && c.Status != ContractStatus.Cancelled)
            .SumAsync(c => (decimal?)(c.TotalAmount - c.DiscountAmount)) ?? 0m;

        // ── Invoice-based financials (new invoice system) ───────────────────
        // Sprint Patient-Finance-Ledger: Exclude Draft invoices from outstanding balance.
        // Draft invoices are not yet committed — only Issued and Paid represent actual obligation.
        // This aligns with FinanceV3 GetPatientBalance which also excludes Drafts.
        var invoiceCost = await db.Invoices
            .Where(i => i.PatientId == patientId
                     && i.Status != InvoiceStatus.Cancelled
                     && i.Status != InvoiceStatus.Draft
                     && i.IsActive)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        // ── QA-594: Unbilled visits cost ───────────────────────────────────
        // Sessions performed without a linked invoice (and no contract) used
        // to disappear from the outstanding balance. Include
        // Visit.AmountDueReference for visits that have no invoice referencing
        // them via Invoice.VisitId, so partial payments on a 50k root-canal
        // session are correctly reflected as a 30k remaining debt.
        var billedVisitIds = await db.Invoices
            .Where(i => i.PatientId == patientId && i.VisitId.HasValue && i.IsActive)
            .Select(i => i.VisitId!.Value)
            .ToListAsync();
        var billedVisitIdsSet = billedVisitIds.ToHashSet();

        var unbilledVisitRows = await db.Visits
            .Where(v => v.PatientId == patientId && v.IsActive
                     && v.AmountDueReference.HasValue && v.AmountDueReference > 0)
            .ToListAsync(); // client-side filter to apply HashSet membership cleanly
        var unbilledVisitsCost = unbilledVisitRows
            .Where(v => !billedVisitIdsSet.Contains(v.Id))
            .Sum(v => v.AmountDueReference ?? 0m);

        // ── Combined totals ─────────────────────────────────────────────────
        var totalCost      = contractCost + invoiceCost + unbilledVisitsCost;
        var totalPaid      = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive)
            .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;
        var outstanding    = Math.Max(0m, totalCost - totalPaid);

        // ── Overdue amount ──────────────────────────────────────────────────
        // FIN-13: Project only the fields needed for the date math + the per-contract
        // paid total (computed in SQL via correlated subquery). Avoids loading the
        // full Payment collection for every contract. The month-since-StartDate calc
        // stays in C# because EF can't translate DateOnly year/month arithmetic cleanly.
        var today          = ClinicTimeProvider.ClinicToday();
        var overdueCandidates = await db.Contracts
            .Where(c => c.PatientId == patientId
                     && c.Status == ContractStatus.Active
                     && c.InstallmentAmount > 0
                     && c.StartDate != null)
            .Select(c => new
            {
                c.StartDate,
                c.InstallmentsCount,
                c.InstallmentAmount,
                c.DownPayment,
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)
            })
            .ToListAsync();

        var overdueAmount  = 0m;
        foreach (var c in overdueCandidates)
        {
            var months   = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            var expected = c.DownPayment + Math.Min(months, c.InstallmentsCount) * (c.InstallmentAmount ?? 0);
            if (expected > c.PaidAmount) overdueAmount += expected - c.PaidAmount;
        }

        var latestPayment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        var totalPaymentsCount = await db.Payments
            .CountAsync(p => p.PatientId == patientId && p.IsActive);

        var activeContractsCount = await db.Contracts
            .CountAsync(c => c.PatientId == patientId && c.Status == ContractStatus.Active);

        var status = totalCost == 0 ? "no_plan"
            : outstanding <= 0 ? "paid"
            : overdueAmount > 0 ? "overdue"
            : "on_track";

        return new PatientFinanceSummaryDto
        {
            TotalTreatmentCost   = totalCost,
            TotalPaid            = totalPaid,
            OutstandingBalance   = outstanding,
            OverdueAmount        = overdueAmount,
            LatestPayment        = latestPayment == null ? null : FinanceMappers.MapPayment(latestPayment),
            FinancialStatus      = status,
            ActiveContractsCount = activeContractsCount,
            TotalPaymentsCount   = totalPaymentsCount,
            UnbilledVisitsAmount = unbilledVisitsCost
        };
    }
}
