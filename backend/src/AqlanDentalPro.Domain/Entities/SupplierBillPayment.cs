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
