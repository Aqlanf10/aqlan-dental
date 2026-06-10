using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using Npgsql;

namespace AqlanDentalPro.API.Controllers;

// ─── Request DTOs ────────────────────────────────────────────────────────────

public sealed class CreatePurchaseOrderLineItemRequest
{
    public Guid? InventoryItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? ItemDescription { get; init; }
    public int Quantity { get; init; }
    public decimal UnitCost { get; init; }
}

public sealed class CreatePurchaseOrderLineItemRequestValidator : AbstractValidator<CreatePurchaseOrderLineItemRequest>
{
    public CreatePurchaseOrderLineItemRequestValidator()
    {
        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("اسم المادة مطلوب")
            .MaximumLength(200).WithMessage("اسم المادة يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر");

        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0).WithMessage("تكلفة الوحدة يجب أن تكون صفراً أو أكثر");

        RuleFor(x => x.ItemDescription)
            .MaximumLength(500).WithMessage("وصف المادة يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ItemDescription));
    }
}

public sealed class CreatePurchaseOrderRequest
{
    public Guid SupplierId { get; init; }
    public string? OrderDate { get; init; }
    public string? ExpectedDate { get; init; }
    public decimal TaxAmount { get; init; }
    public string? Notes { get; init; }
    public Guid? BranchId { get; init; }
    public List<CreatePurchaseOrderLineItemRequest> LineItems { get; init; } = [];
}

public sealed class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("المورد مطلوب");

        RuleFor(x => x.TaxAmount)
            .GreaterThanOrEqualTo(0).WithMessage("مبلغ الضريبة يجب أن يكون صفراً أو أكثر");

        RuleFor(x => x.LineItems)
            .NotEmpty().WithMessage("يجب إضافة مادة واحدة على الأقل");

        RuleFor(x => x.OrderDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الطلب غير صالح. استخدم YYYY-MM-DD")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderDate));

        RuleFor(x => x.ExpectedDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الاستلام المتوقع غير صالح. استخدم YYYY-MM-DD")
            .When(x => !string.IsNullOrWhiteSpace(x.ExpectedDate));

        RuleForEach(x => x.LineItems)
            .SetValidator(new CreatePurchaseOrderLineItemRequestValidator());
    }
}

public sealed class UpdatePurchaseOrderLineItemRequest
{
    public Guid? InventoryItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? ItemDescription { get; init; }
    public int Quantity { get; init; }
    public decimal UnitCost { get; init; }
}

public sealed class UpdatePurchaseOrderRequest
{
    public Guid SupplierId { get; init; }
    public string? OrderDate { get; init; }
    public string? ExpectedDate { get; init; }
    public decimal TaxAmount { get; init; }
    public string? Notes { get; init; }
    public Guid? BranchId { get; init; }
    public List<UpdatePurchaseOrderLineItemRequest>? LineItems { get; init; }
}

public sealed class UpdatePurchaseOrderRequestValidator : AbstractValidator<UpdatePurchaseOrderRequest>
{
    public UpdatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("المورد مطلوب");

        RuleFor(x => x.TaxAmount)
            .GreaterThanOrEqualTo(0).WithMessage("مبلغ الضريبة يجب أن يكون صفراً أو أكثر");

        RuleFor(x => x.OrderDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الطلب غير صالح. استخدم YYYY-MM-DD")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderDate));

        RuleFor(x => x.ExpectedDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الاستلام المتوقع غير صالح. استخدم YYYY-MM-DD")
            .When(x => !string.IsNullOrWhiteSpace(x.ExpectedDate));
    }
}

public sealed class ReceivePurchaseOrderLineItemRequest
{
    public Guid Id { get; init; }
    public int ReceivedQuantity { get; init; }
}

public sealed class ReceivePurchaseOrderRequest
{
    public List<ReceivePurchaseOrderLineItemRequest> LineItems { get; init; } = [];
}

public sealed class ReceivePurchaseOrderRequestValidator : AbstractValidator<ReceivePurchaseOrderRequest>
{
    public ReceivePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.LineItems)
            .NotEmpty().WithMessage("يجب تحديد كمية الاستلام لمادة واحدة على الأقل");

        RuleForEach(x => x.LineItems).ChildRules(item =>
        {
            item.RuleFor(x => x.Id).NotEmpty().WithMessage("معرف المادة مطلوب");
            item.RuleFor(x => x.ReceivedQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("كمية الاستلام يجب أن تكون صفراً أو أكثر");
        });
    }
}

// ─── Controller ──────────────────────────────────────────────────────────────

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Policy = "AdminOnly")]
public class PurchaseOrdersController(AppDbContext db, ILogger<PurchaseOrdersController> logger) : ControllerBase
{
    // ─── 1. GET /api/purchase-orders — List all POs ─────────────────────
    /// <summary>Returns paginated list of purchase orders with optional filters.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.PurchaseOrders
            .Include(po => po.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PurchaseOrderStatus>(status, true, out var statusFilter))
            query = query.Where(po => po.Status == statusFilter);

        if (supplierId.HasValue)
            query = query.Where(po => po.SupplierId == supplierId.Value);

        var total = await query.CountAsync();

        var orders = await query
            .OrderByDescending(po => po.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(po => new
            {
                po.Id,
                po.OrderNumber,
                po.SupplierId,
                SupplierName = po.Supplier != null ? po.Supplier.Name : "",
                Status = po.Status.ToString(),
                StatusArabic = GetStatusArabic(po.Status),
                po.OrderDate,
                po.ExpectedDate,
                po.TotalAmount,
                LineItemCount = po.LineItems.Count,
                po.CreatedAt,
                po.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { data = orders, total, page, pageSize });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetAll purchase orders failed");
            var fallback = await TryGetPurchaseOrdersReadFallbackAsync(status, supplierId, page, pageSize);
            if (fallback is not null)
            {
                logger.LogWarning(ex, "Purchase orders list is using a schema-tolerant read fallback");
                return Ok(fallback);
            }

            logger.LogWarning(ex, "Purchase orders list is using an empty read fallback");
            return Ok(new { data = Array.Empty<object>(), total = 0, page, pageSize, readFallback = true, fallbackReason = "schema-unavailable" });
        }
    }

    private static bool IsReadSchemaCompatibilityFailure(Exception ex)
    {
        var pg = ex.InnerException as PostgresException;
        return pg?.SqlState is "42P01" or "42703" or "42804" or "42883" or "22P02";
    }

    private async Task<object?> TryGetPurchaseOrdersReadFallbackAsync(string? status, Guid? supplierId, int page, int pageSize)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Max(1, Math.Min(pageSize, 100));

            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var orderColumns = await GetTableColumnsAsync(connection, "PurchaseOrders");
            if (orderColumns.Count == 0)
                return new { data = Array.Empty<object>(), total = 0, page, pageSize, readFallback = true, fallbackReason = "table-missing" };

            var supplierColumns = await GetTableColumnsAsync(connection, "Suppliers");
            var lineItemColumns = await GetTableColumnsAsync(connection, "PurchaseOrderLineItems");
            var hasSuppliers = supplierColumns.Count > 0;
            var hasLineItems = lineItemColumns.Count > 0;

            var where = new List<string>();
            if (orderColumns.Contains("IsActive"))
                where.Add("po.\"IsActive\" = true");
            if (!string.IsNullOrWhiteSpace(status) && orderColumns.Contains("Status"))
                where.Add("po.\"Status\"::text = @status");
            if (supplierId.HasValue && orderColumns.Contains("SupplierId"))
                where.Add("po.\"SupplierId\" = @supplierId");

            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            var supplierJoin = hasSuppliers && orderColumns.Contains("SupplierId")
                ? "LEFT JOIN \"Suppliers\" s ON s.\"Id\" = po.\"SupplierId\""
                : "";
            var lineItemCountSql = hasLineItems && lineItemColumns.Contains("PurchaseOrderId")
                ? "(SELECT COUNT(*) FROM \"PurchaseOrderLineItems\" li WHERE li.\"PurchaseOrderId\" = po.\"Id\")"
                : "0";
            var orderBy = orderColumns.Contains("CreatedAt") ? "ORDER BY po.\"CreatedAt\" DESC" : "ORDER BY po.\"Id\"";

            var total = await ExecuteScalarIntAsync(connection, $"SELECT COUNT(*) FROM \"PurchaseOrders\" po {whereSql}", status, supplierId);
            var sql = $"""
                SELECT
                    {ColumnOrNull(orderColumns, "Id", "po")},
                    {ColumnOrNull(orderColumns, "OrderNumber", "po")},
                    {ColumnOrNull(orderColumns, "SupplierId", "po")},
                    {(hasSuppliers && supplierColumns.Contains("Name") ? "s.\"Name\"" : "NULL AS \"SupplierName\"")},
                    {(orderColumns.Contains("Status") ? "po.\"Status\"::text AS \"Status\"" : "NULL AS \"Status\"")},
                    {ColumnOrNull(orderColumns, "OrderDate", "po")},
                    {ColumnOrNull(orderColumns, "ExpectedDate", "po")},
                    {ColumnOrNull(orderColumns, "TotalAmount", "po")},
                    {lineItemCountSql} AS "LineItemCount",
                    {ColumnOrNull(orderColumns, "CreatedAt", "po")},
                    {ColumnOrNull(orderColumns, "UpdatedAt", "po")}
                FROM "PurchaseOrders" po
                {supplierJoin}
                {whereSql}
                {orderBy}
                OFFSET @offset LIMIT @limit
                """;

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "offset", (page - 1) * pageSize);
            AddParameter(command, "limit", pageSize);
            if (!string.IsNullOrWhiteSpace(status) && orderColumns.Contains("Status"))
                AddParameter(command, "status", status);
            if (supplierId.HasValue && orderColumns.Contains("SupplierId"))
                AddParameter(command, "supplierId", supplierId.Value);

            var orders = new List<object>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var statusText = ReadString(reader, 4);
                orders.Add(new
                {
                    Id = ReadString(reader, 0),
                    OrderNumber = ReadString(reader, 1) ?? "",
                    SupplierId = ReadString(reader, 2),
                    SupplierName = ReadString(reader, 3) ?? "",
                    Status = statusText,
                    StatusArabic = GetStatusArabic(statusText),
                    OrderDate = ReadDateString(reader, 5),
                    ExpectedDate = ReadDateString(reader, 6),
                    TotalAmount = ReadDecimal(reader, 7) ?? 0m,
                    LineItemCount = ReadInt(reader, 8),
                    CreatedAt = ReadDateString(reader, 9),
                    UpdatedAt = ReadDateString(reader, 10)
                });
            }

            return new { data = orders, total, page, pageSize, readFallback = true, fallbackReason = "schema-tolerant" };
        }
        catch (Exception fallbackEx)
        {
            logger.LogWarning(fallbackEx, "Purchase orders schema-tolerant read fallback failed");
            return null;
        }
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(System.Data.Common.DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = @tableName
            """;
        AddParameter(command, "tableName", tableName);

        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static string ColumnOrNull(HashSet<string> columns, string name, string alias) =>
        columns.Contains(name) ? $"{alias}.\"{name}\"" : $"NULL AS \"{name}\"";

    private static async Task<int> ExecuteScalarIntAsync(System.Data.Common.DbConnection connection, string sql, string? status, Guid? supplierId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (sql.Contains("@status", StringComparison.Ordinal))
            AddParameter(command, "status", status);
        if (sql.Contains("@supplierId", StringComparison.Ordinal) && supplierId.HasValue)
            AddParameter(command, "supplierId", supplierId.Value);
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadString(System.Data.Common.DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));

    private static int ReadInt(System.Data.Common.DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));

    private static decimal? ReadDecimal(System.Data.Common.DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));

    private static string? ReadDateString(System.Data.Common.DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd"),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(value)
        };
    }

    // ─── 2. GET /api/purchase-orders/{id} — Get PO with line items ──────
    /// <summary>Returns full purchase order with line items.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.LineItems.OrderBy(li => li.SortOrder))
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order is null)
            return NotFound(new { message = "أمر الشراء غير موجود" });

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.SupplierId,
            SupplierName = order.Supplier?.Name ?? "",
            Status = order.Status.ToString(),
            StatusArabic = GetStatusArabic(order.Status),
            order.OrderDate,
            order.ExpectedDate,
            order.ReceivedDate,
            order.Subtotal,
            order.TaxAmount,
            order.TotalAmount,
            order.Notes,
            order.BranchId,
            order.CreatedBy,
            order.CreatedAt,
            order.UpdatedAt,
            LineItems = order.LineItems.Select(li => new
            {
                li.Id,
                li.PurchaseOrderId,
                li.InventoryItemId,
                li.ItemName,
                li.ItemDescription,
                li.Quantity,
                li.ReceivedQuantity,
                li.UnitCost,
                li.TotalPrice,
                li.SortOrder,
                IsFullyReceived = li.ReceivedQuantity >= li.Quantity
            })
        });
    }

    // ─── 3. POST /api/purchase-orders — Create PO ───────────────────────
    /// <summary>Creates a new purchase order in Draft status with line items.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest req)
    {
        // Validate supplier exists
        var supplierExists = await db.Suppliers.AnyAsync(s => s.Id == req.SupplierId);
        if (!supplierExists)
            return BadRequest(new { message = "المورد غير موجود" });

        // Generate order number with advisory lock
        const int maxRetries = 3;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                // Acquire advisory lock for purchase order number generation
                var lockKey = Math.Abs("PurchaseOrderNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

                var orderNumber = await GenerateOrderNumberAsync(db);

                var (orderDate, dateError) = DateParsingHelper.TryParseDateOrDefault(req.OrderDate, DateOnly.FromDateTime(DateTime.Today), "تاريخ الطلب");
                if (dateError != null) return dateError;

                DateOnly? expectedDate = null;
                if (!string.IsNullOrWhiteSpace(req.ExpectedDate))
                {
                    var (expDate, expError) = DateParsingHelper.TryParseDate(req.ExpectedDate, "تاريخ الاستلام المتوقع");
                    if (expError != null) return expError;
                    expectedDate = expDate;
                }

                var lineItems = new List<PurchaseOrderLineItem>();
                var sortOrder = 0;
                var subtotal = 0m;

                foreach (var itemReq in req.LineItems)
                {
                    var quantity = itemReq.Quantity > 0 ? itemReq.Quantity : 1;
                    var unitCost = itemReq.UnitCost;
                    var totalPrice = quantity * unitCost;
                    subtotal += totalPrice;

                    lineItems.Add(new PurchaseOrderLineItem
                    {
                        InventoryItemId = itemReq.InventoryItemId,
                        ItemName = itemReq.ItemName,
                        ItemDescription = itemReq.ItemDescription,
                        Quantity = quantity,
                        ReceivedQuantity = 0,
                        UnitCost = unitCost,
                        TotalPrice = totalPrice,
                        SortOrder = sortOrder++
                    });
                }

                var taxAmount = req.TaxAmount;
                var totalAmount = subtotal + taxAmount;

                var order = new PurchaseOrder
                {
                    OrderNumber = orderNumber,
                    SupplierId = req.SupplierId,
                    Status = PurchaseOrderStatus.Draft,
                    OrderDate = orderDate,
                    ExpectedDate = expectedDate,
                    Subtotal = subtotal,
                    TaxAmount = taxAmount,
                    TotalAmount = totalAmount,
                    Notes = req.Notes,
                    BranchId = req.BranchId,
                    CreatedBy = GetCurrentUserId(),
                    LineItems = lineItems
                };

                db.PurchaseOrders.Add(order);

                try
                {
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    await tx.RollbackAsync();
                    logger.LogWarning("Purchase order number collision on attempt {Attempt}, retrying", attempt + 1);
                    continue;
                }

                return CreatedAtAction(nameof(GetById), new { id = order.Id },
                    new { order.Id, order.OrderNumber, Status = order.Status.ToString(), order.TotalAmount, message = "تم إنشاء أمر الشراء بنجاح" });
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                throw;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        logger.LogError("Failed to generate unique purchase order number after {MaxRetries} attempts", maxRetries);
        return StatusCode(500, new { message = "فشل إنشاء رقم أمر شراء فريد بعد عدة محاولات. يرجى المحاولة مرة أخرى." });
    }

    // ─── 4. PUT /api/purchase-orders/{id} — Update PO (Draft only) ──────
    /// <summary>Updates a draft purchase order (line items, notes, etc.). Only Draft status allowed.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePurchaseOrderRequest req)
    {
        var order = await db.PurchaseOrders
            .Include(po => po.LineItems)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order is null)
            return NotFound(new { message = "أمر الشراء غير موجود" });
        if (!order.IsActive)
            return BadRequest(new { message = "أمر الشراء محذوف" });
        if (order.Status != PurchaseOrderStatus.Draft)
            return BadRequest(new { message = "يمكن تعديل أوامر الشراء المسودة فقط" });

        // Validate supplier exists
        var supplierExists = await db.Suppliers.AnyAsync(s => s.Id == req.SupplierId);
        if (!supplierExists)
            return BadRequest(new { message = "المورد غير موجود" });

        var (orderDate, dateError) = DateParsingHelper.TryParseDateOrDefault(req.OrderDate, DateOnly.FromDateTime(DateTime.Today), "تاريخ الطلب");
        if (dateError != null) return dateError;

        DateOnly? expectedDate = null;
        if (!string.IsNullOrWhiteSpace(req.ExpectedDate))
        {
            var (expDate, expError) = DateParsingHelper.TryParseDate(req.ExpectedDate, "تاريخ الاستلام المتوقع");
            if (expError != null) return expError;
            expectedDate = expDate;
        }

        order.SupplierId = req.SupplierId;
        order.OrderDate = orderDate;
        order.ExpectedDate = expectedDate;
        order.TaxAmount = req.TaxAmount;
        order.Notes = req.Notes;
        order.BranchId = req.BranchId;

        // Replace line items if provided
        if (req.LineItems != null && req.LineItems.Count > 0)
        {
            db.PurchaseOrderLineItems.RemoveRange(order.LineItems);

            var sortOrder = 0;
            var subtotal = 0m;

            foreach (var itemReq in req.LineItems)
            {
                var quantity = itemReq.Quantity > 0 ? itemReq.Quantity : 1;
                var unitCost = itemReq.UnitCost;
                var totalPrice = quantity * unitCost;
                subtotal += totalPrice;

                var lineItem = new PurchaseOrderLineItem
                {
                    PurchaseOrderId = order.Id,
                    InventoryItemId = itemReq.InventoryItemId,
                    ItemName = itemReq.ItemName,
                    ItemDescription = itemReq.ItemDescription,
                    Quantity = quantity,
                    ReceivedQuantity = 0,
                    UnitCost = unitCost,
                    TotalPrice = totalPrice,
                    SortOrder = sortOrder++
                };

                db.PurchaseOrderLineItems.Add(lineItem);
            }

            order.Subtotal = subtotal;
        }
        else
        {
            // Recalculate subtotal from existing items
            var allLineItems = await db.PurchaseOrderLineItems
                .Where(li => li.PurchaseOrderId == order.Id && li.IsActive)
                .ToListAsync();
            order.Subtotal = allLineItems.Sum(li => li.TotalPrice);
        }

        order.TotalAmount = order.Subtotal + order.TaxAmount;

        await db.SaveChangesAsync();
        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            Status = order.Status.ToString(),
            order.Subtotal,
            order.TaxAmount,
            order.TotalAmount,
            message = "تم تحديث أمر الشراء بنجاح"
        });
    }

    // ─── 5. PATCH /api/purchase-orders/{id}/submit — Submit PO ──────────
    /// <summary>Changes PO status from Draft to Submitted.</summary>
    [HttpPatch("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var order = await db.PurchaseOrders.FindAsync(id);
        if (order is null)
            return NotFound(new { message = "أمر الشراء غير موجود" });
        if (!order.IsActive)
            return BadRequest(new { message = "أمر الشراء محذوف" });
        if (order.Status != PurchaseOrderStatus.Draft)
            return BadRequest(new { message = "يمكن تقديم أوامر الشراء المسودة فقط" });

        order.Status = PurchaseOrderStatus.Submitted;
        await db.SaveChangesAsync();

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            Status = order.Status.ToString(),
            StatusArabic = GetStatusArabic(order.Status),
            message = "تم تقديم أمر الشراء بنجاح"
        });
    }

    // ─── 6. PATCH /api/purchase-orders/{id}/receive — Receive PO ────────
    /// <summary>
    /// Receives purchase order items. Updates inventory quantities and creates adjustment records.
    /// Auto-transitions status to Received if all items fully received, else PartiallyReceived.
    /// </summary>
    [HttpPatch("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, [FromBody] ReceivePurchaseOrderRequest req)
    {
        var order = await db.PurchaseOrders
            .Include(po => po.LineItems)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order is null)
            return NotFound(new { message = "أمر الشراء غير موجود" });
        if (!order.IsActive)
            return BadRequest(new { message = "أمر الشراء محذوف" });
        if (order.Status != PurchaseOrderStatus.Submitted && order.Status != PurchaseOrderStatus.PartiallyReceived)
            return BadRequest(new { message = "يمكن استلام أوامر الشراء المقدمة أو المستلمة جزئياً فقط" });

        var userId = GetCurrentUserId();

        foreach (var itemReq in req.LineItems)
        {
            var lineItem = order.LineItems.FirstOrDefault(li => li.Id == itemReq.Id);
            if (lineItem is null)
                return BadRequest(new { message = $"المادة بمعرف {itemReq.Id} غير موجودة في أمر الشراء" });

            if (itemReq.ReceivedQuantity < 0)
                return BadRequest(new { message = "كمية الاستلام لا يمكن أن تكون سالبة" });

            var newReceivedQty = lineItem.ReceivedQuantity + itemReq.ReceivedQuantity;
            if (newReceivedQty > lineItem.Quantity)
                return BadRequest(new { message = $"كمية الاستلام للمادة '{lineItem.ItemName}' تتجوز الكمية المطلوبة" });

            lineItem.ReceivedQuantity = newReceivedQty;

            // Update inventory if linked
            if (lineItem.InventoryItemId.HasValue)
            {
                var inventoryItem = await db.Inventory.FindAsync(lineItem.InventoryItemId.Value);
                if (inventoryItem != null)
                {
                    var previousQuantity = inventoryItem.Quantity;
                    inventoryItem.Quantity += itemReq.ReceivedQuantity;

                    // Create inventory adjustment record
                    var adjustment = new InventoryAdjustment
                    {
                        InventoryItemId = inventoryItem.Id,
                        PreviousQuantity = previousQuantity,
                        NewQuantity = inventoryItem.Quantity,
                        Delta = itemReq.ReceivedQuantity,
                        Reason = $"استلام أمر شراء #{order.OrderNumber}",
                        AdjustmentType = "purchase",
                        PurchaseOrderLineItemId = lineItem.Id,
                        AdjustedBy = userId
                    };

                    db.InventoryAdjustments.Add(adjustment);
                }
            }
        }

        // Auto-transition status
        var allFullyReceived = order.LineItems.All(li => li.ReceivedQuantity >= li.Quantity);
        var anyReceived = order.LineItems.Any(li => li.ReceivedQuantity > 0);

        if (allFullyReceived)
        {
            order.Status = PurchaseOrderStatus.Received;
            order.ReceivedDate = DateOnly.FromDateTime(DateTime.Today);
        }
        else if (anyReceived)
        {
            order.Status = PurchaseOrderStatus.PartiallyReceived;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            Status = order.Status.ToString(),
            StatusArabic = GetStatusArabic(order.Status),
            order.ReceivedDate,
            LineItems = order.LineItems.Select(li => new
            {
                li.Id,
                li.ItemName,
                li.Quantity,
                li.ReceivedQuantity,
                IsFullyReceived = li.ReceivedQuantity >= li.Quantity
            }),
            message = allFullyReceived ? "تم استلام أمر الشراء بالكامل" : "تم استلام أمر الشراء جزئياً"
        });
    }

    // ─── 7. PATCH /api/purchase-orders/{id}/cancel — Cancel PO ──────────
    /// <summary>Changes PO status to Cancelled. Only Draft/Submitted can be cancelled.</summary>
    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var order = await db.PurchaseOrders.FindAsync(id);
        if (order is null)
            return NotFound(new { message = "أمر الشراء غير موجود" });
        if (!order.IsActive)
            return BadRequest(new { message = "أمر الشراء محذوف" });
        if (order.Status != PurchaseOrderStatus.Draft && order.Status != PurchaseOrderStatus.Submitted)
            return BadRequest(new { message = "يمكن إلغاء أوامر الشراء المسودة أو المقدمة فقط" });

        order.Status = PurchaseOrderStatus.Cancelled;
        await db.SaveChangesAsync();

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            Status = order.Status.ToString(),
            StatusArabic = GetStatusArabic(order.Status),
            message = "تم إلغاء أمر الشراء بنجاح"
        });
    }

    // ─── Private helpers ───────────────────────────────────────────────────

    private static string GetStatusArabic(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "مسودة",
        PurchaseOrderStatus.Submitted => "مقدم",
        PurchaseOrderStatus.PartiallyReceived => "مستلم جزئياً",
        PurchaseOrderStatus.Received => "مستلم",
        PurchaseOrderStatus.Cancelled => "ملغى",
        _ => status.ToString()
    };

    private static string GetStatusArabic(string? status) =>
        Enum.TryParse<PurchaseOrderStatus>(status, true, out var parsed)
            ? GetStatusArabic(parsed)
            : status ?? "";

    /// <summary>Generates a unique purchase order number: PO-yyyyMMdd-NNN.</summary>
    private static async Task<string> GenerateOrderNumberAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"PO-{datePart}-";

        var lastNumber = await db.PurchaseOrders
            .IgnoreQueryFilters()
            .Where(po => po.OrderNumber.StartsWith(prefix))
            .OrderByDescending(po => po.OrderNumber)
            .Select(po => po.OrderNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastNumber) && lastNumber.Length > prefix.Length)
        {
            var seqPart = lastNumber[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }

    /// <summary>Checks if a DbUpdateException is a PostgreSQL unique constraint violation (error code 23505).</summary>
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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
