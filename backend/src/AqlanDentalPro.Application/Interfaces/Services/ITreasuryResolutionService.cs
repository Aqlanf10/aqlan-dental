using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Centralized treasury resolution service for Finance V3.
/// Routes payments to the correct treasury based on payment method:
/// - cash → CashierSession's treasury (or vault treasury if no session)
/// - card / bank / bank_transfer → Bank treasury
///
/// All financial write paths MUST use this service to ensure:
/// 1. Cash payments go to the physical cash drawer (Vault treasury)
/// 2. Card/bank payments go to the bank account treasury
/// 3. Treasury.Balance is atomically mutated in the same transaction
/// 4. CashierSession is linked for cash transactions
/// </summary>
public interface ITreasuryResolutionService
{
    /// <summary>
    /// Resolves the correct treasury for a given branch and payment method.
    /// For cash payments: requires an open CashierSession and uses its TreasuryId.
    /// For card/bank payments: uses the configured bank treasury.
    ///
    /// Throws ArgumentException with Arabic message if resolution fails.
    /// Does NOT call SaveChangesAsync — the caller persists all changes together.
    /// </summary>
    Task<Treasury> ResolveTreasuryAsync(
        Guid branchId,
        string? paymentMethod,
        Guid? cashierSessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically decrements the resolved treasury balance for an outflow.
    /// Uses raw SQL on relational databases for concurrency safety.
    /// Does NOT call SaveChangesAsync — the caller persists all changes together.
    /// </summary>
    Task DecrementTreasuryBalanceAsync(
        Guid branchId,
        string? paymentMethod,
        decimal amount,
        Guid? cashierSessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically increments the resolved treasury balance for a reversal/refund inflow.
    /// Uses raw SQL on relational databases for concurrency safety.
    /// Does NOT call SaveChangesAsync — the caller persists all changes together.
    /// </summary>
    Task IncrementTreasuryBalanceAsync(
        Guid branchId,
        string? paymentMethod,
        decimal amount,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically increments the balance of a specific treasury by its ID.
    /// Used for reversals where the exact original treasury must be restored.
    /// Rejects with Arabic error if the treasury cannot be found or is inactive.
    /// Uses raw SQL on relational databases for concurrency safety.
    /// Does NOT call SaveChangesAsync — the caller persists all changes together.
    /// </summary>
    Task IncrementTreasuryBalanceByTreasuryIdAsync(
        Guid treasuryId,
        decimal amount,
        CancellationToken ct = default);
}
