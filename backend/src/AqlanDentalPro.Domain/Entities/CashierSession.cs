using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Models a cashier's drawer daily shift or session.
/// Payments made by patients must be linked to an open session for security and anti-fraud lock controls.
/// </summary>
public class CashierSession : BaseEntity
{
    /// <summary>Unique sequential session number (e.g., CS-20260525-01).</summary>
    public string SessionNumber { get; set; } = string.Empty;

    /// <summary>The cashier or receptionist running this shift.</summary>
    public Guid CashierId { get; set; }
    public User Cashier { get; set; } = null!;

    /// <summary>Branch ID where the cash drawer is located.</summary>
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Shift opening timestamp.</summary>
    public DateTime OpeningTime { get; set; }

    /// <summary>Shift closing timestamp.</summary>
    public DateTime? ClosingTime { get; set; }

    /// <summary>Initial cash balance in the drawer at the start of the shift.</summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Sum of all cash payments received during the shift, calculated by the system.</summary>
    public decimal ExpectedClosingCash { get; set; }

    /// <summary>Actual cash counted physically by the cashier at closing.</summary>
    public decimal? ActualClosingCash { get; set; }

    /// <summary>Sum of card payments received during the shift, calculated by the system.</summary>
    public decimal ExpectedClosingCard { get; set; }

    /// <summary>Actual card total reported from POS terminals at closing.</summary>
    public decimal? ActualClosingCard { get; set; }

    /// <summary>Sum of bank transfer payments received during the shift, calculated by the system.</summary>
    public decimal ExpectedClosingBank { get; set; }

    /// <summary>Actual bank transfer total reported at closing.</summary>
    public decimal? ActualClosingBank { get; set; }

    /// <summary>Difference: Actual total - Expected total (can be negative for shortages or positive for surpluses).</summary>
    public decimal? ShortageOrSurplus { get; set; }

    /// <summary>Current state of the session: Open, Closed, or Reconciled.</summary>
    public SessionStatus Status { get; set; } = SessionStatus.Open;

    public string? Notes { get; set; }

    /// <summary>FK to the treasury (cash drawer) this session is operating against.</summary>
    public Guid? TreasuryId { get; set; }
    public Treasury? Treasury { get; set; }

    // Navigation properties for bidirectional relationships
    public ICollection<CashFlowTransaction> Transactions { get; set; } = [];
}
