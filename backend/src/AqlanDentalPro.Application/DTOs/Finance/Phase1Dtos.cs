namespace AqlanDentalPro.Application.DTOs.Finance;

/// <summary>
/// Request to create a new Credit Note against a patient invoice.
/// </summary>
public class CreateCreditNoteRequest
{
    public Guid InvoiceId { get; set; }
    public Guid PatientId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// Request to process a refund for an approved Credit Note.
/// The refund payment is created and the credit note is marked as Refunded.
/// </summary>
public class ProcessRefundRequest
{
    /// <summary>Payment method for the refund: cash, card, bank_transfer.</summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>Optional notes for the refund transaction.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Request to pay (partially or fully) a supplier bill.
/// Creates a SupplierBillPayment, CashFlowTransaction, and journal entry.
/// </summary>
public class PaySupplierBillRequest
{
    /// <summary>Amount to pay in this installment (YER). Must be positive and not exceed remaining balance.</summary>
    public decimal Amount { get; set; }

    /// <summary>Payment method: cash, card, bank_transfer.</summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>Optional bank reference / receipt number.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; set; }
}
