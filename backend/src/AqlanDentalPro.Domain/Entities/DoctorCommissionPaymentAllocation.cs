namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Append-only allocation of an actual doctor disbursement to the commission
/// line or positive correction that it settles. Exactly one target is required.
/// </summary>
public class DoctorCommissionPaymentAllocation : BaseEntity
{
    public Guid PaymentId { get; set; }
    public Guid? InvoiceLineItemId { get; set; }
    public Guid? CommissionAdjustmentId { get; set; }
    public decimal Amount { get; set; }

    public DoctorCommissionPayment Payment { get; set; } = null!;
    public InvoiceLineItem? InvoiceLineItem { get; set; }
    public DoctorCommissionAdjustment? CommissionAdjustment { get; set; }
}
