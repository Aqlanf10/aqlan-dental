using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Applies patient advances to issued invoices through auditable reclassification entries.
/// It never creates a second cash movement for money already received.
/// </summary>
public interface IAdvancePaymentAllocationService
{
    Task<AdvancePaymentAllocationResult> AllocateAvailableAdvancesAsync(Guid invoiceId, CancellationToken ct = default);
    Task<AdvancePaymentAllocationResult> ReleaseInvoiceAllocationsAsync(Guid invoiceId, CancellationToken ct = default);
    Task<bool> HasActiveAllocationsForPaymentAsync(Guid paymentId, CancellationToken ct = default);
}
