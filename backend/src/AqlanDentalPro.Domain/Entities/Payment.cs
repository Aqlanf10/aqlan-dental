namespace AqlanDentalPro.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid? ContractId { get; set; }

    /// <summary>Linked invoice (optional — payment may be linked to a contract, an invoice, or standalone).</summary>
    public Guid? InvoiceId { get; set; }

    public Guid PatientId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code of the payment amount (YER, SAR, USD).
    /// Null = YER (legacy/default). Treasury/dashboard YER sums filter YER-only
    /// (Currency == null || Currency == "YER") to avoid mixing currencies.
    /// Foreign-currency payments are recorded + shown on receipts but excluded
    /// from YER totals — no exchange rates; the owner tracks them separately.</summary>
    public string? Currency { get; set; }
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? PaymentMethod { get; set; }
    public string? Specialty { get; set; }
    public string? ServiceDescription { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ReceivedBy { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }

    public Contract? Contract { get; set; }
    public Invoice? Invoice { get; set; }
    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public Branch? Branch { get; set; }
    public Receipt? Receipt { get; set; }
}
