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
    DateTime CreatedAt);

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
    List<Guid>? LineItemIds);

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
    string Status);

public record CommissionReportSummary(
    decimal TotalGross,
    decimal TotalDiscount,
    decimal TotalMaterialCost,
    decimal TotalLabCost,
    decimal TotalOtherCosts,
    decimal TotalNet,
    decimal TotalDoctorCommission,
    decimal TotalPaid,
    decimal TotalRemaining);

public record CommissionReportResponse(
    CommissionReportSummary Summary,
    List<CommissionReportRow> Rows);

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
    DateTime CreatedAt);

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
