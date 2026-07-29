namespace AqlanDentalPro.Domain.Entities;

using AqlanDentalPro.Domain.Enums;

/// <summary>
/// Sprint 15 — Leave request (إجازة) for employees.
/// Tracks leave type, duration, and approval workflow.
/// </summary>
public class LeaveRequest : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TotalDays { get; set; }
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
}
