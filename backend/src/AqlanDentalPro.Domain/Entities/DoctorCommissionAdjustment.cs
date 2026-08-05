using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// CORE-FIN-LAB-ADJ: a standalone correction line on a doctor's commission, raised when the
/// ACTUAL cost behind an already-PAID commission turns out to differ from the cost that was
/// used when it was paid.
/// <para>
/// A paid commission is history: its <see cref="InvoiceLineItem"/> is never rewritten, because
/// the doctor was handed money against those exact numbers and the ledger already carries the
/// disbursement. The correction therefore lives here, as a separate signed line that joins the
/// doctor's next settlement — positive when the clinic still owes the doctor, negative when the
/// doctor was overpaid — carrying a full reference back to the lab order, the invoice and the
/// original commission line so the owner can see exactly why it exists.
/// </para>
/// <para>
/// Unpaid commissions never produce a row here: they are simply recalculated in place, which is
/// both cheaper and more honest than accumulating corrections against a number nobody has acted on.
/// </para>
/// </summary>
public class DoctorCommissionAdjustment : BaseEntity
{
    /// <summary>Doctor whose settlement carries this line.</summary>
    public Guid DoctorId { get; set; }

    /// <summary>Branch that earned the original commission.</summary>
    public Guid BranchId { get; set; }

    /// <summary>Currency of every monetary amount on this correction.</summary>
    public string Currency { get; set; } = "YER";

    /// <summary>The already-paid commission line this corrects.</summary>
    public Guid InvoiceLineItemId { get; set; }

    /// <summary>Denormalised for reporting and for the settlement print-out.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Lab order whose cost changed, when that is what triggered the correction.</summary>
    public Guid? LabOrderId { get; set; }

    /// <summary>Doctor commission frozen on the line item at the time it was paid.</summary>
    public decimal PaidCommissionAmount { get; set; }

    /// <summary>Doctor commission the line item WOULD carry using the cost known now.</summary>
    public decimal RecalculatedCommissionAmount { get; set; }

    /// <summary>
    /// Signed correction: positive means the clinic owes the doctor more, negative means
    /// the doctor was overpaid. Successive corrections on the same line item accumulate,
    /// so the sum of all non-cancelled rows always closes the gap exactly once.
    /// </summary>
    public decimal AdjustmentAmount { get; set; }

    /// <summary>Lab cost baked into the paid commission.</summary>
    public decimal PreviousLabCost { get; set; }

    /// <summary>Lab cost the order actually carries now, in the invoice's currency.</summary>
    public decimal CurrentLabCost { get; set; }

    /// <summary>Arabic explanation shown on the settlement screen and the print-out.</summary>
    public string Reason { get; set; } = string.Empty;

    public CommissionAdjustmentStatus Status { get; set; } = CommissionAdjustmentStatus.Pending;

    /// <summary>Disbursement that first carried this line to the doctor.</summary>
    public Guid? SettledByPaymentId { get; set; }

    public DateOnly? SettledOn { get; set; }

    /// <summary>User whose action produced the correction (null when raised by a background sweep).</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Administrator who voided the line, with the reason kept in <see cref="CancelReason"/>.</summary>
    public Guid? CancelledByUserId { get; set; }

    public string? CancelReason { get; set; }
}
