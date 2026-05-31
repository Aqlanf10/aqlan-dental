using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// JournalEntry — the atomic posting unit in the Finance V3 double-entry bookkeeping model.
/// Each financial event produces one JournalEntry with at least two JournalLines
/// (debits and credits) that must sum to zero before posting.
///
/// INVARIANT: SUM(JournalLine.Debit) = SUM(JournalLine.Credit) per entry.
/// This is validated before IsPosted is set to true.
///
/// JournalEntry replaces CashFlowTransaction as the final accounting source of truth.
/// CashFlowTransaction remains as a transitional/operational table during the rebuild.
/// </summary>
public class JournalEntry : BaseEntity
{
    /// <summary>Unique sequential identifier (e.g., JE-20260526-001).</summary>
    public string EntryNumber { get; set; } = string.Empty;

    /// <summary>FK to the source financial document (Payment, Expense, Invoice, etc.).</summary>
    public Guid FinancialDocumentId { get; set; }

    /// <summary>Discriminator indicating the type of source document.</summary>
    public FinancialDocumentType FinancialDocumentType { get; set; }

    /// <summary>Human-readable description of the transaction.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Date the financial event occurred.</summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>Branch where this entry was created. REQUIRED — never Guid.Empty.</summary>
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Link to the active cashier session (null only for non-session events).</summary>
    public Guid? CashierSessionId { get; set; }
    public CashierSession? CashierSession { get; set; }

    /// <summary>Primary treasury account affected by this entry.</summary>
    public Guid? TreasuryId { get; set; }
    public Treasury? Treasury { get; set; }

    /// <summary>User who created this entry. REQUIRED — never Guid.Empty.</summary>
    public Guid PerformedBy { get; set; }
    public User? PerformedByUser { get; set; }

    // ─── Reversal Tracking ──────────────────────────────────────────────────

    /// <summary>True if this entry reverses a previous entry.</summary>
    public bool IsReversal { get; set; } = false;

    /// <summary>FK to the original entry that this entry reverses (if this is a reversal).</summary>
    public Guid? ReversalOfEntryId { get; set; }
    public JournalEntry? ReversalOfEntry { get; set; }

    /// <summary>FK to the reversal entry that reverses this entry (if this was reversed).</summary>
    public Guid? ReversedByEntryId { get; set; }
    public JournalEntry? ReversedByEntry { get; set; }

    // ─── Posting Status ─────────────────────────────────────────────────────

    /// <summary>True once the entry has been validated and all lines balance.</summary>
    public bool IsPosted { get; set; } = false;

    /// <summary>Timestamp when the entry was posted. Null if not yet posted.</summary>
    public DateTime? PostedAt { get; set; }

    // ─── Navigation ─────────────────────────────────────────────────────────

    /// <summary>All debit/credit lines for this journal entry.</summary>
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();

    // ─── Computed Validation ─────────────────────────────────────────────────

    /// <summary>
    /// Validates the double-entry balancing invariant:
    /// SUM(Lines.Debit) must equal SUM(Lines.Credit).
    /// Returns true if balanced, false otherwise.
    /// </summary>
    public bool IsBalanced()
    {
        var totalDebit = Lines.Sum(l => l.Debit);
        var totalCredit = Lines.Sum(l => l.Credit);
        return Math.Abs(totalDebit - totalCredit) < 0.005m; // tolerance for decimal precision
    }
}
