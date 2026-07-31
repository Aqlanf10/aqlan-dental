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

    // CORE-LAB-001: the draft workflow needs these. Without them a draft created
    // with no lab and no cost could never be completed — Update accepted the lab but
    // had no way to accept the cost it must be billed at.
    public decimal? Cost { get; init; }
    public string? Currency { get; init; }
    public decimal? ExchangeRateToYer { get; init; }
}

public sealed class UpdateLabOrderRequestValidator : AbstractValidator<UpdateLabOrderRequest>
{
    private static readonly HashSet<string> ValidPriorities = ["urgent", "normal", "low"];
    private static readonly HashSet<string> ValidCurrencies = ["YER", "SAR", "USD"];

    public UpdateLabOrderRequestValidator()
    {
        RuleFor(x => x.Cost)
            .GreaterThan(0m).WithMessage("التكلفة يجب أن تكون أكبر من صفر")
            .When(x => x.Cost.HasValue);
        RuleFor(x => x.Currency)
            .Must(c => ValidCurrencies.Contains(c!.Trim().ToUpperInvariant()))
            .WithMessage("العملة غير مدعومة. المدعوم: YER أو SAR أو USD")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
        // A non-YER cost is meaningless to the books without the rate it was agreed at.
        //
        // Each validator carries its OWN WithMessage: FluentValidation attaches a
        // trailing .WithMessage() to the LAST validator in the chain only, so writing
        // `.NotNull().GreaterThan(0m).WithMessage(arabic)` leaves NotNull emitting the
        // default ENGLISH "'Exchange Rate To Yer' must not be empty." — a user-facing
        // English error, which this project forbids. Caught by
        // ForeignCurrency_WithoutRate_IsRejected_WithArabicMessage.
        RuleFor(x => x.ExchangeRateToYer)
            .NotNull()
            .WithMessage("سعر الصرف الفعلي إلى الريال اليمني مطلوب للعملات غير اليمنية")
            .GreaterThan(0m)
            .WithMessage("سعر الصرف الفعلي إلى الريال اليمني يجب أن يكون أكبر من صفر")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency)
                    && !string.Equals(x.Currency!.Trim(), "YER", StringComparison.OrdinalIgnoreCase));
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
    // CORE-LAB-001 — supplier bill + payable + journal linkage, shared by create and
    // update so attaching a lab/cost after the fact still reaches the books.
    LabOrderFinanceSyncService financeSync,
    // CORE-FIN-LAB-ADJ — the doctor's commission deducts this order's cost, so any edit that
    // moves the cost has to reach the commission too: recalculated where it is still unpaid,
    // raised as a separate correction line where it has already been paid out.
    // CORE-XMOD-002: journalEntryService is gone from here on purpose. The controller no
    // longer posts its own ledger entries — LabOrderFinanceSyncService owns that, and it is
    // the only path that does, so a journal entry for a lab order can only be written one way.
    ICommissionAdjustmentService commissionAdjustments) : ControllerBase
{
    /// <summary>
    /// CORE-LAB-002: a lab order may only leave "draft" (or "remake") for "sent" once
    /// it actually describes billable work. Without this, the draft the create modal
    /// happily saves with no lab and no cost could be pushed straight to "sent" and
    /// would then sit in the lab queue forever with no supplier, no payable and no
    /// expense — invisible to finance and to the doctor's commission.
    /// </summary>
    private static string? ValidateReadyToSend(LabOrder order)
    {
        if (!order.LabId.HasValue)
            return "لا يمكن إرسال الطلب قبل تحديد المعمل.";

        var amount = order.TotalCost ?? order.Cost ?? 0m;
        if (amount <= 0m)
            return "لا يمكن إرسال الطلب قبل إدخال تكلفة صحيحة أكبر من صفر.";

        var currency = (order.Currency ?? "YER").Trim().ToUpperInvariant();
        if (currency != "YER" && order.ExchangeRateToYer <= 0m)
            return "سعر الصرف الفعلي إلى الريال اليمني مطلوب قبل إرسال الطلب.";

        var hasWorkDescription = !string.IsNullOrWhiteSpace(order.ApplianceType)
            || order.Items.Any(i => i.IsActive);
        if (!hasWorkDescription)
            return "لا يمكن إرسال الطلب قبل تحديد نوع العمل أو إضافة بند واحد على الأقل.";

        return null;
    }
    // CLIN-01: Per-patient access check for actions where patientId is in body or inferred.
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!currentUser.IsAdmin)
        {
            if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
                return Forbid();

            var patientBranch = await db.Patients
                .Where(patient => patient.Id == patientId)
                .Select(patient => patient.BranchId)
                .FirstOrDefaultAsync();
            if (!patientBranch.HasValue || patientBranch.Value != currentUser.BranchId.Value)
            {
                await audit.LogAsync(AuditAction.View, "Patient", patientId,
                    newData: new
                    {
                        status = "denied",
                        reason = "cross-branch",
                        resource = "LabOrder",
                        role = currentUser.Role?.ToString(),
                        userId = currentUser.UserId
                    });
                return StatusCode(403, new { message = "طلب المختبر لا يتبع فرع المستخدم الحالي" });
            }
        }

        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "LabOrder", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }
        return null;
    }

    /// <summary>
    /// CORE-LAB-006: per-patient gate for routes that carry only the ORDER id.
    /// <para>
    /// The class-level PatientAccessFilter only fires on a route/query value literally
    /// named "patientId" (verified in PatientAccessFilter.OnActionExecutionAsync), so
    /// every "{id:guid}/..." action is unguarded by it. History and attachments were
    /// relying on it and were therefore readable — and downloadable — across patients
    /// by a restricted doctor. Resolves the owning patient from the order and applies
    /// the same explicit check Update/UpdateStatus/Cancel already use.
    /// </para>
    /// </summary>
    private async Task<IActionResult?> DenyIfCannotAccessOrderAsync(Guid orderId)
    {
        var patientId = await db.LabOrders
            .Where(l => l.Id == orderId)
            .Select(l => (Guid?)l.PatientId)
            .FirstOrDefaultAsync();

        // Unknown order: let the action's own 404 path answer, so this guard never
        // turns a "not found" into a misleading "forbidden".
        if (!patientId.HasValue) return null;

        return await DenyIfDoctorCannotAccess(patientId.Value);
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

                // CORE-XMOD-002: this used to be ~90 lines of inline supplier / bill-number /
                // payable / journal code — a second, hand-maintained copy of what
                // LabOrderFinanceSyncService already does for Update and for the send
                // transition. Two copies of a money path drift: the update path grew a branch
                // guard, an advisory lock and a currency check that this one never received.
                // One code path owns the linkage now.
                //
                // The boundary is unchanged: a draft is not a commitment, so only a "sent"
                // order builds a payable. UpdateStatus owns the same rule.
                if (string.Equals(order.Status, "sent", StringComparison.OrdinalIgnoreCase))
                {
                    var sync = await financeSync.SyncAsync(order, branchId, currentUser.UserId ?? Guid.Empty);
                    if (!sync.Ok)
                    {
                        if (useTx) await tx!.RollbackAsync();
                        return BadRequest(new { message = sync.Error });
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

        // Rule #10: any lab-cost edit on an already-sent (financially recognised) order
        // must leave an audit trail. Snapshot before the field mutations below so the
        // audit entry after SaveChanges can tell whether anything money-relevant moved.
        var wasSent = string.Equals(order.Status, "sent", StringComparison.OrdinalIgnoreCase);
        var costBeforeEdit = order.TotalCost ?? order.Cost ?? 0m;
        var currencyBeforeEdit = order.Currency;
        var labIdBeforeEdit = order.LabId;

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

        // CORE-LAB-001: accept the money fields so a draft can actually be completed.
        // Only an itemless order takes an explicit cost — when items exist they are the
        // source of truth and the block above already recomputed the total.
        var hasItems = order.Items != null && order.Items.Any(i => i.IsActive);
        if (req.Cost.HasValue && !hasItems)
        {
            order.Cost = req.Cost.Value;
            order.TotalCost = req.Cost.Value;
        }

        if (!string.IsNullOrWhiteSpace(req.Currency))
        {
            var newCurrency = req.Currency.Trim().ToUpperInvariant();
            order.Currency = newCurrency;
            order.ExchangeRateToYer = newCurrency == "YER"
                ? 1m
                : req.ExchangeRateToYer ?? order.ExchangeRateToYer;
        }
        else if (req.ExchangeRateToYer.HasValue
                 && !string.Equals(order.Currency, "YER", StringComparison.OrdinalIgnoreCase))
        {
            order.ExchangeRateToYer = req.ExchangeRateToYer.Value;
        }

        // CORE-LAB-001: keep the supplier bill / payable / ledger in step with whatever
        // the order now says. Idempotent — repeated saves converge on one bill, and the
        // whole thing commits with the order so a failure cannot leave a half-linked row.
        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            // Saving a draft must not create an expense or supplier debt. "sent" is
            // the financially recognised state that this edit endpoint still permits.
            if (string.Equals(order.Status, "sent", StringComparison.OrdinalIgnoreCase))
            {
                var sync = await financeSync.SyncAsync(order, order.BranchId ?? currentUser.BranchId ?? Guid.Empty,
                    currentUser.UserId ?? Guid.Empty);
                if (!sync.Ok)
                {
                    if (useTx) await tx!.RollbackAsync();
                    return BadRequest(new { message = sync.Error });
                }
            }

            // CORE-FIN-LAB-ADJ: runs for EVERY status, not just "sent". Pulling an order back to
            // draft, or lowering its cost, has to release the deduction it was making against the
            // doctor's commission — a guard here would leave the doctor short for a bill the
            // clinic no longer owes. Idempotent, and inside this transaction so the order edit and
            // the commission correction it caused commit together.
            await commissionAdjustments.ResyncLabOrderAsync(order.Id, currentUser.UserId);

            await db.SaveChangesAsync();
            if (useTx) await tx!.CommitAsync();
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        // Rule #10: log once the change is actually committed. Only fires when the order
        // was already "sent" (a real financial commitment) and something money-relevant
        // moved — editing a still-draft order is normal data entry, not a post-approval change.
        var costAfterEdit = order.TotalCost ?? order.Cost ?? 0m;
        if (wasSent && (costAfterEdit != costBeforeEdit
                        || !string.Equals(currencyBeforeEdit, order.Currency, StringComparison.OrdinalIgnoreCase)
                        || labIdBeforeEdit != order.LabId))
        {
            await audit.LogAsync(AuditAction.Update, "LabOrder", order.Id,
                oldData: new { cost = costBeforeEdit, currency = currencyBeforeEdit, labId = labIdBeforeEdit },
                newData: new { cost = costAfterEdit, currency = order.Currency, labId = order.LabId },
                details: $"تعديل تكلفة/معمل طلب معمل مُرسَل {order.OrderNumber} بعد الاعتماد");
        }

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

        // CORE-LAB-001/002: Items are needed both to validate "ready to send" and to
        // price the order, so load them here instead of a bare FindAsync.
        var order = await db.LabOrders
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (order is null) return NotFound(new { message = "طلب المختبر غير موجود" });

        // SEC-ROUTE: per-patient access check before mutating. The route only carries the
        // order id ({id:guid}/status), so the class-level PatientAccessFilter never sees a
        // patientId for this action. Resolve it from the fetched order and check explicitly.
        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        var oldStatus = order.Status;
        if (!CanTransition(oldStatus, nextStatus))
            return BadRequest(new { message = $"لا يمكن نقل الطلب من {oldStatus} إلى {nextStatus}" });

        // CORE-LAB-002: gate every entry into "sent" (draft -> sent AND remake -> sent),
        // not just the draft one — a remake is re-sent to a lab and must be just as
        // complete. Blocking here is what makes the incomplete draft a dead end rather
        // than something that quietly reaches the lab queue unbilled.
        if (string.Equals(nextStatus, "sent", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(oldStatus, "sent", StringComparison.OrdinalIgnoreCase))
        {
            var notReady = ValidateReadyToSend(order);
            if (notReady is not null)
                return BadRequest(new { message = notReady });
        }

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

        // CORE-LAB-001: entering "sent" is the point the order becomes a real commitment
        // to the lab, so make sure its financial trail exists. This is idempotent — if
        // Create or Update already built the bill this is a no-op — and it catches any
        // order that acquired a lab/cost through a path that did not sync.
        var useSendTx = db.Database.IsRelational();
        var sendTx = useSendTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            if (string.Equals(nextStatus, "sent", StringComparison.OrdinalIgnoreCase))
            {
                var sync = await financeSync.SyncAsync(order,
                    order.BranchId ?? currentUser.BranchId ?? Guid.Empty,
                    currentUser.UserId ?? Guid.Empty);
                if (!sync.Ok)
                {
                    if (useSendTx) await sendTx!.RollbackAsync();
                    return BadRequest(new { message = sync.Error });
                }
            }

            // CORE-FIN-LAB-ADJ: the status IS the commitment. Crossing draft → sent makes the
            // cost deductible from the doctor's commission for the first time, and any move back
            // out of a committed state releases it again — so this runs on every transition, not
            // only the one that touches the supplier bill.
            await commissionAdjustments.ResyncLabOrderAsync(order.Id, currentUser.UserId);

            await db.SaveChangesAsync();
            if (useSendTx) await sendTx!.CommitAsync();
        }
        catch
        {
            if (useSendTx) await sendTx!.RollbackAsync();
            throw;
        }
        finally
        {
            if (sendTx is not null) await sendTx.DisposeAsync();
        }

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

        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            var financeCancellation = await financeSync.CancelAsync(
                order,
                currentUser.UserId ?? Guid.Empty);
            if (!financeCancellation.Ok)
            {
                if (useTx) await tx!.RollbackAsync();
                return BadRequest(new { message = financeCancellation.Error });
            }

            var oldStatus = order.Status;
            order.Status = "cancelled";
            order.CancellationReason = req.Reason;
            db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
            {
                LabOrderId = id, FromStatus = oldStatus, ToStatus = "cancelled",
                ChangedByUserId = currentUser.UserId, Reason = req.Reason
            });

            // CORE-FIN-LAB-ADJ: a cancelled order is not owed, so it must stop being deducted
            // from the doctor's commission. Where that commission was already paid the money is
            // not clawed back silently — a positive correction line is raised for the next
            // settlement instead.
            await commissionAdjustments.ResyncLabOrderAsync(order.Id, currentUser.UserId);

            await db.SaveChangesAsync();
            if (useTx) await tx!.CommitAsync();
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

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

        if (!req.IsFreeRemake && (!req.RemakeCost.HasValue || req.RemakeCost.Value <= 0m))
            return BadRequest(new { message = "يجب إدخال تكلفة إضافية أكبر من صفر لإعادة الصنع غير المجانية، أو تحديدها كإعادة مجانية" });

        // CORE-LAB-007: a paid remake used to record RemakeCost on the order without ever
        // adding it to TotalCost/Cost — the field that LabOrderFinanceSyncService.SyncAsync
        // reads when the remake is re-sent to the lab. The extra cost was captured but never
        // billed: no additional supplier debt, no extra expense, and no larger deduction from
        // the doctor's commission. A free remake must add nothing; folding the cost in here
        // (once, guarded by the "returned" status check above) keeps it correct either way.
        var oldStatus = order.Status;
        var costBefore = order.TotalCost ?? order.Cost ?? 0m;
        order.Status = "remake";
        order.RemakeReason = req.Reason;
        order.IsFreeRemake = req.IsFreeRemake;
        order.RemakeCost = req.IsFreeRemake ? null : req.RemakeCost;
        order.RemakeCount += 1;
        order.SentDate = ClinicTimeProvider.ClinicToday();

        if (!req.IsFreeRemake && req.RemakeCost is > 0m)
        {
            var newTotal = costBefore + req.RemakeCost.Value;
            order.TotalCost = newTotal;
            order.Cost = newTotal;
        }

        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            LabOrderId = id, FromStatus = oldStatus, ToStatus = "remake",
            ChangedByUserId = currentUser.UserId, Reason = req.Reason
        });
        await db.SaveChangesAsync();

        // CORE-LAB-007 / rule #10: any lab-cost change after the order was first sent
        // to the lab (which "returned" implies) must leave an audit trail, not just a
        // silent field update — this is money moving, not a cosmetic edit.
        await audit.LogAsync(AuditAction.Update, "LabOrder", id,
            oldData: new { status = oldStatus, cost = costBefore },
            newData: new { status = "remake", cost = order.TotalCost, order.IsFreeRemake, order.RemakeCost, order.RemakeCount },
            details: req.IsFreeRemake
                ? $"إعادة صناعة مجانية للطلب {order.OrderNumber} — السبب: {req.Reason}"
                : $"إعادة صناعة بتكلفة إضافية {req.RemakeCost:N0} للطلب {order.OrderNumber} — السبب: {req.Reason}");

        return Ok(new { id, status = "remake", order.RemakeCount, order.TotalCost });
    }

    // ─── Lab Sprint 4 — Status history ───────────────────────────────────────
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        // CORE-LAB-006: this route carries only the order id, so PatientAccessFilter
        // never gates it — check the owning patient explicitly.
        var deniedAccess = await DenyIfCannotAccessOrderAsync(id);
        if (deniedAccess is not null) return deniedAccess;

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

        // CORE-LAB-006: this route carries only the order id, so PatientAccessFilter
        // never gates it — check the owning patient explicitly.
        var deniedAccess = await DenyIfCannotAccessOrderAsync(id);
        if (deniedAccess is not null) return deniedAccess;

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

        // CORE-LAB-006: the order is already loaded here, so check its owning patient
        // directly — this route carries only the order id and PatientAccessFilter
        // therefore never gates it.
        var deniedAccess = await DenyIfDoctorCannotAccess(order.PatientId);
        if (deniedAccess is not null) return deniedAccess;

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

        // CORE-LAB-006: this route carries only the order id, so PatientAccessFilter
        // never gates it — check the owning patient explicitly.
        var deniedAccess = await DenyIfCannotAccessOrderAsync(id);
        if (deniedAccess is not null) return deniedAccess;

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

        // CORE-LAB-006: this route carries only the order id, so PatientAccessFilter
        // never gates it — check the owning patient explicitly.
        var deniedAccess = await DenyIfCannotAccessOrderAsync(id);
        if (deniedAccess is not null) return deniedAccess;

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

        // CORE-LAB-006: soft-deleting another patient's lab order is a cross-patient
        // mutation; this route carries only the order id so PatientAccessFilter never gates it.
        var deniedAccess = await DenyIfDoctorCannotAccess(order.PatientId);
        if (deniedAccess is not null) return deniedAccess;

        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            var financeCancellation = await financeSync.CancelAsync(
                order,
                currentUser.UserId ?? Guid.Empty);
            if (!financeCancellation.Ok)
            {
                if (useTx) await tx!.RollbackAsync();
                return BadRequest(new { message = financeCancellation.Error });
            }

            order.IsActive = false;
            order.DeletedAt = DateTime.UtcNow;
            order.DeletedBy = currentUser.UserId;

            // CORE-FIN-LAB-ADJ: same reasoning as Cancel — a deleted order stops being a cost,
            // so it must stop reducing the doctor's commission.
            await commissionAdjustments.ResyncLabOrderAsync(order.Id, currentUser.UserId);

            await db.SaveChangesAsync();
            if (useTx) await tx!.CommitAsync();
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        return Ok(new { message = "تم حذف الطلب بنجاح" });
    }

    // ─── Lab Sprint 5 — PDF Generation ─────────────────────────────────────────
    /// <summary>Generates a PDF for the lab order.</summary>
    [HttpGet("{id:guid}/print")]
    public async Task<IActionResult> PrintPdf(Guid id)
    {
        if (!await CanAsync("export")) return Forbid();

        // CORE-LAB-006: the generated PDF carries the patient's name, file number,
        // treating doctor and clinical details. Exporting it is a read of that patient's
        // record, and this route carries only the order id — PatientAccessFilter never
        // fires on it, so the check must be explicit.
        var deniedAccess = await DenyIfCannotAccessOrderAsync(id);
        if (deniedAccess is not null) return deniedAccess;

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
