using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Sprint 12 — READ-side query service for lab orders.
///
/// Extracted from <c>LabOrdersController</c> to shrink the controller (1401 lines)
/// and establish a maintainability pattern for the other backend hot spots
/// (FinanceService, OrthoCasesController, FinanceV3Controller) that are deferred
/// to future PRs.
///
/// <para><b>Scope:</b> all GET / read endpoints under <c>/api/lab-orders</c>:</para>
/// <list type="bullet">
///   <item><c>GET /</c> — list with patient/status filter + pagination</item>
///   <item><c>GET /pending-count</c></item>
///   <item><c>GET /today</c></item>
///   <item><c>GET /ready</c></item>
///   <item><c>GET /overdue</c></item>
///   <item><c>GET /ready-for-delivery</c></item>
///   <item><c>GET /{id}</c> — detail with items</item>
///   <item><c>GET /{id}/history</c> — status history</item>
///   <item><c>GET /{id}/attachments</c> — attachment list</item>
/// </list>
///
/// <para><b>Authorization-aware.</b> Every query is constrained by
/// <see cref="IBranchResourceScope"/>. The controller still applies action
/// permissions and doctor/patient ownership checks before delegating.</para>
///
/// <para><b>API contract unchanged.</b> DTO property names match the original
/// anonymous-type projections exactly, so the JSON response shape is identical
/// (same camelCase keys, same null/non-null fields per endpoint).</para>
///
/// <para><b>Query behavior unchanged within the authorized branch.</b> The
/// filters, ordering, schema fallback (PostgreSQL 42P01/42703), and
/// <c>ClinicToday()</c> handling remain the same.</para>
/// </summary>
public class LabOrderQueryService
{
    private readonly AppDbContext _db;
    private readonly IBranchResourceScope _branchScope;
    private readonly ILogger<LabOrderQueryService> _logger;

    public LabOrderQueryService(
        AppDbContext db,
        IBranchResourceScope branchScope,
        ILogger<LabOrderQueryService> logger)
    {
        _db = db;
        _branchScope = branchScope;
        _logger = logger;
    }

    private IQueryable<LabOrder> ScopedOrders()
    {
        var query = _db.LabOrders.AsQueryable();
        if (_branchScope.HasGlobalAccess)
            return query;

        var branchId = _branchScope.EffectiveBranchId;
        return branchId.HasValue && branchId.Value != Guid.Empty
            ? query.Where(order => order.BranchId == branchId.Value)
            : query.Where(_ => false);
    }

    /// <summary>
    /// LABINV-REQ-008 — resolves a scanned order number to its id, or null.
    ///
    /// <para>
    /// Deliberately goes through <see cref="ScopedOrders"/> like every other read here, so
    /// a code belonging to another branch resolves to null rather than to an order the
    /// scanner's user may not see. The caller must turn null into the same response it
    /// gives for a code that matches nothing at all: a scanner is an enumeration surface,
    /// and distinguishing "not yours" from "does not exist" would let anyone holding a
    /// printed slip probe which order numbers are real.
    /// </para>
    ///
    /// <para>
    /// Matching is case-insensitive and ignores surrounding whitespace, because the code
    /// arrives from a camera decode or a barcode wedge, not from a form.
    /// </para>
    /// </summary>
    public async Task<Guid?> FindIdByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        var code = (orderNumber ?? string.Empty).Trim();
        if (code.Length == 0) return null;

        var match = await ScopedOrders()
            .Where(order => order.OrderNumber != null
                         && order.OrderNumber.ToLower() == code.ToLower())
            .Select(order => (Guid?)order.Id)
            .FirstOrDefaultAsync(ct);

        return match;
    }

    // ─── GET /api/lab-orders ────────────────────────────────────────────────

    /// <summary>
    /// Paged list with optional patient/status filter. Returns the page items plus
    /// the unfiltered total count (controller wraps as <c>{ data, total, page, pageSize }</c>).
    /// </summary>
    public async Task<(List<LabOrderListItemDto> Orders, int Total)> GetAllAsync(
        Guid? patientId,
        string? status,
        int page,
        int pageSize)
    {
        var query = ScopedOrders()
            .Include(l => l.Patient)
            .Include(l => l.OrthoCase)
            .Include(l => l.Doctor)
            .Include(l => l.Lab)
            .AsQueryable();

        if (patientId.HasValue) query = query.Where(l => l.PatientId == patientId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LabOrderListItemDto
            {
                Id = l.Id,
                OrderNumber = l.OrderNumber,
                PatientId = l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                OrthoCaseNumber = l.OrthoCase != null ? l.OrthoCase.CaseNumber : null,
                ApplianceType = l.ApplianceType,
                LabName = l.LabName,
                LabEntityName = l.Lab != null ? l.Lab.Name : null,
                LabId = l.LabId,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                Status = l.Status,
                Priority = l.Priority,
                Cost = l.Cost,
                TotalCost = l.TotalCost,
                Currency = l.Currency,
                ExchangeRateToYer = l.ExchangeRateToYer,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                Shade = l.Shade,
                RestorationType = l.RestorationType,
                VisitId = l.VisitId,
                CancellationReason = l.CancellationReason,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return (orders, total);
    }

    // ─── GET /api/lab-orders/pending-count ──────────────────────────────────

    public async Task<int> GetPendingCountAsync()
    {
        return await ScopedOrders()
            .CountAsync(l => l.Status == "sent" || l.Status == "manufacturing" || l.Status == "tryIn" || l.Status == "remake");
    }

    // ─── GET /api/lab-orders/today ──────────────────────────────────────────

    /// <summary>Returns lab orders where any key date matches today.</summary>
    public async Task<List<LabOrderTodayDto>> GetTodayAsync()
    {
        var today = ClinicTimeProvider.ClinicToday();
        return await ScopedOrders()
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Where(l => l.SentDate == today || l.ExpectedDate == today || l.ReceivedDate == today || l.DeliveredDate == today)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LabOrderTodayDto
            {
                Id = l.Id,
                OrderNumber = l.OrderNumber,
                PatientId = l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                ApplianceType = l.ApplianceType,
                LabName = l.LabName,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                Status = l.Status,
                Priority = l.Priority,
                Cost = l.Cost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                Shade = l.Shade,
                RestorationType = l.RestorationType,
                VisitId = l.VisitId,
                CancellationReason = l.CancellationReason,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
    }

    // ─── GET /api/lab-orders/ready ──────────────────────────────────────────

    /// <summary>Returns lab orders that are ready or received (awaiting patient delivery).</summary>
    public async Task<List<LabOrderTodayDto>> GetReadyAsync()
    {
        return await ScopedOrders()
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Where(l => l.Status == "ready" || l.Status == "received")
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LabOrderTodayDto
            {
                Id = l.Id,
                OrderNumber = l.OrderNumber,
                PatientId = l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                ApplianceType = l.ApplianceType,
                LabName = l.LabName,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                Status = l.Status,
                Priority = l.Priority,
                Cost = l.Cost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                Shade = l.Shade,
                RestorationType = l.RestorationType,
                VisitId = l.VisitId,
                CancellationReason = l.CancellationReason,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
    }

    // ─── GET /api/lab-orders/overdue ────────────────────────────────────────

    /// <summary>Returns lab orders past their expected date and not yet delivered/cancelled.</summary>
    public async Task<List<LabOrderOverdueDto>> GetOverdueAsync()
    {
        var today = ClinicTimeProvider.ClinicToday();
        return await ScopedOrders()
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Include(l => l.Lab)
            .Where(l => l.ExpectedDate != null
                && l.ExpectedDate < today
                && l.Status != "delivered"
                && l.Status != "cancelled")
            .OrderBy(l => l.ExpectedDate)
            .Select(l => new LabOrderOverdueDto
            {
                Id = l.Id,
                OrderNumber = l.OrderNumber,
                PatientId = l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                ApplianceType = l.ApplianceType,
                LabName = l.LabName,
                LabEntityName = l.Lab != null ? l.Lab.Name : null,
                LabId = l.LabId,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                Status = l.Status,
                Priority = l.Priority,
                Cost = l.Cost,
                TotalCost = l.TotalCost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                Shade = l.Shade,
                RestorationType = l.RestorationType,
                DaysOverdue = l.ExpectedDate != null ? (int)(today.DayNumber - l.ExpectedDate.Value.DayNumber) : 0,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
    }

    // ─── GET /api/lab-orders/ready-for-delivery ─────────────────────────────

    /// <summary>Returns lab orders that are received and ready for patient delivery.</summary>
    public async Task<List<LabOrderReadyForDeliveryDto>> GetReadyForDeliveryAsync()
    {
        return await ScopedOrders()
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Include(l => l.Lab)
            .Where(l => l.Status == "received")
            .OrderBy(l => l.ReceivedDate)
            .Select(l => new LabOrderReadyForDeliveryDto
            {
                Id = l.Id,
                OrderNumber = l.OrderNumber,
                PatientId = l.PatientId,
                PatientName = l.Patient.FirstName + " " + l.Patient.LastName,
                PatientNumber = l.Patient.PatientNumber,
                ApplianceType = l.ApplianceType,
                LabName = l.LabName,
                LabEntityName = l.Lab != null ? l.Lab.Name : null,
                LabId = l.LabId,
                SentDate = l.SentDate != null ? l.SentDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDate = l.ExpectedDate != null ? l.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                ReceivedDate = l.ReceivedDate != null ? l.ReceivedDate.Value.ToString("yyyy-MM-dd") : null,
                DeliveredDate = l.DeliveredDate != null ? l.DeliveredDate.Value.ToString("yyyy-MM-dd") : null,
                Status = l.Status,
                Priority = l.Priority,
                Cost = l.Cost,
                TotalCost = l.TotalCost,
                DoctorName = l.Doctor != null ? l.Doctor.Name : null,
                Shade = l.Shade,
                RestorationType = l.RestorationType,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
    }

    // ─── GET /api/lab-orders/{id} ───────────────────────────────────────────

    /// <summary>
    /// Single-lab-order detail with line items. Returns <c>null</c> if the order
    /// does not exist (controller maps that to a 404 with the Arabic message).
    /// Includes the same schema-mismatch fallback as the original controller action:
    /// if <c>LabOrderItems</c> / <c>LabWorkTypes</c> are missing (PostgreSQL 42P01/42703),
    /// re-queries without <c>Items</c> and logs a warning.
    /// </summary>
    public async Task<LabOrderDetailDto?> GetByIdAsync(Guid id)
    {
        LabOrder? order;
        try
        {
            order = await ScopedOrders()
                .Include(l => l.Patient)
                .Include(l => l.OrthoCase)
                .Include(l => l.Doctor)
                .Include(l => l.Lab)
                .Include(l => l.Items).ThenInclude(i => i.WorkType)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
        catch (Exception ex) when (IsMissingTableOrColumnError(ex))
        {
            _logger.LogWarning(ex, "LabOrderItems/WorkType query failed (schema mismatch) — falling back to query without Items for lab order {OrderId}. Error: {ErrorMsg}", id, ex.InnerException?.Message ?? ex.Message);
            order = await ScopedOrders()
                .Include(l => l.Patient)
                .Include(l => l.OrthoCase)
                .Include(l => l.Doctor)
                .Include(l => l.Lab)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
        // NOTE: unexpected (non-schema) exceptions are NOT caught here — they
        // propagate up to the controller, which logs them and returns an Arabic
        // 500 (preserving the original GetById behavior).

        if (order is null) return null;

        return new LabOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            PatientId = order.PatientId,
            PatientName = order.Patient.FirstName + " " + order.Patient.LastName,
            PatientNumber = order.Patient.PatientNumber,
            OrthoCaseNumber = order.OrthoCase?.CaseNumber,
            ApplianceType = order.ApplianceType,
            LabName = order.LabName,
            LabEntityName = order.Lab?.Name,
            LabId = order.LabId,
            SentDate = order.SentDate?.ToString("yyyy-MM-dd"),
            ExpectedDate = order.ExpectedDate?.ToString("yyyy-MM-dd"),
            ReceivedDate = order.ReceivedDate?.ToString("yyyy-MM-dd"),
            DeliveredDate = order.DeliveredDate?.ToString("yyyy-MM-dd"),
            Status = order.Status,
            Priority = order.Priority,
            Instructions = order.Instructions,
            Cost = order.Cost,
            TotalCost = order.TotalCost,
            DoctorName = order.Doctor?.Name,
            Shade = order.Shade,
            RestorationType = order.RestorationType,
            VisitId = order.VisitId,
            CancellationReason = order.CancellationReason,
            CreatedAt = order.CreatedAt.ToString("yyyy-MM-dd"),
            Items = order.Items
                .Select(i => new LabOrderItemDto
                {
                    Id = i.Id,
                    WorkTypeId = i.WorkTypeId,
                    WorkTypeName = i.WorkType != null ? i.WorkType.Name : null,
                    ToothNumber = i.ToothNumber,
                    Arch = i.Arch,
                    Shade = i.Shade,
                    RestorationType = i.RestorationType,
                    UnitsCount = i.UnitsCount,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    Instructions = i.Instructions,
                    SortOrder = i.SortOrder
                })
                .OrderBy(i => i.SortOrder)
                .ToList()
        };
    }

    // ─── GET /api/lab-orders/{id}/history ───────────────────────────────────

    /// <summary>
    /// Status history for a lab order. Returns <c>null</c> if the order itself
    /// does not exist (controller maps to 404); otherwise a (possibly empty)
    /// list of history entries.
    /// </summary>
    public async Task<List<LabOrderStatusHistoryDto>?> GetHistoryAsync(Guid id)
    {
        // Mirrors original controller: existence check before listing history.
        var order = await ScopedOrders().FirstOrDefaultAsync(order => order.Id == id);
        if (order is null) return null;

        return await _db.LabOrderStatusHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.LabOrderId == id && ScopedOrders().Any(order => order.Id == h.LabOrderId))
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new LabOrderStatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                ChangedByName = h.ChangedByUser != null ? h.ChangedByUser.Username : null,
                Reason = h.Reason,
                CreatedAt = h.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();
    }

    // ─── GET /api/lab-orders/{id}/attachments ───────────────────────────────

    /// <summary>
    /// Attachment list for a lab order. Original controller did NOT check order
    /// existence — empty list if no attachments (preserved here).
    /// </summary>
    public async Task<List<LabOrderAttachmentDto>> GetAttachmentsAsync(Guid id)
    {
        return await _db.LabOrderAttachments
            .Where(a => a.LabOrderId == id && ScopedOrders().Any(order => order.Id == a.LabOrderId))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new LabOrderAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSize = a.FileSize,
                Category = a.Category,
                LabOrderItemId = a.LabOrderItemId,
                StoragePath = a.StoragePath,
                UploadedByName = a.UploadedByUser != null ? a.UploadedByUser.Username : null,
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();
    }

    // ─── Shared schema-mismatch helper ──────────────────────────────────────
    // Extracted verbatim from LabOrdersController.IsMissingTableOrColumnError.
    // Used by GetByIdAsync (read path) — also still used directly by the controller
    // for Update / PrintPdf (which are NOT extracted in Sprint 12).

    /// <summary>
    /// Checks if an exception is caused by a missing database table or column
    /// (PostgreSQL 42P01/42703). Allows graceful fallback when LabOrderItems or
    /// related tables don't exist yet.
    /// </summary>
    public static bool IsMissingTableOrColumnError(Exception ex)
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
}

// ─── DTOs ────────────────────────────────────────────────────────────────────
// Property names match the original controller's anonymous-type projections
// using the API's camelCase JSON naming. Each DTO corresponds to one endpoint's
// response shape.

/// <summary>
/// GET /api/lab-orders — list item shape. Financial fields are included so an
/// edit form can round-trip SAR/USD orders without silently defaulting to YER.
/// </summary>
public sealed record LabOrderListItemDto
{
    public Guid Id { get; init; }
    public string? OrderNumber { get; init; }
    public Guid PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string? PatientNumber { get; init; }
    public string? OrthoCaseNumber { get; init; }
    public string? ApplianceType { get; init; }
    public string? LabName { get; init; }
    public string? LabEntityName { get; init; }
    public Guid? LabId { get; init; }
    public string? SentDate { get; init; }
    public string? ExpectedDate { get; init; }
    public string? ReceivedDate { get; init; }
    public string? DeliveredDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public decimal? Cost { get; init; }
    public decimal? TotalCost { get; init; }
    public string Currency { get; init; } = "YER";
    public decimal ExchangeRateToYer { get; init; } = 1m;
    public string? DoctorName { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public Guid? VisitId { get; init; }
    public string? CancellationReason { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>GET /api/lab-orders/today and /ready — shared shape (no OrthoCaseNumber, Lab, TotalCost).</summary>
public sealed record LabOrderTodayDto
{
    public Guid Id { get; init; }
    public string? OrderNumber { get; init; }
    public Guid PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string? PatientNumber { get; init; }
    public string? ApplianceType { get; init; }
    public string? LabName { get; init; }
    public string? SentDate { get; init; }
    public string? ExpectedDate { get; init; }
    public string? ReceivedDate { get; init; }
    public string? DeliveredDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public decimal? Cost { get; init; }
    public string? DoctorName { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public Guid? VisitId { get; init; }
    public string? CancellationReason { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>GET /api/lab-orders/overdue — adds Lab, TotalCost, DaysOverdue; no VisitId/CancellationReason/OrthoCaseNumber.</summary>
public sealed record LabOrderOverdueDto
{
    public Guid Id { get; init; }
    public string? OrderNumber { get; init; }
    public Guid PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string? PatientNumber { get; init; }
    public string? ApplianceType { get; init; }
    public string? LabName { get; init; }
    public string? LabEntityName { get; init; }
    public Guid? LabId { get; init; }
    public string? SentDate { get; init; }
    public string? ExpectedDate { get; init; }
    public string? ReceivedDate { get; init; }
    public string? DeliveredDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public decimal? Cost { get; init; }
    public decimal? TotalCost { get; init; }
    public string? DoctorName { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public int DaysOverdue { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>GET /api/lab-orders/ready-for-delivery — like Overdue without DaysOverdue.</summary>
public sealed record LabOrderReadyForDeliveryDto
{
    public Guid Id { get; init; }
    public string? OrderNumber { get; init; }
    public Guid PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string? PatientNumber { get; init; }
    public string? ApplianceType { get; init; }
    public string? LabName { get; init; }
    public string? LabEntityName { get; init; }
    public Guid? LabId { get; init; }
    public string? SentDate { get; init; }
    public string? ExpectedDate { get; init; }
    public string? ReceivedDate { get; init; }
    public string? DeliveredDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public decimal? Cost { get; init; }
    public decimal? TotalCost { get; init; }
    public string? DoctorName { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>GET /api/lab-orders/{id} — full detail with line items.</summary>
public sealed record LabOrderDetailDto
{
    public Guid Id { get; init; }
    public string? OrderNumber { get; init; }
    public Guid PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string? PatientNumber { get; init; }
    public string? OrthoCaseNumber { get; init; }
    public string? ApplianceType { get; init; }
    public string? LabName { get; init; }
    public string? LabEntityName { get; init; }
    public Guid? LabId { get; init; }
    public string? SentDate { get; init; }
    public string? ExpectedDate { get; init; }
    public string? ReceivedDate { get; init; }
    public string? DeliveredDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? Instructions { get; init; }
    public decimal? Cost { get; init; }
    public decimal? TotalCost { get; init; }
    public string? DoctorName { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public Guid? VisitId { get; init; }
    public string? CancellationReason { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public List<LabOrderItemDto> Items { get; init; } = new();
}

/// <summary>Line item shape nested inside <see cref="LabOrderDetailDto"/>.</summary>
public sealed record LabOrderItemDto
{
    public Guid Id { get; init; }
    public Guid WorkTypeId { get; init; }
    public string? WorkTypeName { get; init; }
    public string? ToothNumber { get; init; }
    public string? Arch { get; init; }
    public string? Shade { get; init; }
    public string? RestorationType { get; init; }
    public int UnitsCount { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? TotalPrice { get; init; }
    public string? Instructions { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>GET /api/lab-orders/{id}/history — status transition entry.</summary>
public sealed record LabOrderStatusHistoryDto
{
    public Guid Id { get; init; }
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public string? ChangedByName { get; init; }
    public string? Reason { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>GET /api/lab-orders/{id}/attachments — attachment metadata.</summary>
public sealed record LabOrderAttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Category { get; init; } = string.Empty;
    public Guid? LabOrderItemId { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public string? UploadedByName { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
