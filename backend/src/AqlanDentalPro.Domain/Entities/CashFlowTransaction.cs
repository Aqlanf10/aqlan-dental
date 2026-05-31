using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A centralized financial ledger entry tracking any cash inflow or outflow in the clinic.
/// Represents the single source of truth for all cash drawer and bank transactions.
/// </summary>
public class CashFlowTransaction : BaseEntity
{
    /// <summary>Unique sequential transaction reference number (e.g., TX-20260525-0001).</summary>
    public string TransactionNumber { get; set; } = string.Empty;

    /// <summary>Indicates if the money was received (Inflow) or disbursed (Outflow).</summary>
    public TransactionType Type { get; set; }

    /// <summary>Bookkeeping category of this transaction.</summary>
    public FinancialCategory Category { get; set; }

    /// <summary>Transaction amount in YER.</summary>
    public decimal Amount { get; set; }

    /// <summary>Method used: cash, card, or bank_transfer.</summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>Date the transaction occurred.</summary>
    public DateOnly TransactionDate { get; set; }

    /// <summary>Reference to the original subsystem entity ID (e.g., Patient Payment, Salary record, Expense, or Supplier Purchase Order).</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>Original document number (e.g. RCP-20260525-001, PO-002, EXP-003, SAL-004).</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Description explaining the transaction.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The ID of the user who performed this transaction.</summary>
    public Guid PerformedBy { get; set; }

    /// <summary>The ID of the clinic branch where this transaction took place.</summary>
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Optional reference to the cashier drawer session in which this transaction occurred.</summary>
    public Guid? CashierSessionId { get; set; }
    public CashierSession? CashierSession { get; set; }

    /// <summary>FK to the treasury account that received or disbursed the funds.</summary>
    public Guid? TreasuryId { get; set; }
    public Treasury? Treasury { get; set; }

    // ─── Reversal Tracking (C3 — CashFlowTransaction Immutability) ─────────
    // IMPORTANT: CashFlowTransaction entries MUST NEVER be soft-deleted
    // (IsActive must never be set to false). For financial ledger integrity,
    // any correction must be done by creating a reversal CashFlowTransaction
    // with opposite Type and linking via ReversalOfTransactionId.

    /// <summary>Indicates whether this transaction is a reversal of a previous transaction.</summary>
    public bool IsReversal { get; set; } = false;

    /// <summary>FK to the original CashFlowTransaction that this transaction reverses.</summary>
    public Guid? ReversalOfTransactionId { get; set; }
    public CashFlowTransaction? ReversalOfTransaction { get; set; }

    /// <summary>Navigation to the CashFlowTransaction that reverses this transaction.</summary>
    public Guid? ReversedByTransactionId { get; set; }
    public CashFlowTransaction? ReversedByTransaction { get; set; }
}
