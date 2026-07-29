namespace AqlanDentalPro.Application.DTOs.Finance;

/// <summary>Outcome of applying recorded patient advances to one invoice.</summary>
public sealed record AdvancePaymentAllocationResult(
    Guid InvoiceId,
    decimal AllocatedAmount,
    int AllocationCount,
    decimal RemainingInvoiceAmount,
    string Currency,
    bool SkippedDueToUnresolvedRefunds = false);
