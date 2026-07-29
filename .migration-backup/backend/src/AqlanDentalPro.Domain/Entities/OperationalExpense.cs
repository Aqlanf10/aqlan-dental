using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Represents an operational or general clinic expense (e.g., rent, utilities, marketing, maintenance, external lab order fees).
/// </summary>
public class OperationalExpense : BaseEntity
{
    /// <summary>Sequential expense document reference number (e.g., EXP-20260525-001).</summary>
    public string ExpenseNumber { get; set; } = string.Empty;

    /// <summary>Short title describing the expense (e.g., "سداد إيجار العيادة لشهر مايو").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The opex category of this expense.</summary>
    public ExpenseCategory Category { get; set; }

    /// <summary>Total cost in YER.</summary>
    public decimal Amount { get; set; }

    /// <summary>Date the expense was incurred.</summary>
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Payment method used: cash, card, bank_transfer.</summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>Optional reference to a Supplier if the expense was paid to a registered supplier.</summary>
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Optional reference to an external LabOrder if this expense paid for dental lab work.</summary>
    public Guid? LabOrderId { get; set; }
    public LabOrder? LabOrder { get; set; }

    public string? Notes { get; set; }

    /// <summary>Scanned invoice/receipt document URL.</summary>
    public string? ReceiptAttachmentUrl { get; set; }

    /// <summary>ID of the user/admin who registered and paid the expense.</summary>
    public Guid PaidBy { get; set; }

    /// <summary>ID of the branch responsible for this expense.</summary>
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    // --- Sprint 22: Tiered Expenditure Approval ---

    /// <summary>
    /// Approval status. Expenses above the configured threshold (default: 50,000 YER)
    /// are auto-placed in Pending until a manager/admin approves.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;

    /// <summary>ID of the manager/admin who approved or rejected this expense.</summary>
    public Guid? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }

    /// <summary>Timestamp when the approval/rejection decision was made.</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Reason for rejection if the expense was rejected by management.</summary>
    public string? ApprovalNotes { get; set; }

    /// <summary>
    /// Whether this expense has been posted to the central CashFlow ledger (GL).
    /// Only approved or NotRequired expenses are posted.
    /// </summary>
    public bool IsPostedToLedger { get; set; } = false;

    /// <summary>FK to the resulting CashFlowTransaction ledger entry (null until posted).</summary>
    public Guid? CashFlowTransactionId { get; set; }
    public CashFlowTransaction? CashFlowTransaction { get; set; }
}
