using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Domain.Interfaces;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Represents a physical transfer of cash or digital funds between two treasuries.
/// E.g., transferring YER cash from a receptionist's drawer to the clinic's main vault.
/// </summary>
public class VaultTransfer : BaseEntity, ISoftDeletable
{
    /// <summary>Unique sequential transfer code (e.g., TR-20260525-001).</summary>
    public string TransferNumber { get; set; } = string.Empty;

    /// <summary>The source treasury the money is moved from.</summary>
    public Guid? SourceTreasuryId { get; set; }
    public Treasury? SourceTreasury { get; set; }

    /// <summary>The destination treasury the money is deposited to.</summary>
    public Guid DestinationTreasuryId { get; set; }
    public Treasury DestinationTreasury { get; set; } = null!;

    /// <summary>The cashier drawer daily session this transfer originates from (optional).</summary>
    public Guid? CashierSessionId { get; set; }
    public CashierSession? CashierSession { get; set; }

    /// <summary>The amount transferred in Yemeni Riyals (YER).</summary>
    public decimal Amount { get; set; }

    /// <summary>The timestamp of the transfer creation.</summary>
    public DateTime TransferDate { get; set; }

    /// <summary>The user who created/initiated the transfer (usually a receptionist or cashier).</summary>
    public Guid PerformedBy { get; set; }
    public User PerformedByUser { get; set; } = null!;

    /// <summary>The accountant or administrator who approved the physical custody/receipt of funds.</summary>
    public Guid? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }

    /// <summary>The timestamp of the approval.</summary>
    public DateTime? ApprovalDate { get; set; }

    /// <summary>Current status of the transfer: Pending, Approved, or Rejected.</summary>
    public TransferStatus Status { get; set; } = TransferStatus.Pending;

    /// <summary>Reconciliation notes or comments (e.g., discrepancy explanation).</summary>
    public string? Notes { get; set; }
}
