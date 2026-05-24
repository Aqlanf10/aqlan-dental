namespace AqlanDentalPro.Domain.Entities;

using AqlanDentalPro.Domain.Enums;

/// <summary>
/// Sprint 15 — Attendance tracking for employees.
/// Records daily check-in/check-out times and attendance status.
/// </summary>
public class Attendance : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Notes { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
}
