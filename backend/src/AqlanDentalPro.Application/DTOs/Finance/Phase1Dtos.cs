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

    /// <summary>Optional treasury ID to refund from. If null, auto-resolved by payment method.</summary>
    public Guid? TreasuryId { get; set; }

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

    /// <summary>Optional treasury ID to pay from. If null, auto-resolved by payment method and branch.</summary>
    public Guid? TreasuryId { get; set; }

    /// <summary>Immutable YER conversion snapshot for this disbursement.</summary>
    public decimal? ExchangeRateToYer { get; set; }

    /// <summary>Source of the exchange rate: manual or settings.</summary>
    public string? ExchangeRateSource { get; set; }

    /// <summary>Optional bank reference / receipt number.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Canonical references created when a supplier payment is posted. They allow
/// the UI to take the user directly to the journal entry and its disbursement
/// voucher without guessing from timestamps or descriptions.
/// </summary>
public sealed record SupplierPaymentPostingResult(
    Guid PaymentId,
    Guid JournalEntryId,
    string JournalEntryNumber);

/// <summary>
/// Request to register a new supplier bill (accounts payable entry).
/// Increases what the clinic owes the supplier by TotalAmount.
/// </summary>
public class SupplierBillCreateRequest
{
    /// <summary>Supplier bill reference number (e.g., "INV-2026-045").</summary>
    public string BillNumber { get; set; } = string.Empty;

    /// <summary>Total invoice amount in YER as stated by the supplier.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Due date for full payment (for credit terms).</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Optional link to an external lab order this bill pays for.</summary>
    public Guid? LabOrderId { get; set; }
}
