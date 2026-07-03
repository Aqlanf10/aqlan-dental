using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Contract service — extracted from <see cref="IFinanceService"/> as part of
/// TD-021 PR A3 (god-service extraction). Owns the read + clean-write side of
/// the contracts cluster.
///
/// Behaviour-preserving move: the implementation is byte-for-byte identical to the
/// previous FinanceService methods. No business rule changes; only the host type changed.
///
/// Slicing rationale (see docs/technical-debt/TD-021-god-service-extraction-plan.md):
/// - This PR moves only the self-contained contract methods.
/// - <c>CreateContractAsync</c> stays in FinanceService because it calls
///   <c>CreatePaymentAsync</c> (down payment) — a cross-cluster dependency that
///   will be resolved when PR A4 extracts PaymentService.
/// - <c>UpdateContractStatusAsync</c> stays in FinanceService because the
///   cancellation path calls payment-side helpers (<c>UpdateTreasuryBalanceNoSaveAsync</c>,
///   <c>DualWriteReversalEntryAsync</c>, <c>TryMarkInvoicePaidAsync</c>) — these
///   will move together with the PaymentService cluster in PR A4.
/// - <c>TryReconcileContractStatusAsync</c> stays in FinanceService because it is
///   called from <c>CreatePaymentAsync</c>, <c>DeletePaymentAsync</c>, and
///   <c>RefundPaymentAsync</c> — it will move with the payment cluster in PR A4.
/// </summary>
public interface IContractService
{
    /// <summary>
    /// Returns a paginated list of contracts with per-contract paid/remaining amounts.
    /// Branch-scoped for non-admin users. Optional patient + status filters.
    /// </summary>
    Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status);

    /// <summary>
    /// Returns the full contract detail including payments list and package info.
    /// Returns null if the contract does not exist.
    /// </summary>
    Task<ContractDetailDto?> GetContractByIdAsync(Guid id);

    /// <summary>
    /// Updates a contract's mutable fields (specialty, currency, total amount,
    /// installments, dates, discount, notes, package link). Validates that
    /// TotalAmount is not reduced below what's already been paid. Returns the
    /// updated contract detail, or null if the contract does not exist.
    /// </summary>
    Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req);
}
