using AqlanDentalPro.Application.DTOs.Commission;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface ICommissionService
{
    // ── Line item commission ──────────────────────────────────────────────────

    Task<LineItemCommissionDto?> GetLineItemCommissionAsync(Guid lineItemId);
    Task<List<LineItemCommissionDto>> GetInvoiceCommissionsAsync(Guid invoiceId);

    /// <summary>Recalculates and persists commission for a single line item.</summary>
    Task<LineItemCommissionDto?> RecalculateAsync(Guid lineItemId);

    /// <summary>Updates cost fields and recalculates commission. Blocked if Approved.</summary>
    Task<LineItemCommissionDto?> UpdateCostsAsync(Guid lineItemId, UpdateLineItemCommissionRequest req, Guid updatedBy);

    /// <summary>Approves commission — locks further editing for non-admins.</summary>
    Task<LineItemCommissionDto?> ApproveAsync(Guid lineItemId, ApproveCommissionRequest req, Guid approvedBy);

    /// <summary>Admin unlock: reverts Approved → Calculated so costs can be edited again.</summary>
    Task<LineItemCommissionDto?> UnlockAsync(Guid lineItemId, Guid unlockedBy);

    // ── Commission report ─────────────────────────────────────────────────────

    Task<CommissionReportResponse> GetReportAsync(
        DateOnly from, DateOnly to,
        Guid? doctorId, Guid? branchId,
        string? serviceCategory, string? commissionStatus, string? paymentStatus);

    // ── Commission payment disbursement ───────────────────────────────────────

    Task<DoctorCommissionPaymentDto> RecordPaymentAsync(RecordCommissionPaymentRequest req, Guid recordedBy);
    Task<List<DoctorCommissionPaymentDto>> GetPaymentsAsync(Guid? doctorId);

    // ── Service commission defaults ───────────────────────────────────────────

    Task<ServiceCommissionDefaultsDto?> GetServiceDefaultsAsync(Guid serviceId);
    Task<ServiceCommissionDefaultsDto?> UpdateServiceDefaultsAsync(Guid serviceId, UpdateServiceCommissionDefaultsRequest req);

    // ── Auto-fill from service defaults ──────────────────────────────────────

    /// <summary>
    /// Called when a service is added to an invoice line item.
    /// Fills MaterialCost, LabCost, CommissionPercentage from service defaults.
    /// </summary>
    Task AutoFillFromServiceAsync(Guid lineItemId);

    /// <summary>Returns the DoctorId for a given UserId (for role-based scoping).</summary>
    Task<Guid?> GetDoctorIdForUserAsync(Guid userId);
}
