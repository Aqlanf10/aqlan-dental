using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 Suppliers Controller
   ═══════════════════════════════════════════════════════════════════════════════
   المتحكم المستقل والنظيف الخاص بالموردين والمعامل.
   يوفر مسارات جلب الموردين، إنشاء فواتير لهم، وتسديدها مع القيود المحاسبية.

   المسارات الخمسة:
   1. GET    /api/finance-v3/suppliers              — جلب الموردين مع أرصدتهم
   2. POST   /api/finance-v3/suppliers              — إضافة مورد/معمل جديد
   3. GET    /api/finance-v3/suppliers/{id}/bills    — كشف حساب المورد
   4. POST   /api/finance-v3/suppliers/{id}/bills    — تسجيل فاتورة مطالبة
   5. POST   /api/finance-v3/suppliers/bills/{id}/pay — سداد من الخزينة

   إضافةً إلى إشعارات الدائن:
   6. GET    /api/finance-v3/credit-notes            — جلب الإشعارات
   7. POST   /api/finance-v3/credit-notes            — إنشاء إشعار دائن
   8. POST   /api/finance-v3/credit-notes/{id}/refund — تسليم المرتجع للمريض
   ═══════════════════════════════════════════════════════════════════════════════ */

[ApiController]
[Route("api/finance-v3/suppliers")]
[Authorize(Policy = "FinanceAccess")] // Admin + Accountant + Receptionist
public class FinanceV3SuppliersController(
    AppDbContext db,
    IFinanceService financeService,
    ICurrentUserService currentUser,
    IAuditService audit) : ControllerBase
{
    // ─── 1. GET /api/finance-v3/suppliers — جلب جميع الموردين والمعامل مع أرصدتهم ──
    /// <summary>Returns paginated list of suppliers with balance info for Finance V3.</summary>
    [HttpGet]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        // Branch isolation guard - Admin can see all branches
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return Forbid("ليس لديك فرع معين. تواصل مع الإدارة.");

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 30;

        var query = db.Suppliers.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) ||
                                     (s.ContactPerson != null && s.ContactPerson.Contains(search)) ||
                                     (s.Phone != null && s.Phone.Contains(search)));

        var total = await query.CountAsync();

        var suppliers = await query
            .OrderByDescending(s => s.Balance) // عرض من لهم ديون أكبر أولاً
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Type,
                s.ContactPerson,
                s.Phone,
                s.IsActive,
                TotalBilled = db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.TotalAmount) ?? 0,
                TotalPaid = db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.PaidAmount) ?? 0,
                Balance = (db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.TotalAmount) ?? 0)
                         - (db.SupplierBills.Where(b => b.SupplierId == s.Id && b.IsActive).Sum(b => (decimal?)b.PaidAmount) ?? 0)
            })
            .ToListAsync();

        return Ok(new { data = suppliers, total, page, pageSize });
    }

    // ─── 2. POST /api/finance-v3/suppliers — إضافة مورد أو معمل جديد ──
    /// <summary>Creates a new supplier (dental lab, medical vendor, etc.).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateFinanceV3SupplierRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "اسم المورد مطلوب" });

        var supplier = new Supplier
        {
            Name = req.Name,
            ContactPerson = req.ContactPerson,
            Phone = req.Phone,
            Type = Enum.TryParse<SupplierType>(req.Type, out var t) ? t : SupplierType.MedicalVendor
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Create, "Supplier", supplier.Id);

        return CreatedAtAction(nameof(GetSuppliers), new { id = supplier.Id },
            new { supplier.Id, supplier.Name, message = "تم إنشاء المورد بنجاح" });
    }

    // ─── 3. GET /api/finance-v3/suppliers/{id}/bills — كشف حساب المورد ──
    /// <summary>Returns all bills for a specific supplier.</summary>
    [HttpGet("{supplierId:guid}/bills")]
    public async Task<IActionResult> GetSupplierBills(Guid supplierId)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);
        if (supplier == null)
            return NotFound(new { message = "المورد غير موجود" });

        // Load bills in-memory first, then project — avoids EF Core translation issues with DateOnly.ToString
        var rawBills = await db.SupplierBills
            .AsNoTracking()
            .Where(b => b.SupplierId == supplierId && b.IsActive)
            .OrderByDescending(b => b.BillDate)
            .Select(b => new
            {
                b.Id,
                b.BillNumber,
                b.Description,
                b.TotalAmount,
                b.PaidAmount,
                b.Status,
                b.BillDate,
                b.DueDate
            })
            .ToListAsync();

        var bills = rawBills.Select(b => new
        {
            b.Id,
            b.BillNumber,
            b.Description,
            b.TotalAmount,
            b.PaidAmount,
            RemainingAmount = b.TotalAmount - b.PaidAmount,
            Status = b.Status.ToString(),
            BillDate = b.BillDate.ToString("yyyy-MM-dd"),
            DueDate = b.DueDate.HasValue ? b.DueDate.Value.ToString("yyyy-MM-dd") : null
        }).ToList();

        return Ok(new
        {
            SupplierId = supplierId,
            SupplierName = supplier.Name,
            Balance = supplier.Balance,
            Bills = bills
        });
    }

    // ─── 4. POST /api/finance-v3/suppliers/{id}/bills — تسجيل فاتورة مطالبة ──
    /// <summary>Registers a new supplier bill (increases what the clinic owes).</summary>
    [HttpPost("{supplierId:guid}/bills")]
    public async Task<IActionResult> CreateSupplierBill(Guid supplierId, [FromBody] CreateSupplierBillRequestDto req)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);
        if (supplier == null)
            return NotFound(new { message = "المورد غير موجود" });

        if (req.TotalAmount <= 0)
            return BadRequest(new { message = "يجب أن يكون إجمالي الفاتورة أكبر من الصفر" });

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = currentUser.BranchId ?? Guid.Empty;
        if (branchId == Guid.Empty)
        {
            // Admin fallback: resolve to the first active branch
            var firstBranch = await db.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.CreatedAt)
                .FirstOrDefaultAsync();
            if (firstBranch == null)
                return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل تسجيل فاتورة المورد. لا توجد فروع نشطة في النظام." });
            branchId = firstBranch.Id;
        }

        // Generate BILL number
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"BILL-{datePart}-";
        if (db.Database.IsRelational())
        {
            var lockKey = StableLockKeyHelper.BillNumber;
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
        }

        var lastBill = await db.SupplierBills
            .IgnoreQueryFilters()
            .Where(b => b.BillNumber.StartsWith(prefix))
            .OrderByDescending(b => b.BillNumber)
            .Select(b => b.BillNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastBill) && lastBill.Length > prefix.Length)
        {
            var seqPart = lastBill[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1;
        }

        var billNumber = $"{prefix}{nextSeq:D3}";

        var bill = new SupplierBill
        {
            BillNumber = billNumber,
            SupplierId = supplierId,
            Description = req.Description ?? string.Empty,
            TotalAmount = req.TotalAmount,
            PaidAmount = 0,
            Status = BillStatus.Unpaid,
            BillDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = !string.IsNullOrWhiteSpace(req.DueDate) && DateOnly.TryParse(req.DueDate, out var dd) ? dd : (DateOnly?)null,
            LabOrderId = req.LabOrderId,
            BranchId = branchId,
            CreatedBy = userId
        };

        // زيادة مديونية العيادة للمعمل
        supplier.Balance += req.TotalAmount;

        db.SupplierBills.Add(bill);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Create, "SupplierBill", bill.Id);

        return Ok(new
        {
            bill.Id,
            bill.BillNumber,
            SupplierName = supplier.Name,
            bill.Description,
            bill.TotalAmount,
            bill.PaidAmount,
            RemainingAmount = bill.TotalAmount,
            Status = bill.Status.ToString(),
            BillDate = bill.BillDate.ToString("yyyy-MM-dd"),
            DueDate = bill.DueDate?.ToString("yyyy-MM-dd"),
            message = "تم تسجيل فاتورة المورد بنجاح"
        });
    }

    // ─── 5. POST /api/finance-v3/suppliers/bills/{id}/pay — سداد دفعة من فاتورة المعمل ──
    /// <summary>
    /// Pays a supplier bill installment. Delegates to FinanceService.PaySupplierBillAsync
    /// which handles: open cashier session validation, bill+supplier update,
    /// SupplierBillPayment creation, CashFlowTransaction (Outflow), and double-entry
    /// journal (Debit AccountsPayable / Credit Treasury). Commits atomically.
    /// </summary>
    [HttpPost("bills/{billId:guid}/pay")]
    public async Task<IActionResult> PayBill(Guid billId, [FromBody] PaySupplierBillRequest request)
    {
        var userId = currentUser.UserId ?? Guid.Empty;

        await financeService.PaySupplierBillAsync(billId, request, userId);

        await audit.LogAsync(AuditAction.Create, "SupplierBillPayment", billId,
            details: $"Bill {billId} payment of {request.Amount:N0} via FinanceV3SuppliersController");

        return Ok(new { message = "تم سداد القسط بنجاح وترحيل القيد للأستاذ العام" });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Credit Notes (إشعارات الدائن)
    // ═══════════════════════════════════════════════════════════════════════════

    // ─── 6. GET /api/finance-v3/credit-notes — جلب إشعارات الدائن ──
    /// <summary>Returns credit notes, optionally filtered by invoice.</summary>
    [HttpGet("/api/finance-v3/credit-notes")]
    public async Task<IActionResult> GetCreditNotes([FromQuery] Guid? invoiceId)
    {
        var query = db.CreditNotes
            .Include(cn => cn.Invoice)
            .Include(cn => cn.Patient)
            .Where(cn => cn.IsActive);

        if (invoiceId.HasValue)
            query = query.Where(cn => cn.InvoiceId == invoiceId.Value);

        var notes = await query
            .OrderByDescending(cn => cn.CreatedAt)
            .Select(cn => new
            {
                cn.Id,
                cn.InvoiceId,
                cn.PatientId,
                PatientName = cn.Patient != null ? (cn.Patient.FirstName + " " + cn.Patient.LastName).Trim() : "",
                cn.Amount,
                cn.Reason,
                Status = cn.Status.ToString(),
                cn.RefundPaymentId,
                CreatedAt = cn.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
            .ToListAsync();

        return Ok(new { data = notes });
    }

    // ─── 7. POST /api/finance-v3/credit-notes — إنشاء إشعار دائن ──
    /// <summary>
    /// Creates a Credit Note against a patient invoice.
    /// The credit note starts in Draft status and can be approved by an accountant.
    /// </summary>
    [HttpPost("/api/finance-v3/credit-notes")]
    public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ الإشعار الدائن أكبر من الصفر" });

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.IsActive);
        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = currentUser.BranchId ?? Guid.Empty;

        var creditNote = new CreditNote
        {
            InvoiceId = request.InvoiceId,
            PatientId = invoice.PatientId,
            Amount = request.Amount,
            Reason = request.Reason,
            Status = CreditNoteStatus.Approved, // Auto-approve — user already has FinanceAccess
            BranchId = branchId,
            CreatedBy = userId,
            Notes = request.Notes
        };

        db.CreditNotes.Add(creditNote);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Create, "CreditNote", creditNote.Id);

        return Ok(new
        {
            creditNote.Id,
            creditNote.InvoiceId,
            creditNote.PatientId,
            creditNote.Amount,
            creditNote.Reason,
            Status = creditNote.Status.ToString(),
            message = "تم إنشاء إشعار الدائن واعتماده بنجاح. يمكن الآن تسليم المرتجع للمريض."
        });
    }

    // ─── 8. POST /api/finance-v3/credit-notes/{id}/refund — تسليم المرتجع للمريض ──
    /// <summary>
    /// Processes a refund for an approved Credit Note.
    /// Delegates to FinanceService.ProcessRefundAsync which handles:
    /// open cashier session validation, refund Payment creation, CreditNote status update,
    /// CashFlowTransaction (Outflow), and double-entry journal
    /// (Debit SalesReturns / Credit Treasury). Commits atomically.
    /// </summary>
    [HttpPost("/api/finance-v3/credit-notes/{creditNoteId:guid}/refund")]
    public async Task<IActionResult> ProcessRefund(Guid creditNoteId, [FromBody] ProcessRefundRequest request)
    {
        var userId = currentUser.UserId ?? Guid.Empty;

        await financeService.ProcessRefundAsync(creditNoteId, request, userId);

        await audit.LogAsync(AuditAction.Create, "CreditNoteRefund", creditNoteId,
            details: $"Refund processed for credit note {creditNoteId}");

        return Ok(new { message = "تم تسليم المرتجع للمريض وخصمه من الخزينة وترحيل القيد" });
    }
}

// ─── Inline DTOs for this controller ────────────────────────────────────────

public sealed class CreateFinanceV3SupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Type { get; init; } // DentalLab, MedicalVendor, GeneralService
}

public sealed class CreateSupplierBillRequestDto
{
    public string? Description { get; init; }
    public decimal TotalAmount { get; init; }

    /// <summary>Due date as ISO string (e.g., "2026-06-15"). Parsed to DateOnly server-side.</summary>
    public string? DueDate { get; init; }

    public Guid? LabOrderId { get; init; }
}
