namespace AqlanDentalPro.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid? ContractId { get; set; }

    /// <summary>Linked invoice (optional — payment may be linked to a contract, an invoice, or standalone).</summary>
    public Guid? InvoiceId { get; set; }

    public Guid PatientId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code of the physical amount received (YER, SAR, USD).</summary>
    public string? Currency { get; set; }

    /// <summary>Currency of the patient account/invoice/contract that this payment is applied to.</summary>
    public string AccountCurrency { get; set; } = "YER";

    /// <summary>Exchange rate from Currency to AccountCurrency captured at payment time.</summary>
    public decimal ExchangeRateToAccountCurrency { get; set; } = 1m;

    /// <summary>Amount applied to the patient balance in AccountCurrency.</summary>
    public decimal AppliedAmount { get; set; }

    /// <summary>Optional source label for the exchange rate snapshot (manual, settings, ai_suggested).</summary>
    public string? ExchangeRateSource { get; set; }
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
