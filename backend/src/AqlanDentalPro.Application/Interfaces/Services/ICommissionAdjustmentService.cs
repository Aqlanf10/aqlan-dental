using AqlanDentalPro.Application.DTOs.Commission;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// CORE-FIN-LAB-ADJ: keeps doctor commissions honest when the ACTUAL lab cost behind them
/// changes after the fact.
/// <para>
/// The split is deliberate and is the whole point of this service: an unpaid commission is
/// recalculated in place, while a PAID commission is never rewritten — it receives a separate
/// signed correction line that joins the doctor's next settlement. Running any method here
/// repeatedly must converge, never accumulate.
/// </para>
/// </summary>
public interface ICommissionAdjustmentService
{
    /// <summary>
    /// Brings every commission line that draws its cost from <paramref name="labOrderId"/> back
    /// in step with what that order actually costs now.
    /// </summary>
    /// <remarks>
    /// Neither opens a transaction NOR calls SaveChanges — the caller owns the unit of work, so
    /// the lab order edit and the commission correction it caused commit together or not at all.
    /// </remarks>
    Task<CommissionResyncResult> ResyncLabOrderAsync(
        Guid labOrderId, Guid? performedBy, CancellationToken ct = default);

    /// <summary>
    /// Admin sweep over historical records: re-syncs every commission line already linked to a
    /// lab order. Only touches lines whose link is unambiguous — it never invents a link.
    /// </summary>
    /// <remarks>Same unit-of-work contract as <see cref="ResyncLabOrderAsync"/>.</remarks>
    Task<CommissionResyncResult> ResyncAllLinkedAsync(
        Guid? doctorId, Guid? performedBy, CancellationToken ct = default);

    /// <summary>Correction lines, newest first. <paramref name="status"/> is optional.</summary>
    Task<List<CommissionAdjustmentDto>> GetAdjustmentsAsync(
        Guid? doctorId, string? status, CancellationToken ct = default);
    Task<List<CommissionAdjustmentDto>> GetAdjustmentsAsync(
        Guid? doctorId, string? status, CancellationToken ct,
        Guid? branchId, string? currency);

    /// <summary>What the clinic owes a doctor right now, corrections included.</summary>
    Task<DoctorSettlementSummaryDto?> GetSettlementSummaryAsync(
        Guid doctorId, CancellationToken ct = default);
    Task<DoctorSettlementSummaryDto?> GetSettlementSummaryAsync(
        Guid doctorId, CancellationToken ct, Guid? branchId, string? currency);

    /// <summary>
    /// Voids a correction line so it stops counting toward the doctor's balance. The row stays
    /// for audit; only <see cref="Domain.Enums.CommissionAdjustmentStatus.Cancelled"/> rows are
    /// excluded from the arithmetic.
    /// </summary>
    Task<CommissionAdjustmentDto?> CancelAdjustmentAsync(
        Guid adjustmentId, string reason, Guid cancelledBy, CancellationToken ct = default);
}
