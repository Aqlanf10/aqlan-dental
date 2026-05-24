using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateAdvancePaymentRequest
{
    public Guid EmployeeId { get; init; }
    public decimal Amount { get; init; }
    public string? Reason { get; init; }
    public int? DeductFromMonth { get; init; }
    public int? DeductFromYear { get; init; }
}

public sealed class ApproveAdvanceRequest
{
    public bool Approve { get; init; }
    public string? RejectionReason { get; init; }
}

[ApiController]
[Route("api/advances")]
[Authorize]
public class AdvancePaymentController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Get advance payment records with filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] RequestStatus? status,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.AdvancePayments
            .Include(a => a.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (branchId.HasValue)
            query = query.Where(a => a.Employee.BranchId == branchId.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(a => a.RequestDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.EmployeeId,
                EmployeeName = a.Employee.FullName,
                a.Amount,
                a.Reason,
                a.RequestDate,
                Status = a.Status.ToString(),
                a.ApprovedAt,
                a.RejectionReason,
                a.DeductFromMonth,
                a.DeductFromYear,
                a.IsDeducted,
                a.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { data = records, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    /// <summary>
    /// Create advance payment request
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateAdvancePaymentRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { message = "مبلغ السلفة يجب أن يكون أكبر من صفر" });

        var employee = await db.Employees.FindAsync(req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        var advance = new AdvancePayment
        {
            EmployeeId = req.EmployeeId,
            Amount = req.Amount,
            Reason = req.Reason?.Trim(),
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Pending,
            DeductFromMonth = req.DeductFromMonth,
            DeductFromYear = req.DeductFromYear,
        };

        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        return Created($"/api/advances/{advance.Id}", new
        {
            advance.Id,
            advance.EmployeeId,
            EmployeeName = employee.FullName,
            advance.Amount,
            advance.Reason,
            advance.RequestDate,
            Status = advance.Status.ToString(),
            advance.DeductFromMonth,
            advance.DeductFromYear,
        });
    }

    /// <summary>
    /// Approve or reject advance payment
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveAdvanceRequest req)
    {
        var advance = await db.AdvancePayments.FindAsync(id);
        if (advance is null)
            return NotFound(new { message = "طلب السلفة غير موجود" });

        if (advance.Status != RequestStatus.Pending)
            return BadRequest(new { message = "تم معالجة هذا الطلب مسبقاً" });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (req.Approve)
        {
            advance.Status = RequestStatus.Approved;
            advance.ApprovedBy = Guid.TryParse(userId, out var uid) ? uid : null;
            advance.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            advance.Status = RequestStatus.Rejected;
            advance.RejectionReason = req.RejectionReason?.Trim();
            advance.ApprovedBy = Guid.TryParse(userId, out var uid) ? uid : null;
            advance.ApprovedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = req.Approve ? "تم الموافقة على السلفة" : "تم رفض السلفة",
            Status = advance.Status.ToString(),
        });
    }

    /// <summary>
    /// Delete advance payment
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var advance = await db.AdvancePayments.FindAsync(id);
        if (advance is null)
            return NotFound(new { message = "طلب السلفة غير موجود" });

        advance.IsActive = false;
        advance.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف طلب السلفة بنجاح" });
    }
}
