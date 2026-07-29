namespace AqlanDentalPro.Domain.Entities;

using AqlanDentalPro.Domain.Enums;

/// <summary>
/// Sprint 15 — Advance payment (سلف) for employees.
/// Tracks advance requests, approvals, and salary deduction.
/// </summary>
public class AdvancePayment : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int? DeductFromMonth { get; set; }
    public int? DeductFromYear { get; set; }
    public bool IsDeducted { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
}
