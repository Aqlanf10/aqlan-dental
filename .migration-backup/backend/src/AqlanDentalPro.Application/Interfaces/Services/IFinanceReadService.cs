using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Read-side finance service — extracted from <see cref="IFinanceService"/> as part of
/// TD-021 PR A2 (god-service extraction). Owns all read-only aggregation queries:
/// account statements, finance dashboard summary, per-patient finance summary, and
/// the overdue-contracts computation.
///
/// Behaviour-preserving move: the implementation is byte-for-byte identical to the
/// previous FinanceService methods. No business rule changes; only the host type changed.
///
/// Slicing rationale (see docs/technical-debt/TD-021-god-service-extraction-plan.md):
/// - Read-only: no cashier-shift gating, no treasury mutation, no journal posting.
/// - Self-contained: deps are db + currentUser only (for branch filtering).
/// - Shared helpers (MapPayment, NormalizeCurrency) extracted to FinanceMappers so
///   both FinanceService (write) and FinanceReadService (read) use the same code.
/// - GetOverdueContractsAsync moved here too because it is read-only and used
///   internally by GetSummaryAsync (avoids a cross-service dependency).
/// </summary>
public interface IFinanceReadService
{
    /// <summary>
    /// Returns the patient's account statement: total contracted (contracts + invoices),
    /// total discounts, total paid, total remaining, recent payments, and per-contract
    /// paid/remaining breakdown. Includes unbilled visits (QA-596).
    /// </summary>
    Task<AccountStatementDto?> GetAccountStatementAsync(Guid patientId);

    /// <summary>
    /// Returns the finance dashboard summary: today/month collections, outstanding
    /// (contracts + invoices + unbilled visits), active contracts count, unpaid/draft
    /// invoice counts, overdue amount, pending commissions, and recent payments/invoices.
    /// Branch-scoped for non-admin users.
    /// </summary>
    Task<FinanceSummaryDto> GetSummaryAsync();

    /// <summary>
    /// Returns the per-patient finance summary: total treatment cost (contracts + invoices
    /// + unbilled visits), total paid, outstanding balance, overdue amount, financial
    /// status (no_plan/paid/overdue/on_track), latest payment, and contract/payment counts.
    /// </summary>
    Task<PatientFinanceSummaryDto> GetPatientFinanceSummaryAsync(Guid patientId);

    /// <summary>
    /// Returns all active installment contracts where the expected paid amount
    /// (down payment + elapsed installments) exceeds the actual paid amount.
    /// Used by the dashboard summary and the overdue report.
    /// </summary>
    Task<List<OverdueContractDto>> GetOverdueContractsAsync();
}
