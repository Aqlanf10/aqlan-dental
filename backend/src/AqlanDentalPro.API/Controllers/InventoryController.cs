using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using Npgsql;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateInventoryItemRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public int Quantity { get; init; }
    public int MinQuantity { get; init; }
    public string? Unit { get; init; }
    public decimal? CostPerUnit { get; init; }
    public string? BatchNumber { get; init; }
    public string? ExpiryDate { get; init; }
    public Guid? DefaultSupplierId { get; init; }
}

public sealed class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المادة مطلوب")
            .MaximumLength(200).WithMessage("اسم المادة يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("الكمية يجب أن تكون صفراً أو أكثر");

        RuleFor(x => x.MinQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("الحد الأدنى يجب أن يكون صفراً أو أكثر");

        RuleFor(x => x.CostPerUnit)
            .GreaterThanOrEqualTo(0).WithMessage("تكلفة الوحدة يجب أن تكون صفراً أو أكثر")
            .When(x => x.CostPerUnit.HasValue);

        RuleFor(x => x.BatchNumber)
            .MaximumLength(50).WithMessage("رقم الدفعة يجب ألا يتجاوز 50 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.BatchNumber));

        RuleFor(x => x.ExpiryDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الانتهاء غير صالح. استخدم YYYY-MM-DD")
            .When(x => !string.IsNullOrWhiteSpace(x.ExpiryDate));
    }
}

public sealed class AdjustQuantityRequest
{
    public int Delta { get; init; }
    public string? Reason { get; init; }
}

public sealed class AdjustQuantityRequestValidator : AbstractValidator<AdjustQuantityRequest>
{
    public AdjustQuantityRequestValidator()
    {
        RuleFor(x => x.Delta)
            .NotEqual(0).WithMessage("التعديل يجب أن يكون قيمة غير صفرية");
    }
}

[ApiController]
[Route("api/inventory")]
[Authorize(Policy = "AdminOnly")]
public class InventoryController(AppDbContext db, ILogger<InventoryController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] bool? lowStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        try
        {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.Inventory.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(i => i.Category == category);
        if (lowStock == true) query = query.Where(i => i.Quantity <= i.MinQuantity);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.Category).ThenBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.Category,
                i.Quantity,
                i.MinQuantity,
                i.Unit,
                i.CostPerUnit,
                i.BatchNumber,
                ExpiryDate = i.ExpiryDate != null ? i.ExpiryDate.Value.ToString("yyyy-MM-dd") : (string?)null,
                i.DefaultSupplierId,
                IsLowStock = i.Quantity <= i.MinQuantity,
                CreatedAt = i.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetAll inventory failed");
            var fallback = await TryGetInventoryReadFallbackAsync(category, lowStock, page, pageSize);
            if (fallback is not null)
            {
                logger.LogWarning(ex, "Inventory list is using a schema-tolerant read fallback");
                return Ok(fallback);
            }

            logger.LogWarning(ex, "Inventory list is using an empty read fallback");
            return Ok(new { data = Array.Empty<object>(), total = 0, page, pageSize, readFallback = true, fallbackReason = "schema-unavailable" });
        }
    }

    private static bool IsReadSchemaCompatibilityFailure(Exception ex)
    {
        var pg = ex.InnerException as PostgresException;
        return pg?.SqlState is "42P01" or "42703" or "42804" or "42883" or "22P02";
    }

    private async Task<object?> TryGetInventoryReadFallbackAsync(string? category, bool? lowStock, int page, int pageSize)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Max(1, Math.Min(pageSize, 100));

            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var columns = await GetTableColumnsAsync(connection, "Inventory");
            if (columns.Count == 0)
                return new { data = Array.Empty<object>(), total = 0, page, pageSize, readFallback = true, fallbackReason = "table-missing" };

            var selectColumns = new List<string>
            {
                ColumnOrNull(columns, "Id"),
                ColumnOrNull(columns, "Name"),
                ColumnOrNull(columns, "Category"),
                ColumnOrNull(columns, "Quantity"),
                ColumnOrNull(columns, "MinQuantity"),
                ColumnOrNull(columns, "Unit"),
                ColumnOrNull(columns, "CostPerUnit"),
                ColumnOrNull(columns, "BatchNumber"),
                ColumnOrNull(columns, "ExpiryDate"),
                ColumnOrNull(columns, "DefaultSupplierId"),
                ColumnOrNull(columns, "CreatedAt")
            };

            var where = new List<string>();
            if (columns.Contains("IsActive"))
                where.Add("\"IsActive\" = true");
            if (!string.IsNullOrWhiteSpace(category) && columns.Contains("Category"))
                where.Add("\"Category\" = @category");
            if (lowStock == true && columns.Contains("Quantity") && columns.Contains("MinQuantity"))
                where.Add("\"Quantity\" <= \"MinQuantity\"");

            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            var orderSql = columns.Contains("Category") && columns.Contains("Name")
                ? "ORDER BY \"Category\" NULLS LAST, \"Name\""
                : "ORDER BY 1";

            var total = await ExecuteScalarIntAsync(connection, $"SELECT COUNT(*) FROM \"Inventory\" {whereSql}", category);
            var sql = $"""
                SELECT {string.Join(", ", selectColumns)}
                FROM "Inventory"
                {whereSql}
                {orderSql}
                OFFSET @offset LIMIT @limit
                """;

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "offset", (page - 1) * pageSize);
            AddParameter(command, "limit", pageSize);
            if (!string.IsNullOrWhiteSpace(category) && columns.Contains("Category"))
                AddParameter(command, "category", category);

            var items = new List<object>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var quantity = ReadInt(reader, 3);
                var minQuantity = ReadInt(reader, 4);
                items.Add(new
                {
                    Id = ReadString(reader, 0),
                    Name = ReadString(reader, 1) ?? "",
                    Category = ReadString(reader, 2),
                    Quantity = quantity,
                    MinQuantity = minQuantity,
                    Unit = ReadString(reader, 5),
                    CostPerUnit = ReadDecimal(reader, 6),
                    BatchNumber = ReadString(reader, 7),
                    ExpiryDate = ReadDateString(reader, 8),
                    DefaultSupplierId = ReadString(reader, 9),
                    IsLowStock = quantity <= minQuantity,
                    CreatedAt = ReadDateString(reader, 10)
                });
            }

            return new { data = items, total, page, pageSize, readFallback = true, fallbackReason = "schema-tolerant" };
        }
        catch (Exception fallbackEx)
        {
            logger.LogWarning(fallbackEx, "Inventory schema-tolerant read fallback failed");
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

    private static string ColumnOrNull(HashSet<string> columns, string name) =>
        columns.Contains(name) ? $"\"{name}\"" : $"NULL AS \"{name}\"";

    private static async Task<int> ExecuteScalarIntAsync(System.Data.Common.DbConnection connection, string sql, string? category)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (sql.Contains("@category", StringComparison.Ordinal))
            AddParameter(command, "category", category);
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

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await db.Inventory
            .Where(i => i.Category != null)
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        return Ok(categories);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var items = await db.Inventory
            .Where(i => i.Quantity <= i.MinQuantity)
            .OrderBy(i => i.Quantity)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.Category,
                i.Quantity,
                i.MinQuantity,
                i.Unit
            })
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Returns total inventory valuation (sum of quantity * costPerUnit).</summary>
    [HttpGet("valuation")]
    public async Task<IActionResult> GetValuation()
    {
        var totalItems = await db.Inventory.CountAsync();
        var totalQuantity = await db.Inventory.SumAsync(i => i.Quantity);
        var totalValue = await db.Inventory
            .Where(i => i.CostPerUnit.HasValue)
            .SumAsync(i => i.Quantity * i.CostPerUnit!.Value);

        var lowStockCount = await db.Inventory.CountAsync(i => i.Quantity <= i.MinQuantity);

        return Ok(new
        {
            TotalItems = totalItems,
            TotalQuantity = totalQuantity,
            TotalValue = totalValue,
            LowStockCount = lowStockCount
        });
    }

    /// <summary>Returns items expiring within the specified number of days.</summary>
    [HttpGet("expiring-soon")]
    public async Task<IActionResult> GetExpiringSoon([FromQuery] int days = 30)
    {
        if (days < 1) days = 30;

        var cutoffDate = ClinicTimeProvider.ClinicToday().AddDays(days);
        var today = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday());

        var items = await db.Inventory
            .Where(i => i.ExpiryDate != null && i.ExpiryDate <= cutoffDate)
            .OrderBy(i => i.ExpiryDate)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.Category,
                i.Quantity,
                i.BatchNumber,
                ExpiryDateStr = i.ExpiryDate!.Value.ToString("yyyy-MM-dd"),
                ExpiryDateValue = i.ExpiryDate!.Value,
                IsExpired = i.ExpiryDate < today
            })
            .ToListAsync();

        // Compute DaysUntilExpiry in memory (PostgreSQL DateDiff not available via EF.Functions)
        var result = items.Select(i => new
        {
            i.Id,
            i.Name,
            i.Category,
            i.Quantity,
            i.BatchNumber,
            ExpiryDate = i.ExpiryDateStr,
            DaysUntilExpiry = i.ExpiryDateValue.DayNumber - today.DayNumber,
            i.IsExpired
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemRequest req)
    {
        DateOnly? expiryDate = null;
        if (!string.IsNullOrWhiteSpace(req.ExpiryDate))
        {
            if (!DateOnly.TryParse(req.ExpiryDate, out var parsed))
                return BadRequest(new { message = "تنسيق تاريخ الانتهاء غير صالح. استخدم YYYY-MM-DD" });
            expiryDate = parsed;
        }

        var item = new Inventory
        {
            Name = req.Name,
            Category = req.Category,
            Quantity = req.Quantity,
            MinQuantity = req.MinQuantity,
            Unit = req.Unit,
            CostPerUnit = req.CostPerUnit,
            BatchNumber = req.BatchNumber,
            ExpiryDate = expiryDate,
            DefaultSupplierId = req.DefaultSupplierId
        };

        db.Inventory.Add(item);
        await db.SaveChangesAsync();
        return CreatedAtAction(null, new { id = item.Id }, new { item.Id, item.Name });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateInventoryItemRequest req)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        DateOnly? expiryDate = null;
        if (!string.IsNullOrWhiteSpace(req.ExpiryDate))
        {
            if (!DateOnly.TryParse(req.ExpiryDate, out var parsed))
                return BadRequest(new { message = "تنسيق تاريخ الانتهاء غير صالح. استخدم YYYY-MM-DD" });
            expiryDate = parsed;
        }

        item.Name = req.Name;
        item.Category = req.Category;
        item.Quantity = req.Quantity;
        item.MinQuantity = req.MinQuantity;
        item.Unit = req.Unit;
        item.CostPerUnit = req.CostPerUnit;
        item.BatchNumber = req.BatchNumber;
        item.ExpiryDate = expiryDate;
        item.DefaultSupplierId = req.DefaultSupplierId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/adjust")]
    public async Task<IActionResult> AdjustQuantity(Guid id, [FromBody] AdjustQuantityRequest req)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        var previousQty = item.Quantity;
        var newQty = item.Quantity + req.Delta;
        if (newQty < 0) return BadRequest(new { message = "الكمية لا يمكن أن تكون سالبة" });

        item.Quantity = newQty;

        // Create inventory adjustment record
        var adjustment = new InventoryAdjustment
        {
            InventoryItemId = item.Id,
            PreviousQuantity = previousQty,
            NewQuantity = newQty,
            Delta = req.Delta,
            Reason = req.Reason ?? "تعديل يدوي",
            AdjustmentType = "manual",
            AdjustedBy = GetCurrentUserId()
        };

        db.InventoryAdjustments.Add(adjustment);
        await db.SaveChangesAsync();

        return Ok(new
        {
            id,
            newQuantity = item.Quantity,
            isLowStock = item.Quantity <= item.MinQuantity
        });
    }

    /// <summary>Returns adjustment history for an inventory item.</summary>
    [HttpGet("{id:guid}/adjustments")]
    public async Task<IActionResult> GetAdjustments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var itemExists = await db.Inventory.AnyAsync(i => i.Id == id);
        if (!itemExists)
            return NotFound(new { message = "المادة غير موجودة" });

        pageSize = Math.Max(1, Math.Min(pageSize, 100));

        var query = db.InventoryAdjustments
            .Where(a => a.InventoryItemId == id);

        var total = await query.CountAsync();
        var adjustments = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.PreviousQuantity,
                a.NewQuantity,
                a.Delta,
                a.Reason,
                a.AdjustmentType,
                a.AdjustedBy,
                a.PurchaseOrderLineItemId,
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();

        return Ok(new { data = adjustments, total, page, pageSize });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await db.Inventory.FindAsync(id);
        if (item is null) return NotFound(new { message = "المادة غير موجودة" });

        item.IsActive = false;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = GetCurrentUserId();
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المادة بنجاح" });
    }

    // ─── Private helpers ───────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
