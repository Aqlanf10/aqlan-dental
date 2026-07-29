namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPdfService
{
    /// <summary>
    /// Generates a PDF receipt for a payment.
    /// Returns PDF bytes.
    /// </summary>
    Task<byte[]> GeneratePaymentReceiptAsync(Guid paymentId);

    /// <summary>
    /// Generates a PDF financial statement for a patient.
    /// Returns PDF bytes.
    /// </summary>
    Task<byte[]> GenerateFinancialStatementAsync(Guid patientId);

    /// <summary>
    /// Generates a PDF invoice with Arabic/RTL support.
    /// Returns PDF bytes.
    /// </summary>
    Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId);

    /// <summary>
    /// Generates a quarter-A4 disbursement voucher (سند صرف) PDF for an
    /// operational expense. Returns PDF bytes.
    /// </summary>
    Task<byte[]> GenerateExpenseDisbursementVoucherAsync(Guid expenseId);

    /// <summary>
    /// Generates a quarter-A4 disbursement voucher for any posted outflow cash-flow
    /// transaction. Returns PDF bytes.
    /// </summary>
    Task<byte[]> GenerateCashFlowDisbursementVoucherAsync(Guid cashFlowTransactionId);

    /// <summary>
    /// Generates a quarter-A4 disbursement voucher from a posted JournalEntry that
    /// credits a treasury account. Returns PDF bytes.
    /// </summary>
    Task<byte[]> GenerateJournalEntryDisbursementVoucherAsync(Guid journalEntryId);
}
