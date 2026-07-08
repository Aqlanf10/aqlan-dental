using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Payment service — extracted from the former IFinanceService as part of
/// TD-021 PR A4 (slice 2). Owns the payment cluster: reads, create (atomic
/// dual-write to CashFlow/Treasury/JournalEntry), metadata update, delete
/// (reversal entries), refund (partial/full with cumulative idempotency guard),
/// and the invoice-status reconciliation payments drive.
/// </summary>
public interface IPaymentService
{
    Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId);
    Task<PaymentDto?> GetPaymentByIdAsync(Guid id);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req);
    Task<PaymentDto?> UpdatePaymentAsync(Guid id, UpdatePaymentRequest req);
    Task<bool> DeletePaymentAsync(Guid id);
    Task<PaymentDto?> RefundPaymentAsync(Guid id, string? reason, decimal? partialAmount = null);

    /// <summary>
    /// Re-evaluates an invoice's Issued/Paid status against its active payments
    /// (both directions). Safe to call after payment creation, deletion, or refund.
    /// </summary>
    Task TryMarkInvoicePaidAsync(Guid invoiceId);
}
