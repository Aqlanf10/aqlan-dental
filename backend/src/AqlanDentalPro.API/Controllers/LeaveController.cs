using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateLeaveRequest
{
    public Guid EmployeeId { get; init; }
    public LeaveType LeaveType { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string? Reason { get; init; }
}

public sealed class ApproveLeaveRequest
{
    public bool Approve { get; init; }
    public string? RejectionReason { get; init; }
}

[ApiController]
[Route("api/leaves")]
[Authorize]
public class LeaveController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Get leave requests with filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] LeaveType? leaveType,
        [FromQuery] RequestStatus? status,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.LeaveRequests
            .Include(l => l.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(l => l.EmployeeId == employeeId.Value);

        if (leaveType.HasValue)
            query = query.Where(l => l.LeaveType == leaveType.Value);

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        if (branchId.HasValue)
            query = query.Where(l => l.Employee.BranchId == branchId.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.EmployeeId,
                EmployeeName = l.Employee.FullName,
                LeaveType = l.LeaveType.ToString(),
                l.StartDate,
                l.EndDate,
                l.TotalDays,
                l.Reason,
                Status = l.Status.ToString(),
                l.ApprovedAt,
                l.RejectionReason,
                l.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { data = records, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    /// <summary>
    /// Get leave balance for an employee
    /// </summary>
    [HttpGet("balance/{employeeId:guid}")]
    public async Task<IActionResult> GetLeaveBalance(Guid employeeId)
    {
        var employee = await db.Employees.FindAsync(employeeId);
        if (employee is null)
            return NotFound(new { message = "الموظف غير موجود" });

        var currentYear = DateTime.Now.Year;

        var approvedLeaves = await db.LeaveRequests
            .Where(l => l.EmployeeId == employeeId
                && l.Status == RequestStatus.Approved
                && l.StartDate.Year == currentYear)
            .GroupBy(l => l.LeaveType)
            .Select(g => new
            {
                LeaveType = g.Key.ToString(),
                UsedDays = g.Sum(l => l.TotalDays),
            })
            .ToListAsync();

        // Default annual leave entitlement: 21 days
        var defaultAnnual = 21;
        var usedAnnual = approvedLeaves.FirstOrDefault(l => l.LeaveType == "Annual")?.UsedDays ?? 0;

        return Ok(new
        {
            Year = currentYear,
            DefaultAnnualLeave = defaultAnnual,
            UsedAnnualLeave = usedAnnual,
            RemainingAnnualLeave = defaultAnnual - usedAnnual,
            Breakdown = approvedLeaves,
        });
    }

    /// <summary>
    /// Create leave request
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequest req)
    {
        var employee = await db.Employees.FindAsync(req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        if (req.EndDate < req.StartDate)
            return BadRequest(new { message = "تاريخ الانتهاء يجب أن يكون بعد تاريخ البداية" });

        var totalDays = req.EndDate.DayNumber - req.StartDate.DayNumber + 1;

        // Check for overlapping leaves
        var overlapping = await db.LeaveRequests
            .AnyAsync(l => l.EmployeeId == req.EmployeeId
                && l.Status != RequestStatus.Rejected
                && l.Status != RequestStatus.Cancelled
                && l.StartDate <= req.EndDate
                && l.EndDate >= req.StartDate);

        if (overlapping)
            return Conflict(new { message = "يوجد طلب إجازة متداخل في نفس الفترة" });

        var leave = new LeaveRequest
        {
            EmployeeId = req.EmployeeId,
            LeaveType = req.LeaveType,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            TotalDays = totalDays,
            Reason = req.Reason?.Trim(),
            Status = RequestStatus.Pending,
        };

        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        return Created($"/api/leaves/{leave.Id}", new
        {
            leave.Id,
            leave.EmployeeId,
            EmployeeName = employee.FullName,
            LeaveType = leave.LeaveType.ToString(),
            leave.StartDate,
            leave.EndDate,
            leave.TotalDays,
            leave.Reason,
            Status = leave.Status.ToString(),
        });
    }

    /// <summary>
    /// Approve or reject leave request
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveLeaveRequest req)
    {
        var leave = await db.LeaveRequests.FindAsync(id);
        if (leave is null)
            return NotFound(new { message = "طلب الإجازة غير موجود" });

        if (leave.Status != RequestStatus.Pending)
            return BadRequest(new { message = "تم معالجة هذا الطلب مسبقاً" });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (req.Approve)
        {
            leave.Status = RequestStatus.Approved;
            leave.ApprovedBy = Guid.TryParse(userId, out var uid) ? uid : null;
            leave.ApprovedAt = DateTime.UtcNow;

            // Create attendance records for leave days
            for (var d = leave.StartDate; d <= leave.EndDate; d = d.AddDays(1))
            {
                var existingAttendance = await db.Attendances
                    .AnyAsync(a => a.EmployeeId == leave.EmployeeId && a.Date == d);

                if (!existingAttendance)
                {
                    db.Attendances.Add(new Attendance
                    {
                        EmployeeId = leave.EmployeeId,
                        Date = d,
                        Status = AttendanceStatus.Leave,
                    });
                }
            }
        }
        else
        {
            leave.Status = RequestStatus.Rejected;
            leave.RejectionReason = req.RejectionReason?.Trim();
            leave.ApprovedBy = Guid.TryParse(userId, out var uid) ? uid : null;
            leave.ApprovedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = req.Approve ? "تم الموافقة على الإجازة" : "تم رفض الإجازة",
            Status = leave.Status.ToString(),
        });
    }

    /// <summary>
    /// Cancel leave request
    /// </summary>
    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var leave = await db.LeaveRequests.FindAsync(id);
        if (leave is null)
            return NotFound(new { message = "طلب الإجازة غير موجود" });

        if (leave.Status != RequestStatus.Pending && leave.Status != RequestStatus.Approved)
            return BadRequest(new { message = "لا يمكن إلغاء هذا الطلب" });

        var wasApproved = leave.Status == RequestStatus.Approved;
        leave.Status = RequestStatus.Cancelled;

        // Remove auto-created attendance records when cancelling an approved leave
        if (wasApproved)
        {
            var leaveAttendanceRecords = await db.Attendances
                .Where(a => a.EmployeeId == leave.EmployeeId
                    && a.Status == AttendanceStatus.Leave
                    && a.Date >= leave.StartDate
                    && a.Date <= leave.EndDate)
                .ToListAsync();

            foreach (var attendance in leaveAttendanceRecords)
            {
                attendance.IsActive = false;
                attendance.DeletedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();

        return Ok(new { message = wasApproved ? "تم إلغاء الإجازة وحذف سجلات الحضور المرتبطة" : "تم إلغاء طلب الإجازة" });
    }

    /// <summary>
    /// Delete leave request
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var leave = await db.LeaveRequests.FindAsync(id);
        if (leave is null)
            return NotFound(new { message = "طلب الإجازة غير موجود" });

        leave.IsActive = false;
        leave.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف طلب الإجازة بنجاح" });
    }
}
