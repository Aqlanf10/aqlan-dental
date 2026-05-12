using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateLabOrderRequest
{
    public Guid PatientId { get; init; }
    public Guid? OrthoCaseId { get; init; }
    public string ApplianceType { get; init; } = string.Empty;
    public string? LabName { get; init; }
    public string? SentDate { get; init; }
    public string? ExpectedDate { get; init; }
    public string Priority { get; init; } = "normal";
    public string? Instructions { get; init; }
    public decimal? Cost { get; init; }
    public Guid? DoctorId { get; init; }
}

public sealed class CreateLabOrderRequestValidator : AbstractValidator<CreateLabOrderRequest>
{
    private static readonly HashSet<string> ValidPriorities = ["urgent", "normal", "low"];

    public CreateLabOrderRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("المريض مطلوب");
        RuleFor(x => x.ApplianceType).NotEmpty().WithMessage("نوع الجهاز مطلوب").MaximumLength(200);
        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p)).WithMessage("الأولوية غير صالحة");
        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("التكلفة يجب أن تكون صفراً أو أكثر")
            .When(x => x.Cost.HasValue);
        RuleFor(x => x.SentDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الإرسال غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.SentDate));
        RuleFor(x => x.ExpectedDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الاستلام المتوقع غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.ExpectedDate));
    }
}

public sealed class UpdateLabOrderStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? ReceivedDate { get; init; }
}

[ApiController]
[Route("api/lab-orders")]
[Authorize(Policy = "StaffOnly")]
public class LabOrdersController(AppDbContext db, ICurrentUserService currentUser, INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? patientId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.OrthoCase)
            .Include(l => l.Doctor)
            .AsQueryable();

        if (patientId.HasValue)  query = query.Where(l => l.PatientId == patientId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.OrderNumber,
                l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                OrthoCaseNumber = l.OrthoCase != null ? l.OrthoCase.CaseNumber : null,
                l.ApplianceType,
                l.LabName,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                l.Status,
                l.Priority,
                l.Cost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = orders, total, page, pageSize });
    }

    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount()
    {
        var count = await db.LabOrders
            .CountAsync(l => l.Status == "sent" || l.Status == "manufacturing");
        return Ok(new { count });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.OrthoCase)
            .Include(l => l.Doctor)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.PatientId,
            PatientName = order.Patient.FirstName + " " + order.Patient.LastName,
            PatientNumber = order.Patient.PatientNumber,
            OrthoCaseNumber = order.OrthoCase?.CaseNumber,
            order.ApplianceType,
            order.LabName,
            SentDate = order.SentDate?.ToString("yyyy-MM-dd"),
            ExpectedDate = order.ExpectedDate?.ToString("yyyy-MM-dd"),
            ReceivedDate = order.ReceivedDate?.ToString("yyyy-MM-dd"),
            order.Status,
            order.Priority,
            order.Instructions,
            order.Cost,
            DoctorName = order.Doctor?.Name,
            CreatedAt = order.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLabOrderRequest req)
    {
        var year = DateTime.UtcNow.Year;
        var count = await db.LabOrders.IgnoreQueryFilters()
            .CountAsync(l => l.OrderNumber != null && l.OrderNumber.StartsWith($"LAB-{year}-"));

        var order = new LabOrder
        {
            PatientId     = req.PatientId,
            OrthoCaseId   = req.OrthoCaseId,
            OrderNumber   = $"LAB-{year}-{(count + 1):D3}",
            ApplianceType = req.ApplianceType,
            LabName       = req.LabName,
            SentDate      = !string.IsNullOrWhiteSpace(req.SentDate)
                ? DateOnly.Parse(req.SentDate) : DateOnly.FromDateTime(DateTime.Today),
            ExpectedDate  = !string.IsNullOrWhiteSpace(req.ExpectedDate)
                ? DateOnly.Parse(req.ExpectedDate) : null,
            Priority      = req.Priority,
            Instructions  = req.Instructions,
            Cost          = req.Cost,
            DoctorId      = req.DoctorId ?? currentUser.UserId,
            Status        = "sent"
        };

        db.LabOrders.Add(order);
        await db.SaveChangesAsync();

        // Notify admin and reception about the new lab order
        _ = Task.Run(async () =>
        {
            try
            {
                var msg = $"طلب مختبر جديد {order.OrderNumber} — {req.ApplianceType}";
                await notifications.NotifyRoleAsync("Admin", "lab", "طلب مختبر جديد", msg, "LabOrder", order.Id);
                await notifications.NotifyRoleAsync("Reception", "lab", "طلب مختبر جديد", msg, "LabOrder", order.Id);
            }
            catch { /* non-blocking */ }
        });

        return CreatedAtAction(nameof(GetById), new { id = order.Id },
            new { order.Id, order.OrderNumber });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateLabOrderStatusRequest req)
    {
        var validStatuses = new HashSet<string> { "sent", "manufacturing", "ready", "received", "cancelled" };
        if (!validStatuses.Contains(req.Status))
            return BadRequest(new { message = "الحالة غير صالحة" });

        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        order.Status = req.Status;
        if (req.Status == "received" && !string.IsNullOrWhiteSpace(req.ReceivedDate))
            order.ReceivedDate = DateOnly.Parse(req.ReceivedDate);

        await db.SaveChangesAsync();

        if (req.Status == "ready")
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var msg = $"طلب المختبر {order.OrderNumber} — {order.ApplianceType} جاهز للاستلام";
                    await notifications.NotifyRoleAsync("Reception", "lab", "طلب مختبر جاهز", msg, "LabOrder", order.Id);
                    await notifications.NotifyRoleAsync("Admin", "lab", "طلب مختبر جاهز", msg, "LabOrder", order.Id);
                }
                catch { /* non-blocking */ }
            });
        }

        return Ok(new { id, status = req.Status });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        order.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الطلب بنجاح" });
    }
}
