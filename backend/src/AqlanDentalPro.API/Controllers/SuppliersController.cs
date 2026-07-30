using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AqlanDentalPro.API.Controllers;

// ─── Request DTOs ────────────────────────────────────────────────────────────

public sealed class CreateSupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المورد مطلوب")
            .MaximumLength(200).WithMessage("اسم المورد يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("اسم جهة الاتصال يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPerson));

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("رقم الهاتف يجب ألا يتجاوز 30 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .MaximumLength(200).WithMessage("البريد الإلكتروني يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("العنوان يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));
    }
}

public sealed class UpdateSupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المورد مطلوب")
            .MaximumLength(200).WithMessage("اسم المورد يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("اسم جهة الاتصال يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPerson));

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("رقم الهاتف يجب ألا يتجاوز 30 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .MaximumLength(200).WithMessage("البريد الإلكتروني يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("العنوان يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));
    }
}

// ─── Controller ──────────────────────────────────────────────────────────────

[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = "AdminOnly")]
public class SuppliersController(
    AppDbContext db,
    ILogger<SuppliersController> logger,
    // CORE-XMOD-001 — per-currency balances derived from the bills, because
    // Supplier.Balance is a single scalar that cannot represent YER, SAR and USD.
    SupplierBalanceReader balanceReader,
    ICurrentUserService currentUser) : ControllerBase
{
    // ─── 1. GET /api/suppliers — List all suppliers ──────────────────────
    /// <summary>Returns paginated list of suppliers with optional search.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        try
        {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) ||
                                     (s.ContactPerson != null && s.ContactPerson.Contains(search)) ||
                                     (s.Phone != null && s.Phone.Contains(search)));

        var total = await query.CountAsync();
        var suppliers = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                Type = s.Type.ToString(),
                s.ContactPerson,
                s.Phone,
                s.Email,
                s.Address,
                s.Notes,
                s.Balance,
                PurchaseOrderCount = s.PurchaseOrders.Count(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled),
                OpenBillCount = s.Bills.Count(b => b.Status != Domain.Enums.BillStatus.FullyPaid && b.Status != Domain.Enums.BillStatus.Cancelled),
                TotalSpent = s.PurchaseOrders
                    .Where(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled)
                    .Sum(po => po.TotalAmount),
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        // CORE-XMOD-001: `OutstandingBills` used to be one scalar summing TotalAmount-PaidAmount
        // across every bill regardless of currency — a supplier owed 200,000 YER and 1,000 SAR
        // was reported as owing "201,000", which is not money in any currency. Replaced by a
        // per-currency list from the shared reader, so the two are never added together.
        var balances = await balanceReader.GetAsync(
            suppliers.Select(x => x.Id).ToList(), branchId: null, ct: HttpContext.RequestAborted);

        var withBalances = suppliers.Select(x => new
        {
            x.Id, x.Name, x.Type, x.ContactPerson, x.Phone, x.Email, x.Address, x.Notes,
            // Kept for the screens that still read it, and only ever YER — see SupplierBalanceReader.
            LegacyYerBalance = x.Balance,
            x.PurchaseOrderCount,
            x.OpenBillCount,
            x.TotalSpent,
            x.CreatedAt,
            BalancesByCurrency = balances
                .Where(b => b.SupplierId == x.Id)
                .Select(b => new { b.Currency, b.TotalBilled, b.TotalPaid, b.Balance, b.OpenBills })
                .ToList(),
        }).ToList();

        return Ok(new { data = withBalances, total, page, pageSize });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetAll suppliers failed");
            if (IsReadSchemaCompatibilityFailure(ex))
            {
                logger.LogWarning(ex, "Suppliers list is using an empty schema-compatibility fallback");
                return Ok(new { data = Array.Empty<object>(), total = 0, page, pageSize, schemaFallback = true });
            }
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل البيانات" });
        }
    }

    private static bool IsReadSchemaCompatibilityFailure(Exception ex)
    {
        // EF Core surfaces read-query PostgresException directly (unwrapped), and
        // enum-type drift (integer column vs string HasConversion) throws
        // InvalidCastException — the previous inner-only check missed both, so the
        // fallback never fired on the exact failures it was written for.
        if (ex is InvalidCastException || ex.InnerException is InvalidCastException)
            return true;

        var pg = ex as PostgresException
            ?? ex.InnerException as PostgresException
            ?? ex.InnerException?.InnerException as PostgresException;
        return pg?.SqlState is "42P01" or "42703" or "42804" or "42883" or "22P02";
    }

    // ─── 2. GET /api/suppliers/{id} — Get supplier by ID ────────────────
    /// <summary>Returns supplier details with purchase order count and total spent.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var supplier = await db.Suppliers
            .Include(s => s.PurchaseOrders)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (supplier is null)
            return NotFound(new { message = "المورد غير موجود" });

        var purchaseOrderCount = supplier.PurchaseOrders.Count;
        var totalSpent = supplier.PurchaseOrders
            .Where(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled)
            .Sum(po => po.TotalAmount);

        return Ok(new
        {
            supplier.Id,
            supplier.Name,
            Type = supplier.Type.ToString(),
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.Notes,
            supplier.Balance,
            PurchaseOrderCount = purchaseOrderCount,
            TotalSpent = totalSpent,
            CreatedAt = supplier.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = supplier.UpdatedAt.ToString("yyyy-MM-dd")
        });
    }

    /// <summary>Returns purchasing/payables summary for one supplier.</summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id)
    {
        var supplier = await db.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                Type = s.Type.ToString(),
                s.Balance,
                PurchaseOrderCount = s.PurchaseOrders.Count(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled),
                TotalPurchaseOrders = s.PurchaseOrders
                    .Where(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled)
                    .Sum(po => po.TotalAmount),
                BillCount = s.Bills.Count(b => b.Status != Domain.Enums.BillStatus.Cancelled),
                // CORE-XMOD-001: OutstandingBills was one scalar summing across every currency.
                // The per-currency answer is attached below from SupplierBalanceReader.
                LastPurchaseDate = s.PurchaseOrders
                    .Where(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled)
                    .OrderByDescending(po => po.OrderDate)
                    .Select(po => (DateOnly?)po.OrderDate)
                    .FirstOrDefault(),
                RecentPurchaseOrders = s.PurchaseOrders
                    .Where(po => po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled)
                    .OrderByDescending(po => po.OrderDate)
                    .Take(5)
                    .Select(po => new
                    {
                        po.Id,
                        po.OrderNumber,
                        Status = po.Status.ToString(),
                        po.OrderDate,
                        po.TotalAmount
                    }),
                RecentBills = s.Bills
                    .Where(b => b.Status != Domain.Enums.BillStatus.Cancelled)
                    .OrderByDescending(b => b.BillDate)
                    .Take(5)
                    .Select(b => new
                    {
                        b.Id,
                        b.BillNumber,
                        Status = b.Status.ToString(),
                        b.BillDate,
                        b.TotalAmount,
                        b.PaidAmount,
                        RemainingAmount = b.TotalAmount - b.PaidAmount
                    })
            })
            .FirstOrDefaultAsync();

        if (supplier is null)
            return NotFound(new { message = "المورد غير موجود" });

        // CORE-XMOD-001: what the clinic owes this supplier, one figure per currency.
        var balances = await balanceReader.GetForSupplierAsync(id, ct: HttpContext.RequestAborted);

        return Ok(new
        {
            supplier.Id,
            supplier.Name,
            supplier.Type,
            LegacyYerBalance = supplier.Balance,
            supplier.PurchaseOrderCount,
            supplier.TotalPurchaseOrders,
            supplier.BillCount,
            supplier.LastPurchaseDate,
            supplier.RecentPurchaseOrders,
            supplier.RecentBills,
            BalancesByCurrency = balances
                .Select(b => new { b.Currency, b.TotalBilled, b.TotalPaid, b.Balance, b.OpenBills })
                .ToList(),
        });
    }

    // ─── 3. POST /api/suppliers — Create supplier ───────────────────────
    /// <summary>Creates a new supplier.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest req)
    {
        var supplier = new Supplier
        {
            Name = req.Name,
            ContactPerson = req.ContactPerson,
            Phone = req.Phone,
            Email = req.Email,
            Address = req.Address,
            Notes = req.Notes
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id },
            new { supplier.Id, supplier.Name, message = "تم إنشاء المورد بنجاح" });
    }

    // ─── 4. PUT /api/suppliers/{id} — Update supplier ───────────────────
    /// <summary>Updates an existing supplier.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierRequest req)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null)
            return NotFound(new { message = "المورد غير موجود" });

        supplier.Name = req.Name;
        supplier.ContactPerson = req.ContactPerson;
        supplier.Phone = req.Phone;
        supplier.Email = req.Email;
        supplier.Address = req.Address;
        supplier.Notes = req.Notes;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── 5. DELETE /api/suppliers/{id} — Soft-delete supplier ───────────
    /// <summary>Soft-deletes a supplier.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null)
            return NotFound(new { message = "المورد غير موجود" });

        supplier.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المورد بنجاح" });
    }

    // ─── 6. Legacy balance repair (CORE-XMOD-003) ────────────────────────
    //
    // Rows written before the currency guard reached SupplierBillsController hold a scalar
    // that mixed YER with SAR and USD. Nothing reads it any more — every screen goes through
    // SupplierBalanceReader — but leaving a wrong number in the database is a trap for the
    // next person who does read it.
    //
    // The correct value is fully determined: under the rule every writer now follows, the
    // scalar is the unpaid total of the supplier's YER bills and nothing else. So this
    // recomputes rather than adjusts, which makes it idempotent — running it twice changes
    // nothing the second time — and it can never invent a number, only restore a derived one.
    //
    // Preview first, apply explicitly. A financial column is not repaired by a side effect.

    private async Task<List<(Guid Id, string Name, decimal Stored, decimal Correct)>> ComputeBalanceDriftAsync(
        CancellationToken ct)
    {
        var suppliers = await db.Suppliers.IgnoreQueryFilters()
            .Select(s => new { s.Id, s.Name, s.Balance })
            .ToListAsync(ct);

        var correctBySupplier = await db.SupplierBills
            .Where(b => b.IsActive
                     && b.Currency == "YER"
                     && b.Status != Domain.Enums.BillStatus.Cancelled)
            .GroupBy(b => b.SupplierId)
            .Select(g => new { SupplierId = g.Key, Owed = g.Sum(b => b.TotalAmount - b.PaidAmount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Owed, ct);

        return suppliers
            .Select(s => (s.Id, s.Name, Stored: s.Balance, Correct: correctBySupplier.GetValueOrDefault(s.Id, 0m)))
            .Where(x => x.Stored != x.Correct)
            .OrderByDescending(x => Math.Abs(x.Stored - x.Correct))
            .ToList();
    }

    /// <summary>Read-only: which suppliers' legacy YER scalar disagrees with their bills.</summary>
    [HttpGet("balance-drift")]
    public async Task<IActionResult> GetBalanceDrift(CancellationToken ct = default)
    {
        var drift = await ComputeBalanceDriftAsync(ct);

        return Ok(new
        {
            count = drift.Count,
            data = drift.Select(d => new
            {
                supplierId = d.Id,
                supplierName = d.Name,
                storedBalance = d.Stored,
                correctBalance = d.Correct,
                difference = d.Stored - d.Correct,
            }).ToList(),
        });
    }

    /// <summary>Rewrites the legacy YER scalar to the value derived from the supplier's bills.</summary>
    [HttpPost("balance-drift/repair")]
    public async Task<IActionResult> RepairBalanceDrift(CancellationToken ct = default)
    {
        var drift = await ComputeBalanceDriftAsync(ct);
        if (drift.Count == 0)
            return Ok(new { repaired = 0, message = "لا توجد فروقات — كل الأرصدة مطابقة للفواتير" });

        var byId = drift.ToDictionary(d => d.Id, d => d);
        var suppliers = await db.Suppliers.IgnoreQueryFilters()
            .Where(s => byId.Keys.Contains(s.Id))
            .ToListAsync(ct);

        foreach (var supplier in suppliers)
            supplier.Balance = byId[supplier.Id].Correct;

        // Recorded per supplier, with both numbers: a silent rewrite of a financial column
        // is indistinguishable from corruption when someone looks at it a year later.
        foreach (var d in drift)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Resource   = "Supplier.Balance",
                ResourceId = d.Id,
                Action     = Domain.Enums.AuditAction.Update,
                UserId     = currentUser.UserId ?? Guid.Empty,
                NewData    = System.Text.Json.JsonSerializer.SerializeToDocument(new
                {
                    action = "RepairLegacyYerBalance",
                    supplierName = d.Name,
                    storedBalance = d.Stored,
                    correctBalance = d.Correct,
                    reason = "CORE-XMOD-003: scalar mixed currencies before the guard reached SupplierBillsController",
                }),
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            repaired = drift.Count,
            message = $"تم تصحيح {drift.Count} رصيد مورد من واقع الفواتير",
        });
    }
}
