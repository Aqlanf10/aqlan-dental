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
    // CORE-FIN-LAB — resolves the invoice line this order's cost belongs to, so the
    // doctor's commission deducts the REAL lab cost instead of the service default.
    // Links only when the answer is unambiguous; see LabOrderInvoiceLinkService.
    LabOrderInvoiceLinkService invoiceLink,
    IJournalEntryService? journalEntryService = null) : ControllerBase
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

    // ─── CORE-FIN-LAB — admin fix-up list: orders with no invoice line ──────
    /// <summary>Row of <c>GET /api/lab-orders/unlinked</c>.</summary>
    public sealed record UnlinkedLabOrderDto
    {
        public Guid Id { get; init; }
        public string? OrderNumber { get; init; }
        public Guid PatientId { get; init; }
        public string PatientName { get; init; } = string.Empty;
        public string? PatientNumber { get; init; }
        public Guid? VisitId { get; init; }
        public string? VisitDate { get; init; }
        public string? DoctorName { get; init; }
        /// <summary>TotalCost when present, else the Cost snapshot (CLIN-08 ordering).</summary>
        public decimal? Cost { get; init; }
        /// <summary>Never sum these across rows — YER/SAR/USD are different money.</summary>
        public string Currency { get; init; } = "YER";
        public string Status { get; init; } = string.Empty;
        /// <summary>Stable machine code for the reason (see <c>Reason</c> for the Arabic text).</summary>
        public string ReasonCode { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        /// <summary>How many invoice lines were plausible — only meaningful when ambiguous.</summary>
        public int CandidateCount { get; init; }
    }

    /// <summary>
    /// Lists lab orders that carry NO invoice line link, with the reason each one could
    /// not be resolved automatically, so staff can repair them by hand.
    /// <para>
    /// This list is the deliberate other half of "link only when unambiguous". The
    /// resolver refuses to guess, because attaching a lab cost to the wrong invoice line
    /// silently corrupts a DIFFERENT doctor's commission. The cost of that refusal is
    /// orders that stay unlinked — and an unlinked order that nobody can see is a silent
    /// gap in the commission figures, which is what this endpoint exists to prevent.
    /// </para>
    /// <para>
    /// The reason is produced by calling the SAME resolver the create path uses, once per
    /// row, instead of re-deriving the rule here. A second implementation would drift and
    /// then explain outcomes that never happened. It costs a couple of queries per row,
    /// bounded by the page size — acceptable for an admin screen, and the alternative is
    /// a list that lies.
    /// </para>
    /// </summary>
    [HttpGet("unlinked")]
    // Administrative fix-up over commission-affecting financial data — Admin only, using
    // the existing AdminOnly policy (the class-level StaffOnly still applies on top).
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetUnlinked([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // Kept alongside the policy, exactly like every other action here: if the policy
        // is ever widened beyond Admin, the per-resource permission still gates this.
        if (!await CanAsync("view")) return Forbid();

        // Clamp both: a page below 1 would produce a negative Skip and throw.
        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 100));

        var query = db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Include(l => l.Visit)
            // A cancelled order owes the lab nothing and deducts nothing from any
            // commission, so linking it would fix nothing — it is not a fix-up item.
            .Where(l => l.Status != "cancelled"
                     && !db.InvoiceLineItems.Any(line => line.LabOrderId == l.Id));

        // PHI SURFACE: these rows carry patient names and file numbers. The class-level
        // PatientAccessFilter only fires on a route/query value named "patientId", which
        // this endpoint has none of, so the doctor filter must be explicit — same pattern
        // as RadiologyOrdersController/PatientsController, fail-closed on error rather
        // than returning an unfiltered page. Unreachable while the policy is Admin-only;
        // it is here so widening the policy cannot silently open a cross-patient read.
        if (patientAccess.IsDoctor)
        {
            HashSet<Guid> accessible;
            try
            {
                accessible = await patientAccess.GetAccessiblePatientIdsAsync() ?? [];
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[LabOrders] Accessible-patient set failed for unlinked list, user {UserId}", currentUser.UserId);
                return StatusCode(500, new { message = "تعذر تحميل طلبات المختبر غير المرتبطة حالياً" });
            }
            query = query.Where(l => accessible.Contains(l.PatientId));
        }

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        var rows = new List<UnlinkedLabOrderDto>(orders.Count);
        foreach (var order in orders)
        {
            LabOrderInvoiceLinkStatus? linkStatus = null;
            var candidateCount = 0;
            try
            {
                var link = await invoiceLink.ResolveAsync(order);
                linkStatus = link.Status;
                candidateCount = link.CandidateCount;
            }
            catch (Exception ex)
            {
                // One unreadable row must not take down the whole fix-up list; it is
                // reported with an explicit "unknown" reason instead of being dropped.
                logger.LogWarning(ex,
                    "[LabOrders] Could not determine unlink reason for {OrderNumber}", order.OrderNumber);
            }

            var (reasonCode, reason) = DescribeUnlinkReason(linkStatus, candidateCount);

            rows.Add(new UnlinkedLabOrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                PatientId = order.PatientId,
                PatientName = order.Patient != null
                    ? $"{order.Patient.FirstName} {order.Patient.LastName}".Trim()
                    : string.Empty,
                PatientNumber = order.Patient?.PatientNumber,
                VisitId = order.VisitId,
                VisitDate = order.Visit?.VisitDate.ToString("yyyy-MM-dd"),
                DoctorName = order.Doctor?.Name,
                Cost = order.TotalCost ?? order.Cost,
                Currency = order.Currency,
                Status = order.Status,
                ReasonCode = reasonCode,
                Reason = reason,
                CandidateCount = candidateCount,
            });
        }

        return Ok(new { data = rows, total, page, pageSize });
    }

    /// <summary>
    /// Turns a resolver outcome into a stable code plus the Arabic sentence shown to
    /// staff. <c>Resolved</c> appears here too: the order is linkable but was created
    /// before automatic linking existed, which is a different (and easier) repair than
    /// an ambiguous one — reporting it as "no candidate" would be a lie.
    /// </summary>
    private static (string Code, string Text) DescribeUnlinkReason(
        LabOrderInvoiceLinkStatus? status, int candidateCount) => status switch
    {
        LabOrderInvoiceLinkStatus.NoVisit =>
            ("no_visit", "الطلب غير مرتبط بزيارة، ولا توجد وسيلة لتحديد بند الفاتورة تلقائياً. اربطه ببند الفاتورة يدوياً."),
        LabOrderInvoiceLinkStatus.NoCandidate =>
            ("no_candidate", "لا يوجد بند فاتورة متاح على زيارة الطلب. تأكد من إصدار فاتورة الخدمة لهذه الزيارة."),
        LabOrderInvoiceLinkStatus.Ambiguous =>
            ("ambiguous", $"زيارة الطلب تحمل {candidateCount} بنود فاتورة محتملة، ولم يُربط تلقائياً تفادياً لتحميل التكلفة على خدمة أخرى. حدّد البند الصحيح يدوياً."),
        LabOrderInvoiceLinkStatus.PatientMismatch =>
            ("patient_mismatch", "بند الفاتورة المرشح يخص مريضاً آخر، لذلك رُفض الربط. راجع بيانات الزيارة والفاتورة."),
        LabOrderInvoiceLinkStatus.Resolved =>
            ("resolvable", "يوجد بند فاتورة واحد مطابق ويمكن ربط الطلب به. الطلب أُنشئ قبل تفعيل الربط التلقائي."),
        _ =>
            ("unknown", "تعذر تحديد سبب عدم الربط. راجع سجل النظام."),
    };

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
            // CORE-FIN-LAB — the invoice line this attempt linked (if any). Kept out of
            // the try so the unique-violation retry can undo the link before building a
            // brand-new order with a different Id.
            InvoiceLineItem? linkedLineItem = null;
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

                // ── CORE-FIN-LAB — link the order to the invoice line it pays for ────
                // InvoiceLineItem.LabOrderId is the source of truth (the only side EF
                // configures and the only side CommissionService reads); LabOrder
                // .InvoiceLineItemId is an abandoned column and stays untouched.
                //
                // The assignment happens INSIDE this transaction and before SaveChanges,
                // so the order and its link commit together — a committed order with a
                // dangling link (or the reverse) would be worse than no link at all.
                //
                // Not resolving is NORMAL, never an error: without a visit, without a
                // matching invoice line, or with several plausible lines on the same
                // visit, the order stays unlinked and surfaces in the admin fix-up list.
                // Guessing would attach this patient's lab cost to another service and
                // silently corrupt a DIFFERENT doctor's commission.
                try
                {
                    var link = await invoiceLink.ResolveAsync(order);
                    if (link.IsResolved)
                    {
                        link.LineItem!.LabOrderId = order.Id;
                        linkedLineItem = link.LineItem;
                    }
                    else
                    {
                        logger.LogInformation(
                            "[LabOrders] Lab order {OrderNumber} left unlinked to an invoice line ({LinkStatus}, candidates={CandidateCount})",
                            order.OrderNumber, link.Status, link.CandidateCount);
                    }
                }
                catch (Exception ex)
                {
                    // The link is an optimisation of the commission figure, not part of
                    // the order itself — a failure here must never block creating it.
                    logger.LogWarning(ex,
                        "[LabOrders] Invoice-line resolution failed for {OrderNumber}; order created unlinked",
                        order.OrderNumber);
                }

                // A draft is not yet a commitment to the lab. Build the payable only
                // when the order is actually sent; UpdateStatus owns the same boundary.
                if (order.Status == "sent"
                    && order.LabId.HasValue
                    && (order.TotalCost > 0 || order.Cost > 0))
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
                    // CORE-FIN-LAB: the rolled-back order Id must not stay on the line —
                    // the next attempt builds a NEW order, and a leftover link would point
                    // at a row that was never committed.
                    if (linkedLineItem is not null) linkedLineItem.LabOrderId = null;
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
        if (string.Equals(nextStatus, "sent", StringComparison.OrdinalIgnoreCase))
        {
            var useSendTx = db.Database.IsRelational();
            var sendTx = useSendTx ? await db.Database.BeginTransactionAsync() : null;
            try
            {
                var sync = await financeSync.SyncAsync(order,
                    order.BranchId ?? currentUser.BranchId ?? Guid.Empty,
                    currentUser.UserId ?? Guid.Empty);
                if (!sync.Ok)
                {
                    if (useSendTx) await sendTx!.RollbackAsync();
                    return BadRequest(new { message = sync.Error });
                }

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
        }
        else
        {
            await db.SaveChangesAsync();
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
