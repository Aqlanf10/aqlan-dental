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
}
