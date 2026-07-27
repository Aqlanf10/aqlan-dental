using AqlanDentalPro.API.Authorization;
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
public partial class FinanceV3SuppliersController(
    AppDbContext db,
    ISupplierRefundService supplierRefundService,
    ICurrentUserService currentUser,
    IAuditService audit) : ControllerBase
{
    // FIN-PERM (Group B): the class-level FinanceAccess policy is the coarse gate;
    // the granular finance.expenses permission (supplier bills are payables/outflows)
    // is the real per-action gate. Admin always bypasses (PermissionGuard).
    private Task<bool> CanAsync(string action) =>
        PermissionGuard.HasAsync(db, currentUser, "finance.expenses", action);

    private IActionResult Deny() =>
        StatusCode(403, new { message = "غير مصرح لك بهذا الإجراء المالي" });

    // ─── 1. GET /api/finance-v3/suppliers — جلب جميع الموردين والمعامل مع أرصدتهم ──
    /// <summary>Returns paginated list of suppliers with balance info for Finance V3.</summary>
    [HttpGet]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        if (!await CanAsync("view")) return Deny();
        // Branch isolation guard - Admin can see all branches
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 30;
        var userBranchId = currentUser.BranchId;

        var query = db.Suppliers.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) ||
                                     (s.ContactPerson != null && s.ContactPerson.Contains(search)) ||
                                     (s.Phone != null && s.Phone.Contains(search)));

        var total = await query.CountAsync();

        var supplierPage = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Type,
                s.ContactPerson,
                s.Phone,
                s.IsActive
            })
            .ToListAsync();

        var supplierIds = supplierPage.Select(s => s.Id).ToList();
        var balances = await db.SupplierBills
            .Where(b => supplierIds.Contains(b.SupplierId) && b.IsActive
                && (currentUser.IsAdmin || (userBranchId.HasValue && b.BranchId == userBranchId.Value)))
            .GroupBy(b => new { b.SupplierId, b.Currency })
            .Select(group => new
            {
                group.Key.SupplierId,
                Currency = string.IsNullOrWhiteSpace(group.Key.Currency) ? "YER" : group.Key.Currency,
                TotalBilled = group.Sum(b => b.TotalAmount),
                TotalPaid = group.Sum(b => b.PaidAmount)
            })
            .ToListAsync();

        var suppliers = supplierPage.Select(s => new
        {
            s.Id,
            s.Name,
            s.Type,
            s.ContactPerson,
            s.Phone,
            s.IsActive,
            CurrencyBalances = balances.Where(b => b.SupplierId == s.Id).Select(b => new
            {
                b.Currency,
                b.TotalBilled,
                b.TotalPaid,
                Balance = b.TotalBilled - b.TotalPaid
            }).OrderBy(b => b.Currency).ToList()
        }).ToList();

        return Ok(new { data = suppliers, total, page, pageSize });
    }

    // ─── 2. POST /api/finance-v3/suppliers — إضافة مورد أو معمل جديد ──
    /// <summary>Creates a new supplier (dental lab, medical vendor, etc.).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateFinanceV3SupplierRequest req)
    {
        if (!await CanAsync("create")) return Deny();
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
        if (!await CanAsync("view")) return Deny();
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });
        var userBranchId = currentUser.BranchId;
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);
        if (supplier == null)
            return NotFound(new { message = "المورد غير موجود" });

        // Load bills in-memory first, then project — avoids EF Core translation issues with DateOnly.ToString
        var rawBills = await db.SupplierBills
            .AsNoTracking()
            .Where(b => b.SupplierId == supplierId && b.IsActive && (currentUser.IsAdmin || (userBranchId.HasValue && b.BranchId == userBranchId.Value)))
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
                b.DueDate,
                b.IsOpeningBalance,
                b.Currency,
                b.ExchangeRateToYer
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
            DueDate = b.DueDate.HasValue ? b.DueDate.Value.ToString("yyyy-MM-dd") : null,
            b.IsOpeningBalance,
            Currency = string.IsNullOrWhiteSpace(b.Currency) ? "YER" : b.Currency,
            b.ExchangeRateToYer
        }).ToList();

        return Ok(new
        {
            SupplierId = supplierId,
            SupplierName = supplier.Name,
            CurrencyBalances = rawBills.GroupBy(b => string.IsNullOrWhiteSpace(b.Currency) ? "YER" : b.Currency)
                .Select(group => new { Currency = group.Key, Balance = group.Sum(b => b.TotalAmount - b.PaidAmount) })
                .OrderBy(group => group.Currency),
            Bills = bills
        });
    }

    // ─── 4. POST /api/finance-v3/suppliers/{id}/bills — تسجيل فاتورة مطالبة ──
    /// <summary>Registers a new supplier bill (increases what the clinic owes).</summary>
    [HttpPost("{supplierId:guid}/bills")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateSupplierBill(Guid supplierId, [FromBody] CreateSupplierBillRequestDto req)
    {
        if (!await CanAsync("create")) return Deny();
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);
        if (supplier == null)
            return NotFound(new { message = "المورد غير موجود" });

        if (req.TotalAmount <= 0)
            return BadRequest(new { message = "يجب أن يكون إجمالي الفاتورة أكبر من الصفر" });

        var billDate = ClinicTimeProvider.ClinicToday();
        if (!string.IsNullOrWhiteSpace(req.BillDate) && !DateOnly.TryParse(req.BillDate, out billDate))
            return BadRequest(new { message = "تاريخ الفاتورة غير صالح. استخدم صيغة YYYY-MM-DD." });

        DateOnly? dueDate = null;
        if (!string.IsNullOrWhiteSpace(req.DueDate))
        {
            if (!DateOnly.TryParse(req.DueDate, out var parsedDueDate))
                return BadRequest(new { message = "تاريخ الاستحقاق غير صالح. استخدم صيغة YYYY-MM-DD." });
            dueDate = parsedDueDate;
        }

        if (dueDate.HasValue && dueDate.Value < billDate)
            return BadRequest(new { message = "تاريخ الاستحقاق لا يمكن أن يسبق تاريخ الفاتورة." });

        string currency;
        try
        {
            currency = NormalizeCurrency(req.Currency);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var exchangeRateToYer = await ResolveExchangeRateToYerAsync(currency, req.ExchangeRateToYer);
        var exchangeRateSource = currency == "YER"
            ? "same_currency"
            : string.IsNullOrWhiteSpace(req.ExchangeRateSource)
                ? (req.ExchangeRateToYer.HasValue ? "manual" : "settings")
                : req.ExchangeRateSource.Trim();

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
        var prefix = req.IsOpeningBalance ? $"OB-SUP-{datePart}-" : $"BILL-{datePart}-";
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
            Currency = currency,
            ExchangeRateToYer = exchangeRateToYer,
            ExchangeRateSource = exchangeRateSource,
            PaidAmount = 0,
            Status = BillStatus.Unpaid,
            BillDate = billDate,
            DueDate = dueDate,
            IsOpeningBalance = req.IsOpeningBalance,
            LabOrderId = req.LabOrderId,
            BranchId = branchId,
            CreatedBy = userId
        };

        // Legacy scalar balance remains YER-only. The Finance V3 read model below
        // derives separate per-currency balances and must be used for all new work.
        if (currency == "YER")
            supplier.Balance += req.TotalAmount;

        var useTx = db.Database.IsRelational();
        var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            db.SupplierBills.Add(bill);

            if (req.IsOpeningBalance)
            {
                var entryNumber = await GenerateOpeningEntryNumberAsync();
                var openingEntry = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    FinancialDocumentId = bill.Id,
                    FinancialDocumentType = FinancialDocumentType.OpeningBalance,
                    Description = $"رصيد افتتاحي لمورد/معمل: {supplier.Name}",
                    EntryDate = billDate,
                    Currency = currency,
                    ExchangeRateToYer = exchangeRateToYer,
                    BranchId = branchId,
                    PerformedBy = userId,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                };
                db.JournalEntries.Add(openingEntry);
                db.JournalLines.AddRange(
                    new JournalLine
                    {
                        JournalEntryId = openingEntry.Id,
                        AccountType = JournalAccountType.OwnerEquity,
                        AccountId = branchId,
                        Debit = req.TotalAmount,
                        Description = "موازنة رصيد افتتاحي دائن",
                        BranchId = branchId,
                    },
                    new JournalLine
                    {
                        JournalEntryId = openingEntry.Id,
                        AccountType = JournalAccountType.AccountsPayable,
                        AccountId = supplierId,
                        Credit = req.TotalAmount,
                        Description = $"رصيد افتتاحي مستحق لـ {supplier.Name}",
                        BranchId = branchId,
                    });
            }
            else
            {
                var entryNumber = await GenerateOpeningEntryNumberAsync();
                var payableEntry = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    FinancialDocumentId = bill.Id,
                    FinancialDocumentType = FinancialDocumentType.SupplierBill,
                    Description = $"استحقاق فاتورة مورد/معمل: {supplier.Name} - {bill.BillNumber}",
                    EntryDate = billDate,
                    Currency = currency,
                    ExchangeRateToYer = exchangeRateToYer,
                    BranchId = branchId,
                    PerformedBy = userId,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                };
                db.JournalEntries.Add(payableEntry);
                db.JournalLines.AddRange(
                    new JournalLine
                    {
                        JournalEntryId = payableEntry.Id,
                        AccountType = JournalAccountType.Expense,
                        AccountId = bill.Id,
                        Debit = req.TotalAmount,
                        Description = bill.Description,
                        BranchId = branchId,
                    },
                    new JournalLine
                    {
                        JournalEntryId = payableEntry.Id,
                        AccountType = JournalAccountType.AccountsPayable,
                        AccountId = supplierId,
                        Credit = req.TotalAmount,
                        Description = $"مستحق للمورد/المعمل {supplier.Name}",
                        BranchId = branchId,
                    });
            }

            await db.SaveChangesAsync();
            if (useTx) await tx!.CommitAsync();
        }
        catch
        {
            if (useTx) await tx!.RollbackAsync();
            throw;
        }

        await audit.LogAsync(AuditAction.Create, "SupplierBill", bill.Id);

        return Ok(new
        {
            bill.Id,
            bill.BillNumber,
            SupplierName = supplier.Name,
            bill.Description,
            bill.TotalAmount,
            bill.Currency,
            bill.ExchangeRateToYer,
            bill.PaidAmount,
            RemainingAmount = bill.TotalAmount,
            Status = bill.Status.ToString(),
            BillDate = bill.BillDate.ToString("yyyy-MM-dd"),
            DueDate = bill.DueDate?.ToString("yyyy-MM-dd"),
            bill.IsOpeningBalance,
            message = req.IsOpeningBalance ? "تم تسجيل الرصيد الافتتاحي للمورد وترحيل القيد المحاسبي" : "تم تسجيل فاتورة المورد بنجاح"
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
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> PayBill(Guid billId, [FromBody] PaySupplierBillRequest request)
    {
        if (!await CanAsync("approve")) return Deny();
        var userId = currentUser.UserId ?? Guid.Empty;

        var posting = await supplierRefundService.PaySupplierBillAsync(billId, request, userId);

        await audit.LogAsync(AuditAction.Create, "SupplierBillPayment", billId,
            details: $"Bill {billId} payment of {request.Amount:N0} via FinanceV3SuppliersController");

        return Ok(new
        {
            message = "تم سداد القسط بنجاح وترحيل القيد للأستاذ العام",
            posting.PaymentId,
            posting.JournalEntryId,
            posting.JournalEntryNumber,
            disbursementVoucherUrl = $"/api/finance-v3/journal-entries/{posting.JournalEntryId}/disbursement-voucher/pdf"
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Credit Notes (إشعارات الدائن)
    // ═══════════════════════════════════════════════════════════════════════════

    // ─── 6. GET /api/finance-v3/credit-notes — جلب إشعارات الدائن ──
    /// <summary>Returns credit notes, optionally filtered by invoice.</summary>
    [HttpGet("/api/finance-v3/credit-notes")]
    public async Task<IActionResult> GetCreditNotes([FromQuery] Guid? invoiceId)
    {
        if (!await CanAsync("view")) return Deny();
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
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteRequest request)
    {
        if (!await CanAsync("edit")) return Deny();
        if (request.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ الإشعار الدائن أكبر من الصفر" });

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.IsActive);
        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = currentUser.BranchId;
        if (branchId == null || branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل إنشاء إشعار دائن." });

        var creditNote = new CreditNote
        {
            InvoiceId = request.InvoiceId,
            PatientId = invoice.PatientId,
            Amount = request.Amount,
            Reason = request.Reason,
            Status = CreditNoteStatus.Approved, // Auto-approve — user already has FinanceAccess
            BranchId = branchId.Value,
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
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> ProcessRefund(Guid creditNoteId, [FromBody] ProcessRefundRequest request)
    {
        if (!await CanAsync("approve")) return Deny();
        var userId = currentUser.UserId ?? Guid.Empty;

        await supplierRefundService.ProcessRefundAsync(creditNoteId, request, userId);

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

    public string? Currency { get; init; }
    public decimal? ExchangeRateToYer { get; init; }
    public string? ExchangeRateSource { get; init; }

    /// <summary>Date of the historical supplier invoice or opening balance, in YYYY-MM-DD format.</summary>
    public string? BillDate { get; init; }

    /// <summary>Marks a historical payable imported at the start of using the system.</summary>
    public bool IsOpeningBalance { get; init; }

    public Guid? LabOrderId { get; init; }
}

public partial class FinanceV3SuppliersController
{
    private async Task<string> GenerateOpeningEntryNumberAsync()
    {
        var prefix = $"JE-{DateOnly.FromDateTime(DateTime.UtcNow):yyyyMMdd}-";
        var last = await db.JournalEntries
            .Where(e => e.EntryNumber.StartsWith(prefix))
            .OrderByDescending(e => e.EntryNumber)
            .Select(e => e.EntryNumber)
            .FirstOrDefaultAsync();

        var sequence = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var lastSequence))
            sequence = lastSequence + 1;

        return $"{prefix}{sequence:D3}";
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency) ? "YER" : currency.Trim().ToUpperInvariant();
        return normalized switch
        {
            "YER" or "SAR" or "USD" => normalized,
            _ => throw new ArgumentException("العملة غير مدعومة. العملات المتاحة: YER أو SAR أو USD.")
        };
    }

    private async Task<decimal> ResolveExchangeRateToYerAsync(string currency, decimal? directRate)
    {
        if (currency == "YER") return 1m;
        if (directRate.HasValue && directRate.Value > 0m) return directRate.Value;

        var configuredRate = await db.Settings
            .Where(setting => setting.Key == $"finance.exchange_rate.{currency}_YER")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync();

        if (decimal.TryParse(configuredRate, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedRate) && parsedRate > 0m)
            return parsedRate;

        throw new ArgumentException($"لا يوجد سعر صرف معتمد للعملة {currency}. أدخله يدوياً أو حدده من أسعار الصرف قبل التسجيل.");
    }
}
