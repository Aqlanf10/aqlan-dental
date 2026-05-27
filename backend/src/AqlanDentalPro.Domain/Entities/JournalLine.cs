using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// JournalLine — a single debit or credit line within a JournalEntry.
/// Every JournalEntry must have at least two JournalLines where:
///   SUM(Debit) = SUM(Credit)
///
/// Debit and Credit are mutually exclusive per line:
///   - A debit line has Debit > 0 and Credit = 0
///   - A credit line has Credit > 0 and Debit = 0
///
/// This is the final accounting source of truth in Finance V3.
/// </summary>
public class JournalLine : BaseEntity
{
    /// <summary>FK to the parent JournalEntry.</summary>
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    /// <summary>
    /// The type of account this line posts to.
    /// Determines whether the amount is an asset, liability, revenue, or expense.
    /// </summary>
    public JournalAccountType AccountType { get; set; }

    /// <summary>
    /// FK to the specific account instance.
    /// For Treasury: points to a Treasury.Id
    /// For Receivable: points to a Patient.Id
    /// For Payable: points to a Supplier.Id or Employee.Id
    /// For Revenue/Expense: points to a category or cost center
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>Debit amount (zero if this is a credit line).</summary>
    public decimal Debit { get; set; }

    /// <summary>Credit amount (zero if this is a debit line).</summary>
    public decimal Credit { get; set; }

    /// <summary>Optional line-level description.</summary>
    public string? Description { get; set; }

    /// <summary>Branch this line belongs to. REQUIRED — never Guid.Empty.</summary>
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
}
