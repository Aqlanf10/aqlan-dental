using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
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
    public string Currency { get; init; } = "YER";
    public decimal? ExchangeRateToYer { get; init; }
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
        RuleFor(x => x.Currency)
            .Must(currency => currency.Trim().ToUpperInvariant() is "YER" or "SAR" or "USD")
            .WithMessage("العملة يجب أن تكون YER أو SAR أو USD");
        RuleFor(x => x.ExchangeRateToYer)
            .GreaterThan(0).WithMessage("سعر الصرف الفعلي إلى الريال اليمني مطلوب")
            .When(x => !string.Equals(x.Currency, "YER", StringComparison.OrdinalIgnoreCase));
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
    public string? Reason { get; init; }
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

/// <summary>DTO for updating an existing lab order (only draft/sent statuses).</summary>
public sealed class UpdateLabOrderRequest
{
    public string? ApplianceType { get; init; }
    public string? LabName { get; init; }
    public Guid? LabId { get; init; }
    public string? Instructions { get; init; }
    public string? ExpectedDate { get; init; }
    public string? Priority { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
}

public sealed class UpdateLabOrderRequestValidator : AbstractValidator<UpdateLabOrderRequest>
{
    private static readonly HashSet<string> ValidPriorities = ["urgent", "normal", "low"];

    public UpdateLabOrderRequestValidator()
    {
        RuleFor(x => x.ApplianceType)
            .NotEmpty().WithMessage("نوع الجهاز مطلوب")
            .MaximumLength(200)
            .When(x => x.ApplianceType is not null);
        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p)).WithMessage("الأولوية غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.Priority));
        RuleFor(x => x.ExpectedDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الاستلام المتوقع غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.ExpectedDate));
    }
}

[ApiController]
[Route("api/lab-orders")]
[Authorize(Policy = "StaffOnly")]
[ServiceFilter(typeof(PatientAccessFilter))]
public class LabOrdersController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IServiceScopeFactory scopeFactory,
    ILogger<LabOrdersController> logger,
    IPatientAccessService patientAccess,
    IAuditService audit,
    // Sprint 12 — read/query logic extracted to LabOrderQueryService.
    LabOrderQueryService queryService,
    IJournalEntryService? journalEntryService = null) : ControllerBase
{
    // CLIN-01: Per-patient access check for actions where patientId is in body or inferred.
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "LabOrder", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }
        return null;
    }

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "draft", "sent", "manufacturing", "tryIn", "ready", "received", "delivered", "returned", "remake", "cancelled"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["draft"] = new(StringComparer.OrdinalIgnoreCase) { "sent", "cancelled" },
        ["sent"] = new(StringComparer.OrdinalIgnoreCase) { "manufacturing", "cancelled" },
        ["manufacturing"] = new(StringComparer.OrdinalIgnoreCase) { "tryIn", "ready", "cancelled" },
        ["tryIn"] = new(StringComparer.OrdinalIgnoreCase) { "ready", "returned", "cancelled" },
        ["ready"] = new(StringComparer.OrdinalIgnoreCase) { "received", "returned", "cancelled" },
        ["received"] = new(StringComparer.OrdinalIgnoreCase) { "delivered", "returned" },
        ["returned"] = new(StringComparer.OrdinalIgnoreCase) { "remake", "cancelled" },
        ["remake"] = new(StringComparer.OrdinalIgnoreCase) { "sent", "cancelled" },
        ["delivered"] = new(StringComparer.OrdinalIgnoreCase),
        ["cancelled"] = new(StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Checks if an exception is caused by a missing database table or column (PostgreSQL 42P01/42703).
    /// This allows graceful fallback when LabOrderItems or related tables don't exist yet.
    /// </summary>
    private static bool IsMissingTableOrColumnError(Exception ex)
    {
        // Direct PostgresException
        if (ex is PostgresException pgEx)
            return pgEx.SqlState is "42P01" or "42703"; // undefined_table or undefined_column

        // Wrapped in another exception (e.g., InvalidOperationException from EF Core)
        if (ex.InnerException is PostgresException innerPgEx)
            return innerPgEx.SqlState is "42P01" or "42703";

        // Deeper nesting (e.g., DbUpdateException wrapping NpgsqlException)
        var inner = ex.InnerException?.InnerException;
        if (inner is PostgresException deepPgEx)
            return deepPgEx.SqlState is "42P01" or "42703";

        // Fallback: check message for common PostgreSQL error patterns
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            && (msg.Contains("LabOrderItems", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("LabWorkTypes", StringComparison.OrdinalIgnoreCase));
    }

    private Task<bool> CanAsync(string action) => PermissionGuard.HasAsync(db, currentUser, "lab_orders", action);

    private static string CanonicalStatus(string status)
    {
        var trimmed = status.Trim();
        return trimmed.Equals("tryin", StringComparison.OrdinalIgnoreCase) ? "tryIn" : trimmed.ToLowerInvariant();
    }

    private static bool CanTransition(string fromStatus, string toStatus)
    {
        if (string.Equals(fromStatus, toStatus, StringComparison.OrdinalIgnoreCase))
            return true;

        return AllowedTransitions.TryGetValue(fromStatus, out var allowed) && allowed.Contains(toStatus);
    }

    // Sprint 12 — LabOrderProjection (previously a shared list-response shape) was
    // removed: it was dead code (no callers) and the live list endpoints each had
    // their own inline projection. Those projections now live in LabOrderQueryService.

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? patientId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!await CanAsync("view")) return Forbid();

        // pageSize clamping stays in the controller so the response envelope carries
        // the same clamped value the query was actually paged with (behavior preserved).
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var (orders, total) = await queryService.GetAllAsync(patientId, status, page, pageSize);
        return Ok(new { data = orders, total, page, pageSize });
    }

    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount()
    {
        if (!await CanAsync("view")) return Forbid();

        var count = await queryService.GetPendingCountAsync();
        return Ok(new { count });
    }

    // ─── Sprint 2 — Today's lab orders ──────────────────────────────────────
    /// <summary>Returns lab orders where any key date matches today.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        if (!await CanAsync("view")) return Forbid();

        var orders = await queryService.GetTodayAsync();
        return Ok(new { data = orders });
    }

    // ─── Sprint 2 — Lab orders ready for delivery ───────────────────────────
    /// <summary>Returns lab orders that are ready or received (awaiting patient delivery).</summary>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReady()
    {
        if (!await CanAsync("view")) return Forbid();

        var orders = await queryService.GetReadyAsync();
        return Ok(new { data = orders });
    }

    // ─── Lab Sprint 6 — Overdue lab orders ──────────────────────────────────
    /// <summary>Returns lab orders that are past their expected date and not yet delivered/cancelled.</summary>
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue()
    {
        if (!await CanAsync("view")) return Forbid();

        var orders = await queryService.GetOverdueAsync();
        return Ok(new { data = orders, count = orders.Count });
    }

    // ─── Lab Sprint 6 — Ready for delivery to patient ──────────────────────
    /// <summary>Returns lab orders that are received and ready for patient delivery.</summary>
    [HttpGet("ready-for-delivery")]
    public async Task<IActionResult> GetReadyForDelivery()
    {
        if (!await CanAsync("view")) return Forbid();

        var orders = await queryService.GetReadyForDeliveryAsync();
        return Ok(new { data = orders, count = orders.Count });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        // Sprint 12: query + projection extracted to LabOrderQueryService.GetByIdAsync.
        // The schema-mismatch fallback runs inside the service; unexpected exceptions
        // propagate up to this try-catch so we return the same Arabic 500 as before.
        LabOrderDetailDto? dto;
        try
        {
            dto = await queryService.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error loading lab order {OrderId}: {ErrorType} — {ErrorMsg}", id, ex.GetType().Name, ex.InnerException?.Message ?? ex.Message);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل أمر المختبر" });
        }

        if (dto is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // CLIN-01: per-patient check after loading the entity (PatientId is exposed
        // on the DTO for exactly this authorization gate).
        var denied = await DenyIfDoctorCannotAccess(dto.PatientId);
        if (denied is not null) return denied;

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLabOrderRequest req)
    {
        if (!await CanAsync("create")) return Forbid();

        // CLIN-01: per-patient check before creating.
        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        if (!await ActivePatientWriteGuard.ExistsAsync(db, req.PatientId))
            return BadRequest(new { message = ActivePatientWriteGuard.ErrorMessage });

        var branchId = (await db.Patients
            .Where(patient => patient.Id == req.PatientId && patient.IsActive)
            .Select(patient => patient.BranchId)
            .FirstOrDefaultAsync())
            ?? currentUser.BranchId
            ?? Guid.Empty;
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لا يمكن إنشاء طلب معمل لمريض بلا فرع محدد." });

        var currency = req.Currency.Trim().ToUpperInvariant();
        var exchangeRateToYer = currency == "YER" ? 1m : req.ExchangeRateToYer.GetValueOrDefault();
        if (exchangeRateToYer <= 0m)
            return BadRequest(new { message = "سعر الصرف الفعلي إلى الريال اليمني مطلوب لتكلفة المعمل." });

        // CON-02 FIX: Use advisory lock + unique constraint retry to prevent race condition
        // on order number generation. Strategy: advisory lock serializes generation within
        // the DB, unique index on OrderNumber is the safety net, and retry with fresh count
        // handles the extremely unlikely case where both fail.
        const int maxRetries = 3;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var useTx = db.Database.IsRelational();
            var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
            try
            {
                // Acquire advisory lock for lab order number generation
                if (useTx && db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var lockKey = Math.Abs("LabOrderNumber".GetHashCode()) % 100000;
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
                }

                var year = DateTime.UtcNow.Year;
                var count = await db.LabOrders.IgnoreQueryFilters()
                    .CountAsync(l => l.OrderNumber != null && l.OrderNumber.StartsWith($"LAB-{year}-"));

                Lab? selectedLab = null;
                if (req.LabId.HasValue)
                {
                    selectedLab = await db.Labs.FirstOrDefaultAsync(l => l.Id == req.LabId.Value && l.IsActive);
                    if (selectedLab is null)
                        return BadRequest(new { message = "المعمل المحدد غير موجود أو غير مفعل" });
                }

                // LabOrder.DoctorId references Doctors.Id, NOT Users.Id — when the
                // client omits doctorId, resolve the Doctor row of the current user
                // instead of writing the UserId (which violated the FK).
                var resolvedDoctorId = req.DoctorId;
                if (!resolvedDoctorId.HasValue && currentUser.UserId.HasValue)
                {
                    resolvedDoctorId = await db.Doctors
                        .Where(d => d.UserId == currentUser.UserId.Value && d.IsActive)
                        .Select(d => (Guid?)d.Id)
                        .FirstOrDefaultAsync();
                }

                var hasExplicitSentDate = !string.IsNullOrWhiteSpace(req.SentDate);
                var order = new LabOrder
                {
                    PatientId     = req.PatientId,
                    OrthoCaseId   = req.OrthoCaseId,
                    OrderNumber   = $"LAB-{year}-{(count + 1):D3}",
                    ApplianceType = req.ApplianceType,
                    LabName       = selectedLab?.Name ?? req.LabName,
                    LabId         = req.LabId,
                    SentDate      = hasExplicitSentDate
                        ? DateOnly.TryParse(req.SentDate, out var sentDate) ? sentDate : ClinicTimeProvider.ClinicToday() : null,
                    ExpectedDate  = !string.IsNullOrWhiteSpace(req.ExpectedDate)
                        ? DateOnly.TryParse(req.ExpectedDate, out var expectedDate) ? expectedDate : (DateOnly?)null : null,
                    Priority      = req.Priority,
                    Instructions  = req.Instructions,
                    Cost          = req.Cost,
                    DoctorId      = resolvedDoctorId,
                    Status        = hasExplicitSentDate ? "sent" : "draft",
                    // Sprint 2 — new fields
                    Shade            = req.Shade,
                    RestorationType  = req.RestorationType,
                    VisitId          = req.VisitId,
                    BranchId         = branchId,
                    Currency         = currency,
                    ExchangeRateToYer = exchangeRateToYer,
                };

                // Lab Sprint 3 — Add items and auto-calculate TotalCost
                if (req.Items is { Count: > 0 })
                {
                    var workTypeIds = req.Items.Select(i => i.WorkTypeId).Distinct().ToList();
                    var existingWorkTypeIds = await db.LabWorkTypes
                        .Where(w => workTypeIds.Contains(w.Id) && w.IsActive)
                        .Select(w => w.Id)
                        .ToListAsync();
                    if (existingWorkTypeIds.Count != workTypeIds.Count)
                        return BadRequest(new { message = "أحد أنواع أعمال المعمل غير موجود أو غير مفعل" });

                    var priceLookup = req.LabId.HasValue
                        ? await db.LabWorkPrices
                            .Where(p => p.LabId == req.LabId.Value && workTypeIds.Contains(p.WorkTypeId) && p.IsActive)
                            .ToDictionaryAsync(p => p.WorkTypeId)
                        : new Dictionary<Guid, LabWorkPrice>();

                    foreach (var itemDto in req.Items)
                    {
                        var unitsCount = Math.Max(1, itemDto.UnitsCount);
                        var unitPrice = itemDto.UnitPrice;
                        if (!unitPrice.HasValue && priceLookup.TryGetValue(itemDto.WorkTypeId, out var price))
                        {
                            unitPrice = price.UnitPrice;
                            if (req.Priority == "urgent" && price.UrgentSurcharge.HasValue)
                            {
                                unitPrice += price.UrgentSurchargeType == "percentage"
                                    ? price.UnitPrice * (price.UrgentSurcharge.Value / 100m)
                                    : price.UrgentSurcharge.Value;
                            }
                        }
                        var itemTotal = itemDto.TotalPrice ?? (unitPrice * unitsCount);
                        order.Items.Add(new LabOrderItem
                        {
                            WorkTypeId = itemDto.WorkTypeId,
                            ToothNumber = itemDto.ToothNumber,
                            Arch = itemDto.Arch,
                            Shade = itemDto.Shade,
                            RestorationType = itemDto.RestorationType,
                            UnitsCount = unitsCount,
                            UnitPrice = unitPrice,
                            TotalPrice = itemTotal,
                            Instructions = itemDto.Instructions,
                            SortOrder = itemDto.SortOrder,
                        });
                    }
                    order.TotalCost = order.Items.Sum(i => i.TotalPrice ?? 0);
                    order.Cost = order.TotalCost;
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
                        var supplier = selectedLab!.SupplierId.HasValue
                            ? await db.Suppliers.FirstOrDefaultAsync(item => item.Id == selectedLab.SupplierId.Value && item.IsActive)
                            : null;
                        supplier ??= await db.Suppliers.FirstOrDefaultAsync(item =>
                            item.IsActive && item.Type == SupplierType.DentalLab && item.Name == selectedLab.Name);
                        if (supplier is null)
                        {
                            supplier = new Supplier
                            {
                                Name = selectedLab.Name,
                                Type = SupplierType.DentalLab,
                                ContactPerson = selectedLab.ContactPerson,
                                Phone = selectedLab.Phone,
                                Email = selectedLab.Email,
                                Address = selectedLab.Address,
                                Notes = "تم إنشاؤه تلقائياً من وحدة المعامل"
                            };
                            db.Suppliers.Add(supplier);
                        }
                        selectedLab.SupplierId = supplier.Id;

                        var billDate = order.SentDate ?? ClinicTimeProvider.ClinicToday();
                        var billPrefix = $"BILL-{billDate:yyyyMMdd}-";
                        if (useTx && db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", StableLockKeyHelper.BillNumber);
                        var lastBillNumber = await db.SupplierBills.IgnoreQueryFilters()
                            .Where(bill => bill.BillNumber.StartsWith(billPrefix))
                            .OrderByDescending(bill => bill.BillNumber)
                            .Select(bill => bill.BillNumber)
                            .FirstOrDefaultAsync();
                        var billSequence = 1;
                        if (!string.IsNullOrWhiteSpace(lastBillNumber)
                            && int.TryParse(lastBillNumber[billPrefix.Length..], out var previousBillSequence))
                            billSequence = previousBillSequence + 1;

                        var supplierBill = new SupplierBill
                        {
                            BillNumber = $"{billPrefix}{billSequence:D3}",
                            SupplierId = supplier.Id,
                            Description = $"طلب معمل {order.OrderNumber} - {order.ApplianceType}",
                            TotalAmount = payableAmount,
                            Currency = currency,
                            ExchangeRateToYer = exchangeRateToYer,
                            ExchangeRateSource = currency == "YER" ? "same_currency" : "manual",
                            Status = BillStatus.Unpaid,
                            BillDate = billDate,
                            DueDate = order.ExpectedDate,
                            LabOrderId = order.Id,
                            BranchId = branchId,
                            CreatedBy = currentUser.UserId ?? Guid.Empty
                        };
                        db.SupplierBills.Add(supplierBill);
                        if (currency == "YER") supplier.Balance += payableAmount;

                        db.LabPayables.Add(new LabPayable
                        {
                            LabOrderId = order.Id,
                            LabId = order.LabId!.Value,
                            SupplierBillId = supplierBill.Id,
                            Amount = payableAmount,
                            PaidAmount = 0,
                            Status = "pending",
                            DueDate = order.ExpectedDate?.ToDateTime(TimeOnly.MinValue),
                        });

                        if (journalEntryService is not null && currentUser.UserId is { } performedBy && performedBy != Guid.Empty)
                        {
                            var entry = await journalEntryService.CreateEntryAsync(
                                FinancialDocumentType.SupplierBill,
                                supplierBill.Id,
                                $"استحقاق طلب معمل {order.OrderNumber} - {selectedLab.Name}",
                                billDate,
                                branchId,
                                performedBy,
                                cashierSessionId: null,
                                treasuryId: null,
                                lines:
                                [
                                    (JournalAccountType.Expense, supplierBill.Id, payableAmount, 0m, $"تكلفة طلب المعمل {order.OrderNumber}"),
                                    (JournalAccountType.AccountsPayable, supplier.Id, 0m, payableAmount, $"مستحق للمعمل {selectedLab.Name}")
                                ],
                                autoSave: false);
                            entry.Currency = currency;
                            entry.ExchangeRateToYer = exchangeRateToYer;
                            entry.IsPosted = true;
                            entry.PostedAt = DateTime.UtcNow;
                        }
                    }
                }

                try
                {
                    await db.SaveChangesAsync();
                    if (useTx) await tx!.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // CON-02 FIX: Unique constraint on OrderNumber caught a duplicate.
                    // Roll back and retry with a fresh count.
                    if (useTx) await tx!.RollbackAsync();
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
                if (useTx) await tx!.RollbackAsync();
                throw;
            }
            catch
            {
                if (useTx) await tx!.RollbackAsync();
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

    /// <summary>
    /// Updates editable fields on a lab order. Only allowed when status is draft or sent.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLabOrderRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        var validator = new UpdateLabOrderRequestValidator();
        var validationResult = await validator.ValidateAsync(req);
        if (!validationResult.IsValid)
            return BadRequest(new { message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)) });

        LabOrder? order;
        try
        {
            order = await db.LabOrders
                .Include(l => l.Patient)
                .Include(l => l.OrthoCase)
                .Include(l => l.Doctor)
                .Include(l => l.Lab)
                .Include(l => l.Items).ThenInclude(i => i.WorkType)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
        catch (Exception ex) when (IsMissingTableOrColumnError(ex))
        {
            logger.LogWarning(ex, "LabOrderItems/WorkType query failed (schema mismatch) — falling back to query without Items for lab order update {OrderId}. Error: {ErrorMsg}", id, ex.InnerException?.Message ?? ex.Message);
            order = await db.LabOrders
                .Include(l => l.Patient)
                .Include(l => l.OrthoCase)
                .Include(l => l.Doctor)
                .Include(l => l.Lab)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error loading lab order for update {OrderId}: {ErrorType} — {ErrorMsg}", id, ex.GetType().Name, ex.InnerException?.Message ?? ex.Message);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل أمر المختبر للتحديث" });
        }

        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // SEC-ROUTE: per-patient access check before mutating. UpdateLabOrderRequest does NOT
        // carry PatientId, and the class-level PatientAccessFilter only inspects route + query
        // values for "patientId" — so the filter never sees a patientId for this {id:guid} action.
        // Resolve it from the fetched order and check explicitly. Mirrors DocumentsController
        // (SEC-DOCS fix) and the established DenyIfDoctorCannotAccess pattern.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        // Only allow edits when the order is in draft or sent status
        var editableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "draft", "sent" };
        if (!editableStatuses.Contains(order.Status))
            return BadRequest(new { message = "لا يمكن تعديل الطلب بعد بدء التصنيع" });

        // Apply updates only for fields that are provided (non-null)
        if (req.ApplianceType is not null)
            order.ApplianceType = req.ApplianceType;

        if (req.LabId.HasValue)
        {
            // If LabId is being changed, look up the lab and update LabName to match
            var selectedLab = await db.Labs.FirstOrDefaultAsync(l => l.Id == req.LabId.Value && l.IsActive);
            if (selectedLab is null)
                return BadRequest(new { message = "المعمل المحدد غير موجود أو غير مفعل" });
            order.LabId = req.LabId;
            order.LabName = selectedLab.Name;
        }
        else if (req.LabName is not null)
        {
            // Free-text LabName update (LabId is not provided, so keep existing LabId as-is)
            order.LabName = req.LabName;
        }

        if (req.Instructions is not null)
            order.Instructions = req.Instructions;

        if (!string.IsNullOrWhiteSpace(req.ExpectedDate) && DateOnly.TryParse(req.ExpectedDate, out var expectedDate))
            order.ExpectedDate = expectedDate;

        if (!string.IsNullOrWhiteSpace(req.Priority))
            order.Priority = req.Priority;

        if (req.Shade is not null)
            order.Shade = req.Shade;

        if (req.RestorationType is not null)
            order.RestorationType = req.RestorationType;

        // CLIN-31 FIX: Recalculate TotalCost and Cost if items exist and LabId changed.
        // Previously, changing LabId (which could change the price-lookup basis) did NOT
        // recalculate TotalCost — the order kept the old lab's prices. Now re-sums from items.
        if (req.LabId.HasValue && order.Items != null && order.Items.Count > 0)
        {
            var newTotalCost = order.Items.Where(i => i.IsActive).Sum(i => i.TotalPrice);
            order.TotalCost = newTotalCost;
            order.Cost = newTotalCost; // keep Cost in sync with TotalCost (CLIN-08)
        }

        await db.SaveChangesAsync();

        // Re-fetch navigation properties that may have changed (Lab)
        if (req.LabId.HasValue)
        {
            await db.Entry(order).Reference(o => o.Lab).LoadAsync();
        }

        // Return the same projection as GetById
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

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateLabOrderStatusRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        var nextStatus = CanonicalStatus(req.Status);
        if (!ValidStatuses.Contains(nextStatus))
            return BadRequest(new { message = "الحالة غير صالحة" });

        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // SEC-ROUTE: per-patient access check before mutating. The route only carries the
        // order id ({id:guid}/status), so the class-level PatientAccessFilter never sees a
        // patientId for this action. Resolve it from the fetched order and check explicitly.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        var oldStatus = order.Status;
        if (!CanTransition(oldStatus, nextStatus))
            return BadRequest(new { message = $"لا يمكن نقل الطلب من {oldStatus} إلى {nextStatus}" });

        order.Status = nextStatus;
        if (nextStatus == "sent" && order.SentDate is null)
            order.SentDate = ClinicTimeProvider.ClinicToday();
        if (nextStatus == "delivered")
            order.DeliveredDate = ClinicTimeProvider.ClinicToday();

        if (nextStatus == "received" && !string.IsNullOrWhiteSpace(req.ReceivedDate))
        {
            if (!DateOnly.TryParse(req.ReceivedDate, out var receivedDate))
                return BadRequest(new { message = "تنسيق تاريخ الاستلام غير صالح. استخدم YYYY-MM-DD" });
            order.ReceivedDate = receivedDate;
        }
        else if (nextStatus == "received" && order.ReceivedDate is null)
        {
            order.ReceivedDate = ClinicTimeProvider.ClinicToday();
        }

        if (!string.Equals(oldStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
            {
                LabOrderId = id,
                FromStatus = oldStatus,
                ToStatus = nextStatus,
                ChangedByUserId = currentUser.UserId,
                Reason = req.Reason
            });
        }

        await db.SaveChangesAsync();

        if (nextStatus == "ready")
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

        return Ok(new { id, status = nextStatus });
    }

    // ─── Sprint 2 — Mark lab order as received ──────────────────────────────
    /// <summary>Marks a lab order as received from the lab.</summary>
    [HttpPost("{id:guid}/mark-received")]
    public async Task<IActionResult> MarkReceived(Guid id, [FromBody] MarkReceivedRequest? req)
    {
        if (!await CanAsync("edit")) return Forbid();

        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // SEC-ROUTE: per-patient access check before mutating. The route only carries the
        // order id ({id:guid}/mark-received), so the class-level PatientAccessFilter never
        // sees a patientId for this action. Resolve it from the fetched order and check.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        if (order.Status != "ready")
            return BadRequest(new { message = "لا يمكن تأكيد الوصول للحالة الحالية" });

        var oldStatus = order.Status;
        order.Status = "received";
        order.ReceivedDate = !string.IsNullOrWhiteSpace(req?.ReceivedDate) && DateOnly.TryParse(req.ReceivedDate, out var rd)
            ? rd
            : ClinicTimeProvider.ClinicToday();
        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            LabOrderId = id, FromStatus = oldStatus, ToStatus = "received",
            ChangedByUserId = currentUser.UserId
        });
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
        if (!await CanAsync("edit")) return Forbid();

        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // CLIN-01: per-patient check before marking delivered.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        if (order.Status != "received")
            return BadRequest(new { message = "لا يمكن التسليم للحالة الحالية — يجب أن تكون مستلمة أولاً" });

        // CLIN-09 FIX: Require a linked visit before marking delivered. Without this, reception
        // could mark a lab order "delivered" as a pure status flag with no clinical record —
        // the patient's chart shows the appliance was delivered but the visit, treatment, and
        // invoice are missing, leading to revenue leakage and incomplete clinical history.
        if (!order.VisitId.HasValue)
        {
            return BadRequest(new
            {
                message = "لا يمكن تسليم طلب المختبر بدون زيارة مرتبطة. يجب ربط الطلب بزيارة المريض أولاً.",
                requiresVisitLink = true
            });
        }

        var oldStatus = order.Status;
        order.Status = "delivered";
        order.DeliveredDate = ClinicTimeProvider.ClinicToday();
        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            LabOrderId = id, FromStatus = oldStatus, ToStatus = "delivered",
            ChangedByUserId = currentUser.UserId
        });
        await db.SaveChangesAsync();
        return Ok(new { id, status = "delivered" });
    }

    // ─── Sprint 2 — Cancel lab order with reason ───────────────────────────
    /// <summary>Cancels a lab order with a reason.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelLabOrderRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // CLIN-01: per-patient check before cancelling.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

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
        if (!await CanAsync("edit")) return Forbid();

        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // SEC-ROUTE: per-patient access check before mutating. The route only carries the
        // order id ({id:guid}/return), so the class-level PatientAccessFilter never sees a
        // patientId for this action. Resolve it from the fetched order and check.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

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
        if (!await CanAsync("edit")) return Forbid();

        var order = await db.LabOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // SEC-ROUTE: per-patient access check before mutating. The route only carries the
        // order id ({id:guid}/remake), so the class-level PatientAccessFilter never sees a
        // patientId for this action. Resolve it from the fetched order and check.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        if (order.Status != "returned")
            return BadRequest(new { message = "لا يمكن إعادة الصنع إلا للطلبات المرتجعة" });

        var oldStatus = order.Status;
        order.Status = "remake";
        order.RemakeReason = req.Reason;
        order.IsFreeRemake = req.IsFreeRemake;
        order.RemakeCost = req.RemakeCost;
        order.RemakeCount += 1;
        order.SentDate = ClinicTimeProvider.ClinicToday();

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
        if (!await CanAsync("view")) return Forbid();

        // Sprint 12: query extracted to LabOrderQueryService. Service returns null
        // if the order itself doesn't exist (preserving the original 404 path).
        var history = await queryService.GetHistoryAsync(id);
        if (history is null) return NotFound(new { message = "طلب المختبر غير موجود" });
        return Ok(new { data = history });
    }

    // ─── Lab Sprint 4 — Attachments ──────────────────────────────────────────
    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        // Sprint 12: query extracted to LabOrderQueryService. Original behavior
        // (no existence check — empty list if no attachments) preserved.
        var attachments = await queryService.GetAttachmentsAsync(id);
        return Ok(new { data = attachments });
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file,
        [FromForm] string category = "photo", [FromForm] Guid? labOrderItemId = null)
    {
        if (!await CanAsync("edit")) return Forbid();

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

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId)
    {
        if (!await CanAsync("view")) return Forbid();

        var attachment = await db.LabOrderAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.LabOrderId == id);
        if (attachment is null) return NotFound(new { message = "المرفق غير موجود" });
        if (!System.IO.File.Exists(attachment.StoragePath))
            return NotFound(new { message = "ملف المرفق غير موجود على الخادم" });

        var stream = System.IO.File.OpenRead(attachment.StoragePath);
        return File(stream, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId)
    {
        if (!await CanAsync("edit")) return Forbid();

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
        if (!await CanAsync("delete")) return Forbid();

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
        if (!await CanAsync("export")) return Forbid();

        LabOrder? order;
        try
        {
            order = await db.LabOrders
                .Include(l => l.Patient)
                .Include(l => l.Doctor)
                .Include(l => l.Lab)
                .Include(l => l.Items).ThenInclude(i => i.WorkType)
                .Include(l => l.Visit)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
        catch (Exception ex) when (IsMissingTableOrColumnError(ex))
        {
            logger.LogWarning(ex, "LabOrderItems/WorkType query failed (schema mismatch) — falling back to query without Items for lab order PDF {OrderId}. Error: {ErrorMsg}", id, ex.InnerException?.Message ?? ex.Message);
            order = await db.LabOrders
                .Include(l => l.Patient)
                .Include(l => l.Doctor)
                .Include(l => l.Lab)
                .Include(l => l.Visit)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error loading lab order for PDF {OrderId}: {ErrorType} — {ErrorMsg}", id, ex.GetType().Name, ex.InnerException?.Message ?? ex.Message);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل أمر المختبر للطباعة" });
        }

        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        try
        {
            var clinicName = await db.Settings.Where(s => s.Key == "clinic.name").Select(s => s.Value).FirstOrDefaultAsync() ?? "مركز طب الأسنان";
            var clinicPhone = await db.Settings.Where(s => s.Key == "clinic.phones").Select(s => s.Value).FirstOrDefaultAsync() ?? "";
            var clinicAddress = await db.Settings.Where(s => s.Key == "clinic.location").Select(s => s.Value).FirstOrDefaultAsync() ?? "";

            // CLIN-12: CPU-bound PDF generation is offloaded to the thread pool
            // (GenerateAsync wraps Task.Run) so the request thread is released.
            var pdfBytes = await LabOrderPdfGenerator.GenerateAsync(order, clinicName, clinicPhone, clinicAddress);
            return File(pdfBytes, "application/pdf", $"lab-order-{order.OrderNumber}.pdf");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate PDF for lab order {OrderId}: {ErrorType} — {ErrorMsg}", id, ex.GetType().Name, ex.Message);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء أمر العمل" });
        }
    }
}
