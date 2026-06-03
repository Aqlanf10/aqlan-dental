using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/lab-payables")]
[Authorize(Policy = "StaffOnly")]
public class LabPayablesController(AppDbContext db, ILogger<LabPayablesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? labId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.LabPayables
            .Include(p => p.Lab)
            .Include(p => p.LabOrder)
            .AsQueryable();

        if (labId.HasValue) query = query.Where(p => p.LabId == labId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

        var total = await query.CountAsync();
        var payables = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.LabOrderId,
                LabName = p.Lab.Name,
                OrderNumber = p.LabOrder.OrderNumber,
                PatientName = p.LabOrder.Patient.FirstName + " " + p.LabOrder.Patient.LastName,
                p.Amount,
                p.PaidAmount,
                Balance = p.Amount - p.PaidAmount,
                p.Status,
                DueDate = p.DueDate != null ? p.DueDate.Value.ToString("yyyy-MM-dd") : null,
                p.Notes,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = payables, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var payable = await db.LabPayables
            .Include(p => p.Lab)
            .Include(p => p.LabOrder)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payable is null) return NotFound(new { message = "المديونية غير موجودة" });

        return Ok(new
        {
            payable.Id,
            payable.LabOrderId,
            LabName = payable.Lab.Name,
            OrderNumber = payable.LabOrder.OrderNumber,
            payable.Amount,
            payable.PaidAmount,
            Balance = payable.Amount - payable.PaidAmount,
            payable.Status,
            DueDate = payable.DueDate?.ToString("yyyy-MM-dd"),
            payable.Notes,
            CreatedAt = payable.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    public sealed class RecordPaymentRequest
    {
        public decimal Amount { get; init; }
        public string? Notes { get; init; }
    }

    [HttpPost("{id:guid}/record-payment")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest req)
    {
        var payable = await db.LabPayables.FindAsync(id);
        if (payable is null) return NotFound(new { message = "المديونية غير موجودة" });

        if (req.Amount <= 0) return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });
        if (req.Amount > payable.Amount - payable.PaidAmount)
            return BadRequest(new { message = "المبلغ يتجاوز الرصيد المتبقي" });

        payable.PaidAmount += req.Amount;
        payable.Status = payable.PaidAmount >= payable.Amount ? "paid" : "partial";
        if (req.Notes != null) payable.Notes = req.Notes;

        await db.SaveChangesAsync();
        logger.LogInformation("LabPayable payment recorded: {Id} — {Amount}", id, req.Amount);

        return Ok(new { payable.Id, payable.PaidAmount, payable.Status, Balance = payable.Amount - payable.PaidAmount });
    }
}
