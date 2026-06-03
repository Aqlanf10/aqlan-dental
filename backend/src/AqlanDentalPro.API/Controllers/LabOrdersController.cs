using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    // Sprint 2 — new fields
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public Guid? VisitId { get; init; }
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

/// <summary>Sprint 2 — DTO for marking a lab order as received.</summary>
public sealed class MarkReceivedRequest
{
    public string? ReceivedDate { get; init; }
}

/// <summary>Sprint 2 — DTO for cancelling a lab order with a reason.</summary>
public sealed class CancelLabOrderRequest
{
    public string Reason { get; init; } = string.Empty;
}

[ApiController]
[Route("api/lab-orders")]
[Authorize(Policy = "StaffOnly")]
public class LabOrdersController(AppDbContext db, ICurrentUserService currentUser, IServiceScopeFactory scopeFactory, ILogger<LabOrdersController> logger) : ControllerBase
{
    /// <summary>Shared projection shape for lab order list responses (Sprint 2).</summary>
    private static readonly Func<LabOrder, object> LabOrderProjection = l => new
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
        DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
        l.Status,
        l.Priority,
        l.Cost,
        DoctorName = l.Doctor != null ? l.Doctor.Name : null,
        // Sprint 2 — new fields
        l.Shade,
        l.RestorationType,
        l.VisitId,
        l.CancellationReason,
        CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
    };

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
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                l.Status,
                l.Priority,
                l.Cost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                // Sprint 2 — new fields
                l.Shade,
                l.RestorationType,
                l.VisitId,
                l.CancellationReason,
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

    // ─── Sprint 2 — Today's lab orders ──────────────────────────────────────
    /// <summary>Returns lab orders where any key date matches today.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var orders = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Where(l => l.SentDate == today || l.ExpectedDate == today || l.ReceivedDate == today || l.DeliveredDate == today)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                l.Id,
                l.OrderNumber,
                l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                l.ApplianceType,
                l.LabName,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                l.Status,
                l.Priority,
                l.Cost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                l.Shade,
                l.RestorationType,
                l.VisitId,
                l.CancellationReason,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
        return Ok(new { data = orders });
    }

    // ─── Sprint 2 — Lab orders ready for delivery ───────────────────────────
    /// <summary>Returns lab orders that are ready or received (awaiting patient delivery).</summary>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReady()
    {
        var orders = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Where(l => l.Status == "ready" || l.Status == "received")
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                l.Id,
                l.OrderNumber,
                l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                l.ApplianceType,
                l.LabName,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                l.Status,
                l.Priority,
                l.Cost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                l.Shade,
                l.RestorationType,
                l.VisitId,
                l.CancellationReason,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
        return Ok(new { data = orders });
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
            DeliveredDate = order.DeliveredDate?.ToString("yyyy-MM-dd"),
            order.Status,
            order.Priority,
            order.Instructions,
            order.Cost,
            DoctorName = order.Doctor?.Name,
            // Sprint 2 — new fields
            order.Shade,
            order.RestorationType,
            order.VisitId,
            order.CancellationReason,
            CreatedAt = order.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLabOrderRequest req)
    {
        // CON-02 FIX: Use advisory lock + unique constraint retry to prevent race condition
        // on order number generation. Strategy: advisory lock serializes generation within
        // the DB, unique index on OrderNumber is the safety net, and retry with fresh count
        // handles the extremely unlikely case where both fail.
        const int maxRetries = 3;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                // Acquire advisory lock for lab order number generation
                var lockKey = Math.Abs("LabOrderNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

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
                        ? DateOnly.TryParse(req.SentDate, out var sentDate) ? sentDate : DateOnly.FromDateTime(DateTime.Today) : DateOnly.FromDateTime(DateTime.Today),
                    ExpectedDate  = !string.IsNullOrWhiteSpace(req.ExpectedDate)
                        ? DateOnly.TryParse(req.ExpectedDate, out var expectedDate) ? expectedDate : (DateOnly?)null : null,
                    Priority      = req.Priority,
                    Instructions  = req.Instructions,
                    Cost          = req.Cost,
                    DoctorId      = req.DoctorId ?? currentUser.UserId,
                    Status        = "sent",
                    // Sprint 2 — new fields
                    Shade            = req.Shade,
                    RestorationType  = req.RestorationType,
                    VisitId          = req.VisitId,
                };

                db.LabOrders.Add(order);

                try
                {
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // CON-02 FIX: Unique constraint on OrderNumber caught a duplicate.
                    // Roll back and retry with a fresh count.
                    await tx.RollbackAsync();
                    logger.LogWarning("CON-02: Lab order number collision on attempt {Attempt}, retrying", attempt + 1);
                    continue;
                }

                // M1 FIX: Use IServiceScopeFactory for proper DI in fire-and-forget
                var orderNumber = order.OrderNumber;
                var applianceType = req.ApplianceType;
                var orderId = order.Id;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var msg = $"طلب مختبر جديد {orderNumber} — {applianceType}";
                        await notifications.NotifyRoleAsync("Admin", "lab", "طلب مختبر جديد", msg, "LabOrder", orderId);
                        await notifications.NotifyRoleAsync("Reception", "lab", "طلب مختبر جديد", msg, "LabOrder", orderId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[LabOrders] Create notification failed for order {OrderId}", orderId);
                    }
                });

                return CreatedAtAction(nameof(GetById), new { id = order.Id },
                    new { order.Id, order.OrderNumber });
            }
            catch (DbUpdateException)
            {
                // Re-throw if not a unique violation (already handled above)
                await tx.RollbackAsync();
                throw;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // All retries exhausted — this should never happen with advisory lock + unique index
        logger.LogError("CON-02: Failed to generate unique lab order number after {MaxRetries} attempts", maxRetries);
        return StatusCode(500, new { message = "فشل إنشاء رقم طلب فريد بعد عدة محاولات. يرجى المحاولة مرة أخرى." });
    }

    /// <summary>
    /// CON-02 FIX: Checks if a DbUpdateException is a PostgreSQL unique constraint violation (error code 23505).
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner != null)
        {
            if (inner is PostgresException pgEx && pgEx.SqlState == "23505")
                return true;
            inner = inner.InnerException;
        }
        return false;
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
        {
            if (!DateOnly.TryParse(req.ReceivedDate, out var receivedDate))
                return BadRequest(new { message = "تنسيق تاريخ الاستلام غير صالح. استخدم YYYY-MM-DD" });
            order.ReceivedDate = receivedDate;
        }

        await db.SaveChangesAsync();

        if (req.Status == "ready")
        {
            // M1 FIX: Use IServiceScopeFactory for proper DI in fire-and-forget
            var readyOrderNumber = order.OrderNumber;
            var readyApplianceType = order.ApplianceType;
            var readyOrderId = order.Id;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var msg = $"طلب المختبر {readyOrderNumber} — {readyApplianceType} جاهز للاستلام";
                    await notifications.NotifyRoleAsync("Reception", "lab", "طلب مختبر جاهز", msg, "LabOrder", readyOrderId);
                    await notifications.NotifyRoleAsync("Admin", "lab", "طلب مختبر جاهز", msg, "LabOrder", readyOrderId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[LabOrders] Ready notification failed for order {OrderId}", readyOrderId);
                }
            });
        }

        return Ok(new { id, status = req.Status });
    }

    // ─── Sprint 2 — Mark lab order as received ──────────────────────────────
    /// <summary>Marks a lab order as received from the lab.</summary>
    [HttpPost("{id:guid}/mark-received")]
    public async Task<IActionResult> MarkReceived(Guid id, [FromBody] MarkReceivedRequest? req)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });
        if (order.Status != "sent" && order.Status != "manufacturing")
            return BadRequest(new { message = "لا يمكن تأكيد الوصول للحالة الحالية" });

        order.Status = "received";
        order.ReceivedDate = !string.IsNullOrWhiteSpace(req?.ReceivedDate) && DateOnly.TryParse(req.ReceivedDate, out var rd)
            ? rd
            : DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync();

        // Notify about received lab order
        var receivedOrderNumber = order.OrderNumber;
        var receivedApplianceType = order.ApplianceType;
        var receivedOrderId = order.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var msg = $"تم استلام طلب المختبر {receivedOrderNumber} — {receivedApplianceType}";
                await notifications.NotifyRoleAsync("Reception", "lab", "استلام طلب مختبر", msg, "LabOrder", receivedOrderId);
                await notifications.NotifyRoleAsync("Admin", "lab", "استلام طلب مختبر", msg, "LabOrder", receivedOrderId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[LabOrders] MarkReceived notification failed for order {OrderId}", receivedOrderId);
            }
        });

        return Ok(new { id, status = "received" });
    }

    // ─── Sprint 2 — Mark lab order as delivered to patient ──────────────────
    /// <summary>Marks a lab order as delivered to the patient.</summary>
    [HttpPost("{id:guid}/mark-delivered")]
    public async Task<IActionResult> MarkDelivered(Guid id)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });
        if (order.Status != "ready" && order.Status != "received")
            return BadRequest(new { message = "لا يمكن التسليم للحالة الحالية — يجب أن تكون جاهزة أولاً" });

        order.Status = "delivered";
        order.DeliveredDate = DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync();
        return Ok(new { id, status = "delivered" });
    }

    // ─── Sprint 2 — Cancel lab order with reason ───────────────────────────
    /// <summary>Cancels a lab order with a reason.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelLabOrderRequest req)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });
        if (order.Status == "delivered" || order.Status == "cancelled")
            return BadRequest(new { message = "لا يمكن إلغاء طلب مسلم أو ملغي" });

        order.Status = "cancelled";
        order.CancellationReason = req.Reason;
        await db.SaveChangesAsync();
        return Ok(new { id, status = "cancelled" });
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
