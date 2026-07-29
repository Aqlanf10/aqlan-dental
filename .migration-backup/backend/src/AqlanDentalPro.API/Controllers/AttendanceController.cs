using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateAttendanceRequest
{
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }
    public TimeSpan? CheckIn { get; init; }
    public TimeSpan? CheckOut { get; init; }
    public AttendanceStatus Status { get; init; } = AttendanceStatus.Present;
    public string? Notes { get; init; }
}

public sealed class CheckInRequest
{
    public Guid EmployeeId { get; init; }
}

public sealed class CheckOutRequest
{
    public Guid EmployeeId { get; init; }
}

[ApiController]
[Route("api/attendance")]
[Authorize(Policy = "StaffOnly")]
public class AttendanceController(AppDbContext db, ILogger<AttendanceController> logger) : ControllerBase
{
    /// <summary>
    /// Get attendance records with filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] DateOnly? date,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] AttendanceStatus? status,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 31)
    {
        try
        {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 31;

        var query = db.Attendances
            .Include(a => a.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        if (date.HasValue)
            query = query.Where(a => a.Date == date.Value);

        if (year.HasValue)
            query = query.Where(a => a.Date.Year == year.Value);

        if (month.HasValue)
            query = query.Where(a => a.Date.Month == month.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (branchId.HasValue)
            query = query.Where(a => a.Employee.BranchId == branchId.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(a => a.Date)
            .ThenBy(a => a.Employee.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.EmployeeId,
                EmployeeName = a.Employee.FullName,
                a.Date,
                CheckIn = a.CheckIn != null ? a.CheckIn.Value.ToString(@"hh\:mm") : (string?)null,
                CheckOut = a.CheckOut != null ? a.CheckOut.Value.ToString(@"hh\:mm") : (string?)null,
                Status = a.Status.ToString(),
                a.Notes,
                a.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { data = records, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetAll attendance failed");
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل البيانات" });
        }
    }

    /// <summary>
    /// Get attendance summary for a month
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? branchId)
    {
        var query = db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.Date.Year == year && a.Date.Month == month);

        if (branchId.HasValue)
            query = query.Where(a => a.Employee.BranchId == branchId.Value);

        var records = await query.ToListAsync();

        var employeeIds = records.Select(a => a.EmployeeId).Distinct();

        var summary = employeeIds.Select(empId =>
        {
            var empRecords = records.Where(a => a.EmployeeId == empId).ToList();
            var emp = empRecords.First().Employee;
            return new
            {
                EmployeeId = empId,
                EmployeeName = emp.FullName,
                TotalDays = empRecords.Count,
                PresentDays = empRecords.Count(a => a.Status == AttendanceStatus.Present),
                AbsentDays = empRecords.Count(a => a.Status == AttendanceStatus.Absent),
                LateDays = empRecords.Count(a => a.Status == AttendanceStatus.Late),
                HalfDays = empRecords.Count(a => a.Status == AttendanceStatus.HalfDay),
                LeaveDays = empRecords.Count(a => a.Status == AttendanceStatus.Leave),
                HolidayDays = empRecords.Count(a => a.Status == AttendanceStatus.Holiday),
                TotalWorkHours = empRecords
                    .Where(a => a.CheckIn.HasValue && a.CheckOut.HasValue)
                    .Sum(a => (a.CheckOut!.Value - a.CheckIn!.Value).TotalHours),
            };
        }).ToList();

        return Ok(summary);
    }

    /// <summary>
    /// Create attendance record
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateAttendanceRequest req)
    {
        // Check employee exists
        var employee = await db.Employees.FindAsync(req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        // Check duplicate
        var exists = await db.Attendances
            .AnyAsync(a => a.EmployeeId == req.EmployeeId && a.Date == req.Date);
        if (exists)
            return Conflict(new { message = "يوجد سجل حضور لهذا الموظف في هذا التاريخ بالفعل" });

        var attendance = new Attendance
        {
            EmployeeId = req.EmployeeId,
            Date = req.Date,
            CheckIn = req.CheckIn,
            CheckOut = req.CheckOut,
            Status = req.Status,
            Notes = req.Notes?.Trim(),
        };

        db.Attendances.Add(attendance);
        await db.SaveChangesAsync();

        return Created($"/api/attendance/{attendance.Id}", new
        {
            attendance.Id,
            attendance.EmployeeId,
            EmployeeName = employee.FullName,
            attendance.Date,
            CheckIn = attendance.CheckIn?.ToString(@"hh\:mm"),
            CheckOut = attendance.CheckOut?.ToString(@"hh\:mm"),
            Status = attendance.Status.ToString(),
            attendance.Notes,
        });
    }

    /// <summary>
    /// Quick check-in for today
    /// </summary>
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest req)
    {
        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.UserId == req.EmployeeId || e.Id == req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = DateTime.Now.TimeOfDay;

        var existing = await db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employee.Id && a.Date == today);

        if (existing is not null)
        {
            if (existing.CheckIn.HasValue)
                return Conflict(new { message = "تم تسجيل الحضور مسبقاً لهذا اليوم" });
            existing.CheckIn = now;
            existing.Status = AttendanceStatus.Present;
            await db.SaveChangesAsync();
            return Ok(new { message = "تم تسجيل الحضور بنجاح", checkIn = now.ToString(@"hh\:mm") });
        }

        var attendance = new Attendance
        {
            EmployeeId = employee.Id,
            Date = today,
            CheckIn = now,
            Status = AttendanceStatus.Present,
        };
        db.Attendances.Add(attendance);
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تسجيل الحضور بنجاح", checkIn = now.ToString(@"hh\:mm") });
    }

    /// <summary>
    /// Quick check-out for today
    /// </summary>
    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest req)
    {
        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.UserId == req.EmployeeId || e.Id == req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = DateTime.Now.TimeOfDay;

        var existing = await db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employee.Id && a.Date == today);

        if (existing is null)
            return BadRequest(new { message = "لم يتم تسجيل الحضور بعد لهذا اليوم" });

        if (existing.CheckOut.HasValue)
            return Conflict(new { message = "تم تسجيل الانصراف مسبقاً لهذا اليوم" });

        existing.CheckOut = now;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تسجيل الانصراف بنجاح", checkOut = now.ToString(@"hh\:mm") });
    }

    /// <summary>
    /// Update attendance record
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateAttendanceRequest req)
    {
        var attendance = await db.Attendances.FindAsync(id);
        if (attendance is null)
            return NotFound(new { message = "سجل الحضور غير موجود" });

        attendance.Date = req.Date;
        attendance.CheckIn = req.CheckIn;
        attendance.CheckOut = req.CheckOut;
        attendance.Status = req.Status;
        attendance.Notes = req.Notes?.Trim();

        await db.SaveChangesAsync();

        return Ok(new { message = "تم تحديث سجل الحضور بنجاح" });
    }

    /// <summary>
    /// Delete attendance record
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var attendance = await db.Attendances.FindAsync(id);
        if (attendance is null)
            return NotFound(new { message = "سجل الحضور غير موجود" });

        attendance.IsActive = false;
        attendance.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف سجل الحضور بنجاح" });
    }
}
