using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.API.Services;
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

public sealed class CreateLabOrderItemDto
{
    public Guid WorkTypeId { get; init; }
    public string? ToothNumber { get; init; }
    public string? Arch { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public int UnitsCount { get; init; } = 1;
    public decimal? UnitPrice { get; init; }
    public decimal? TotalPrice { get; init; }
    public string? Instructions { get; init; }
    public int SortOrder { get; init; }
}

public sealed class CreateLabOrderRequest
{
    public Guid PatientId { get; init; }
    public Guid? OrthoCaseId { get; init; }
    public string ApplianceType { get; init; } = string.Empty;
    public string? LabName { get; init; }
    public Guid? LabId { get; init; }
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
    // Lab Sprint 3 — professional order items
    public List<CreateLabOrderItemDto>? Items { get; init; }
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
            .Include(l => l.Lab)
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
                LabEntityName = l.Lab != null ? l.Lab.Name : null,
                l.LabId,
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
            .Include(l => l.Lab)
            .Include(l => l.Items).ThenInclude(i => i.WorkType)
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
            LabEntityName = order.Lab?.Name,
            order.LabId,
            SentDate = order.SentDate?.ToString("yyyy-MM-dd"),
            ExpectedDate = order.ExpectedDate?.ToString("yyyy-MM-dd"),
            ReceivedDate = order.ReceivedDate?.ToString("yyyy-MM-dd"),
            DeliveredDate = order.DeliveredDate?.ToString("yyyy-MM-dd"),
            order.Status,
            order.Priority,
            order.Instructions,
            order.Cost,
            order.TotalCost,
            DoctorName = order.Doctor?.Name,
            order.Shade,
            order.RestorationType,
            order.VisitId,
            order.CancellationReason,
            CreatedAt = order.CreatedAt.ToString("yyyy-MM-dd"),
            // Lab Sprint 3 — order items
            Items = order.Items.Select(i => new
            {
                i.Id,
                i.WorkTypeId,
                WorkTypeName = i.WorkType != null ? i.WorkType.Name : null,
                i.ToothNumber,
                i.Arch,
                i.Shade,
                i.RestorationType,
                i.UnitsCount,
                i.UnitPrice,
                i.TotalPrice,
                i.Instructions,
                i.SortOrder
            }).OrderBy(i => i.SortOrder)
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
                    LabId         = req.LabId,
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

                // Lab Sprint 3 — Add items and auto-calculate TotalCost
                if (req.Items is { Count: > 0 })
                {
                    foreach (var itemDto in req.Items)
                    {
                        var itemTotal = itemDto.TotalPrice ?? (itemDto.UnitPrice * itemDto.UnitsCount);
                        order.Items.Add(new LabOrderItem
                        {
                            WorkTypeId = itemDto.WorkTypeId,
                            ToothNumber = itemDto.ToothNumber,
                            Arch = itemDto.Arch,
                            Shade = itemDto.Shade,
                            RestorationType = itemDto.RestorationType,
                            UnitsCount = itemDto.UnitsCount,
                            UnitPrice = itemDto.UnitPrice,
                            TotalPrice = itemTotal,
                            Instructions = itemDto.Instructions,
                            SortOrder = itemDto.SortOrder,
                        });
                    }
                    order.TotalCost = order.Items.Sum(i => i.TotalPrice ?? 0);
                }
                else if (req.Cost.HasValue)
                {
                    order.TotalCost = req.Cost.Value;
                }

                db.LabOrders.Add(order);

                // Lab Sprint 5 — Auto-create LabPayable if order has a lab and cost
                if (order.LabId.HasValue && (order.TotalCost > 0 || order.Cost > 0))
                {
                    var payableAmount = order.TotalCost ?? order.Cost ?? 0;
                    if (payableAmount > 0)
                    {
                        db.LabPayables.Add(new LabPayable
                        {
                            LabOrderId = order.Id,
                            LabId = order.LabId!.Value,
                            Amount = payableAmount,
                            PaidAmount = 0,
                            Status = "pending",
                            DueDate = order.ExpectedDate?.ToDateTime(TimeOnly.MinValue),
                        });
                    }
                }

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

        var oldStatus = order.Status;
        order.Status = "cancelled";
        order.CancellationReason = req.Reason;
        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            LabOrderId = id, FromStatus = oldStatus, ToStatus = "cancelled",
            ChangedByUserId = currentUser.UserId, Reason = req.Reason
        });
        await db.SaveChangesAsync();
        return Ok(new { id, status = "cancelled" });
    }

    // ─── Lab Sprint 4 — Return lab order ─────────────────────────────────────
    public sealed class ReturnLabOrderRequest { public string Reason { get; init; } = string.Empty; }

    /// <summary>Marks a lab order as returned to the lab.</summary>
    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnLabOrderRequest req)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });
        var validReturnStatuses = new HashSet<string> { "tryIn", "ready", "received", "delivered" };
        if (!validReturnStatuses.Contains(order.Status))
            return BadRequest(new { message = "لا يمكن إرجاع الطلب للحالة الحالية" });

        var oldStatus = order.Status;
        order.Status = "returned";
        order.ReturnReason = req.Reason;
        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            LabOrderId = id, FromStatus = oldStatus, ToStatus = "returned",
            ChangedByUserId = currentUser.UserId, Reason = req.Reason
        });
        await db.SaveChangesAsync();
        return Ok(new { id, status = "returned" });
    }

    // ─── Lab Sprint 4 — Remake lab order ─────────────────────────────────────
    public sealed class RemakeLabOrderRequest
    {
        public string Reason { get; init; } = string.Empty;
        public bool IsFreeRemake { get; init; }
        public decimal? RemakeCost { get; init; }
    }

    /// <summary>Creates a remake from a returned lab order.</summary>
    [HttpPost("{id:guid}/remake")]
    public async Task<IActionResult> Remake(Guid id, [FromBody] RemakeLabOrderRequest req)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });
        if (order.Status != "returned")
            return BadRequest(new { message = "لا يمكن إعادة الصنع إلا للطلبات المرتجعة" });

        var oldStatus = order.Status;
        order.Status = "remake";
        order.RemakeReason = req.Reason;
        order.IsFreeRemake = req.IsFreeRemake;
        order.RemakeCost = req.RemakeCost;
        order.RemakeCount += 1;
        order.SentDate = DateOnly.FromDateTime(DateTime.Today);

        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            LabOrderId = id, FromStatus = oldStatus, ToStatus = "remake",
            ChangedByUserId = currentUser.UserId, Reason = req.Reason
        });
        await db.SaveChangesAsync();
        return Ok(new { id, status = "remake", order.RemakeCount });
    }

    // ─── Lab Sprint 4 — Status history ───────────────────────────────────────
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        var history = await db.LabOrderStatusHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.LabOrderId == id)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new
            {
                h.Id, h.FromStatus, h.ToStatus,
                ChangedByName = h.ChangedByUser != null ? h.ChangedByUser.Username : null,
                h.Reason,
                CreatedAt = h.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();
        return Ok(new { data = history });
    }

    // ─── Lab Sprint 4 — Attachments ──────────────────────────────────────────
    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id)
    {
        var attachments = await db.LabOrderAttachments
            .Where(a => a.LabOrderId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id, a.FileName, a.ContentType, a.FileSize, a.Category,
                a.LabOrderItemId, a.StoragePath,
                UploadedByName = a.UploadedByUser != null ? a.UploadedByUser.Username : null,
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();
        return Ok(new { data = attachments });
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file,
        [FromForm] string category = "photo", [FromForm] Guid? labOrderItemId = null)
    {
        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });
        if (file is null || file.Length == 0) return BadRequest(new { message = "الملف مطلوب" });

        // Category validation with size limits
        var allowedCategories = new Dictionary<string, long>
        {
            ["photo"] = 10 * 1024 * 1024, // 10MB
            ["stl"] = 50 * 1024 * 1024, // 50MB
            ["shade-photo"] = 10 * 1024 * 1024,
            ["impression-photo"] = 10 * 1024 * 1024,
            ["pdf-instructions"] = 20 * 1024 * 1024, // 20MB
        };

        if (!allowedCategories.TryGetValue(category, out var maxSize))
            return BadRequest(new { message = "نوع المرفق غير صالح" });
        if (file.Length > maxSize)
            return BadRequest(new { message = $"حجم الملف يتجاوز الحد المسموح ({maxSize / (1024 * 1024)} ميجابايت)" });

        // Save file to uploads directory
        var uploadsDir = Path.Combine("uploads", "lab-attachments", id.ToString());
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var attachment = new LabOrderAttachment
        {
            LabOrderId = id,
            LabOrderItemId = labOrderItemId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Category = category,
            StoragePath = filePath,
            UploadedBy = currentUser.UserId,
        };

        db.LabOrderAttachments.Add(attachment);
        await db.SaveChangesAsync();

        return Ok(new { attachment.Id, attachment.FileName, attachment.Category });
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId)
    {
        var attachment = await db.LabOrderAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.LabOrderId == id);
        if (attachment is null) return NotFound(new { message = "المرفق غير موجود" });

        // Delete physical file
        if (System.IO.File.Exists(attachment.StoragePath))
            System.IO.File.Delete(attachment.StoragePath);

        db.LabOrderAttachments.Remove(attachment);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المرفق بنجاح" });
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

    // ─── Lab Sprint 5 — PDF Generation ─────────────────────────────────────────
    /// <summary>Generates a PDF for the lab order.</summary>
    [HttpGet("{id:guid}/print")]
    public async Task<IActionResult> PrintPdf(Guid id)
    {
        var order = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Include(l => l.Lab)
            .Include(l => l.Items).ThenInclude(i => i.WorkType)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        var clinicName = await db.Settings.Where(s => s.Key == "clinic.name").Select(s => s.Value).FirstOrDefaultAsync() ?? "مركز طب الأسنان";
        var clinicPhone = await db.Settings.Where(s => s.Key == "clinic.phones").Select(s => s.Value).FirstOrDefaultAsync() ?? "";
        var clinicAddress = await db.Settings.Where(s => s.Key == "clinic.location").Select(s => s.Value).FirstOrDefaultAsync() ?? "";

        try
        {
            var pdfBytes = LabOrderPdfGenerator.Generate(order, clinicName, clinicPhone, clinicAddress);
            return File(pdfBytes, "application/pdf", $"lab-order-{order.OrderNumber}.pdf");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate PDF for lab order {OrderId}", id);
            return StatusCode(500, new { message = "فشل إنشاء ملف PDF" });
        }
    }
}
