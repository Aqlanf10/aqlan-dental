using AqlanDentalPro.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AqlanDentalPro.Application.DTOs.Commission;

// ── Line-item commission view ─────────────────────────────────────────────────

public record LineItemCommissionDto(
    Guid LineItemId,
    Guid InvoiceId,
    string InvoiceNumber,
    string PatientName,
    string ServiceName,
    Guid? DoctorId,
    string? DoctorName,
    decimal TotalPrice,
    decimal LineDiscountAmount,
    decimal MaterialCost,
    decimal LabCost,
    decimal OtherDirectCost,
    decimal NetCommissionableAmount,
    decimal DoctorCommissionPercentage,
    decimal DoctorCommissionAmount,
    decimal CenterShareAmount,
    string CommissionStatus,
    string? CommissionNotes,
    bool HasLabOrder,
    bool LabCostMissing,
    bool IsApproved,
    DateTime? CommissionApprovedAt,
    DateTime CreatedAt,
    Guid BranchId = default,
    string Currency = "");

// ── Patch commission costs ────────────────────────────────────────────────────

public record UpdateLineItemCommissionRequest(
    decimal? MaterialCost,
    decimal? LabCost,
    decimal? OtherDirectCost,
    decimal? DoctorCommissionPercentage,
    CommissionBaseRule? CommissionBaseRule,
    Guid? DoctorId,
    string? CommissionNotes);

// ── Approve / pay ─────────────────────────────────────────────────────────────

public record ApproveCommissionRequest(string? Notes);

public record RecordCommissionPaymentRequest(
    [Required] Guid DoctorId,
    [Required] decimal Amount,
    [Required] DateOnly PaymentDate,
    string? PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    /// <summary>When provided, marks these line items as Paid.</summary>
    List<Guid>? LineItemIds,
    [Required] Guid BranchId = default,
    [Required, StringLength(3)] string Currency = "");

// ── Report ────────────────────────────────────────────────────────────────────

public record CommissionReportRow(
    DateTime Date,
    string PatientName,
    string InvoiceNumber,
    string ServiceName,
    string? DoctorName,
    decimal GrossAmount,
    decimal Discount,
    decimal MaterialCost,
    decimal LabCost,
    decimal OtherCosts,
    decimal NetCommissionableAmount,
    decimal DoctorPercentage,
    decimal DoctorCommission,
    decimal PaidCommission,
    decimal RemainingCommission,
    string Status,
    Guid BranchId = default,
    string Currency = "");

public record CommissionReportSummary(
    decimal TotalGross,
    decimal TotalDiscount,
    decimal TotalMaterialCost,
    decimal TotalLabCost,
    decimal TotalOtherCosts,
    decimal TotalNet,
    decimal TotalDoctorCommission,
    decimal TotalPaid,
    decimal TotalRemaining,
    Guid BranchId = default,
    string Currency = "");

public record CommissionReportResponse(
    List<CommissionReportSummary> Summaries,
    List<CommissionReportRow> Rows)
{
    public CommissionReportResponse(CommissionReportSummary summary, List<CommissionReportRow> rows)
        : this([summary], rows) { }
}

// ── Doctor commission payment DTO ─────────────────────────────────────────────

public record DoctorCommissionPaymentDto(
    Guid Id,
    Guid DoctorId,
    string? DoctorName,
    decimal Amount,
    DateOnly PaymentDate,
    string? PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    DateTime CreatedAt,
    Guid BranchId = default,
    string Currency = "");

// ── Commission adjustments (CORE-FIN-LAB-ADJ) ─────────────────────────────────

/// <summary>
/// A signed correction line on an already-PAID commission. Positive means the clinic still
/// owes the doctor; negative means the doctor was overpaid and the amount comes off the next
/// settlement. Never rewrites the original commission.
/// </summary>
public record CommissionAdjustmentDto(
    Guid Id,
    Guid DoctorId,
    string? DoctorName,
    Guid InvoiceLineItemId,
    Guid InvoiceId,
    string InvoiceNumber,
    string PatientName,
    string ServiceName,
    Guid? LabOrderId,
    string? LabOrderNumber,
    decimal PaidCommissionAmount,
    decimal RecalculatedCommissionAmount,
    decimal AdjustmentAmount,
    decimal PreviousLabCost,
    decimal CurrentLabCost,
    string Reason,
    string Status,
    Guid? SettledByPaymentId,
    DateOnly? SettledOn,
    DateTime CreatedAt,
    Guid BranchId = default,
    string Currency = "");

/// <summary>
/// Outcome of re-syncing commissions against the real lab cost. <paramref name="Recalculated"/>
/// counts unpaid commissions corrected in place; <paramref name="AdjustmentsRaised"/> counts
/// paid commissions that received a separate correction line instead.
/// </summary>
public record CommissionResyncResult(
    int LineItemsExamined,
    int Recalculated,
    int AdjustmentsRaised,
    decimal NetAdjustmentAmount,
    List<string> Skipped);

public record CancelCommissionAdjustmentRequest([Required] string? Reason);

/// <summary>Doctor-level settlement position including correction lines.</summary>
public record DoctorSettlementSummaryDto(
    Guid DoctorId,
    string? DoctorName,
    decimal EarnedFromLineItems,
    decimal AdjustmentsTotal,
    decimal TotalDue,
    decimal AlreadyPaid,
    decimal Remaining,
    int PendingAdjustmentCount,
    Guid BranchId = default,
    string Currency = "");

// ── Service commission defaults ───────────────────────────────────────────────

public record ServiceCommissionDefaultsDto(
    Guid ServiceId,
    decimal DefaultMaterialCost,
    string DefaultMaterialCostType,
    decimal DefaultLabCost,
    decimal? DefaultDoctorCommissionPercentage,
    string CommissionBaseRule,
    string CommissionRecognitionMode);

public record UpdateServiceCommissionDefaultsRequest(
    decimal DefaultMaterialCost,
    MaterialCostType DefaultMaterialCostType,
    decimal DefaultLabCost,
    decimal? DefaultDoctorCommissionPercentage,
    CommissionBaseRule CommissionBaseRule,
    CommissionRecognitionMode CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection);
