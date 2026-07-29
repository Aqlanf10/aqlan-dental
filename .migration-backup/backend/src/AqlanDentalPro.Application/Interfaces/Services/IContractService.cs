using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Contract service — extracted from the former FinanceService god-service across
/// TD-021 PRs A3 (reads + clean writes) and A4 final slice (the two orchestrators:
/// CreateContractAsync with its auto down-payment via IPaymentService, and
/// UpdateContractStatusAsync whose cancellation path reverses linked payments).
/// Behaviour-preserving moves throughout — no business rule changes.
/// IFinanceService/FinanceService were retired once this interface absorbed them.
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

    /// <summary>
    /// Creates a contract and, when DownPayment &gt; 0, auto-creates the down payment
    /// via IPaymentService (requires an open cashier session, same as any payment).
    /// </summary>
    Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req);

    /// <summary>
    /// Transitions a contract's status. Cancellation atomically soft-deletes linked
    /// payments, writes CashFlow + JournalEntry reversals, reverses treasury balances,
    /// and re-evaluates affected invoice statuses. Returns null for an unknown
    /// contract or an unparsable status.
    /// </summary>
    Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status);
}
