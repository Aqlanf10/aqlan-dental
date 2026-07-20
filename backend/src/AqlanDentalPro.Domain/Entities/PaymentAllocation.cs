namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Auditable application of a previously received patient advance to an issued invoice.
/// The source payment remains immutable; this record and its journal entry reclassify
/// the amount from PatientAdvance to PatientReceivable without moving cash again.
/// </summary>
public class PaymentAllocation : BaseEntity
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid BranchId { get; set; }

    /// <summary>Amount applied in the invoice/account currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 account currency shared by the payment and invoice.</summary>
    public string Currency { get; set; } = "YER";

    /// <summary>The posted reclassification journal entry for this allocation.</summary>
    public Guid? JournalEntryId { get; set; }

    public string? Notes { get; set; }

    public Payment Payment { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public JournalEntry? JournalEntry { get; set; }
}
