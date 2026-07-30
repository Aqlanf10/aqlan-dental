namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.DoctorCommissionAdjustment"/>.
/// <para>
/// IMPORTANT — this status is presentational, not arithmetical. A doctor's outstanding
/// balance sums every adjustment that is not <see cref="Cancelled"/>, whether it is
/// <see cref="Pending"/> or <see cref="Settled"/>, because <c>Settled</c> only records
/// which disbursement first showed the line to the doctor. Making the balance depend on
/// the flag would double-count a settled adjustment or drop a partially paid one.
/// </para>
/// </summary>
public enum CommissionAdjustmentStatus
{
    /// <summary>Raised, and not yet included in any disbursement shown to the doctor.</summary>
    Pending = 0,

    /// <summary>Carried on a recorded commission disbursement (<c>SettledByPaymentId</c>).</summary>
    Settled = 1,

    /// <summary>Voided by an administrator; excluded from the doctor's balance.</summary>
    Cancelled = 2,
}
