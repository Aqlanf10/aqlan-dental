namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A single installment payment made towards a supplier bill.
/// Each record reduces SupplierBill.PaidAmount and auto-posts a CashFlowTransaction.
/// </summary>
public class SupplierBillPayment : BaseEntity
{
    public Guid SupplierBillId { get; set; }
    public SupplierBill? SupplierBill { get; set; }

    /// <summary>Amount paid in this installment (YER).</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency of the physical disbursement: YER, SAR, or USD.</summary>
    public string Currency { get; set; } = "YER";

    /// <summary>Immutable YER conversion snapshot used when this payment was posted.</summary>
    public decimal ExchangeRateToYer { get; set; } = 1m;

    /// <summary>Rate source: same_currency, manual, or settings.</summary>
    public string ExchangeRateSource { get; set; } = "same_currency";

    /// <summary>Payment method: cash, card, bank_transfer.</summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>Date this installment was paid.</summary>
    public DateOnly PaymentDate { get; set; }

    /// <summary>Optional bank reference / receipt number from external bank.</summary>
    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    /// <summary>The user who recorded this payment.</summary>
    public Guid PaidBy { get; set; }

    /// <summary>FK to the resulting CashFlowTransaction ledger entry.</summary>
    public Guid? CashFlowTransactionId { get; set; }
    public CashFlowTransaction? CashFlowTransaction { get; set; }
}
