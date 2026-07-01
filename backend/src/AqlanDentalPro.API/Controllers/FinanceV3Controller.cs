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

/// <summary>
/// Finance V3 API — Provides data endpoints for the Finance V3 Financial Center dashboard.
/// Access is restricted to Admin and Accountant roles only (ReportsAccess policy).
///
/// MIGRATION STATUS (CashFlowTransaction → JournalEntry/JournalLine):
/// ─────────────────────────────────────────────────────────────────────
/// Migration A — COMPLETED:
///   ✅ GET /dashboard          — migrated to JournalLine (Treasury debits/credits)
///   ✅ GET /account-balances   — already reads from JournalLine (no change needed)
///
/// Migration B — COMPLETED:
///   ✅ GET /dashboard          — FIXED: inflow/outflow labels were swapped
///   ✅ GET /daily-cash-summary — migrated from CashFlowTransaction to JournalLine + Treasury
///   ✅ GET /profit-loss        — migrated cash figures from CashFlowTransaction to JournalLine
///   ✅ GET /patient-balance    — enriched with JournalLine balance calculation
///   ✅ GET /patient-accounts   — enriched with JournalLine aggregation
///   ✅ GET /payments           — already reads from Payment entity (no CashFlow dependency)
///   ✅ GET /invoices           — already reads from Invoice entity (no CashFlow dependency)
///
/// Migration C — COMPLETED:
///   ✅ GET /expenses           — TreasuryId/TreasuryName now from JournalLine (Treasury account)
///   ✅ GET /active-cashier-session — expected values from JournalLine instead of CashFlow
///   ✅ GET /cashier-sessions/active — expected values from JournalLine instead of CashFlow
///   ✅ POST cashier-sessions/close — expected values + entry linking from JournalLine
///   ✅ POST treasuries/recalculate — balance from JournalLine instead of CashFlow
///   ✅ MapDocumentTypeToCategory   — added AdvancePayment explicit mapping
///   ✅ GET /invoices comment fix   — corrected misleading Balance comment
///
/// Migration D — COMPLETED (this commit):
///   ✅ GET /audit              — CashFlowTransaction removed from resource filter;
///      audit entries now enriched with JournalEntry/JournalLine data (date, type,
///      category, amount, treasury, description, reversal status)
///   ✅ Removed IsCashMethod/IsCardMethod/IsBankMethod — unused after Migration C
///   ✅ Code cleanup — removed obsolete helpers and comment blocks
///
/// Hotfixes (same PR):
///   ✅ ExpectedClosingCard = 0 fix — card currently shares the bank treasury bucket
///   ✅ Treasury opening balance — creates JournalEntry on treasury creation
///   ✅ Treasury recalculate fallback — includes CashFlow OP-BAL for legacy treasuries
///   ✅ DELETE /expenses reads — migrated from CashFlowTransaction to JournalEntry
///
/// Hotfixes 5D:
///   ✅ Fix double SaveChanges in POST /treasuries — manual JournalEntry creation
///      with IsPosted=true from the start (single SaveChanges, atomic)
///   âœ… Fix recalculate fallback too broad — now checks for "رصيد افتتاحي" in
///      description instead of any VaultTransfer; improved fallback calculation logic
///   ✅ Fix branchId=Guid.Empty causes 500 — added early BadRequest(400) validation
///      in POST /treasuries, POST /payments, POST cashier-sessions/close
///
/// Sprint 1 — Finance Stability (this commit):
///   ✅ Admin branchId fallback — when Admin user has no branch assigned (Guid.Empty),
///      GET endpoints bypass branch filter for consolidated view (already worked).
///      POST endpoints now use first active branch as fallback instead of rejecting
///      with BadRequest, so admin can still perform write operations.
///   ✅ Nullable decimal safety — all SumAsync calls use (decimal?) cast with ?? 0m
///      to prevent NullReferenceException on empty result sets.
///   ✅ Overdue calculation null safety — StartDate! removed, uses .GetValueOrDefault()
///      to prevent NullReferenceException when contract StartDate is null.
///   ✅ Helper methods — CalculateContractOutstandingAsync and
///      CalculateInvoiceOutstandingAsync now use nullable-safe aggregation.
///
/// Hotfixes 5E:
///   ✅ Eliminated all remaining CreateEntryAsync/CreateReversalEntryAsync calls —
///      replaced with manual JournalEntry+JournalLine creation (IsPosted=true from start)
///      in POST /expenses, POST /expenses/{id}/approve, DELETE /expenses,
///      POST /supplier-bills/{id}/pay — single SaveChanges per operation
///   ✅ Unified branchId validation in POST /vault-transfers — now applies to ALL
///      users (including Admin), returns BadRequest(400) instead of Forbid(403)
///
/// Phase 6 — Final Cleanup (this commit):
///   ✅ GET /dashboard — extended with legacy summary fields for daily-operations
///      FinanceView migration: ActiveContracts, UnpaidInvoicesCount, DraftInvoicesCount,
///      OverdueAmount, PendingCommissionsAmount, RecentPayments, RecentInvoices
///   ✅ Frontend migrated: useFinanceSummary now calls GET /api/finance-v3/dashboard
///   ✅ Deleted GET /api/finance/summary from PaymentsController (was [Obsolete])
///   ✅ Deleted GET /api/finance/overdue from PaymentsController (was [Obsolete])
///   ✅ Frontend link /finance/overdue → /finance-v3?tab=contracts
///   ✅ Deleted GET /api/cashier-sessions/active from CashierSessionsController (was [Obsolete])
///
/// Remaining CashFlowTransaction references (WRITE ONLY — dual-write, keep for now):
///   ✅ POST /treasuries       — creates CashFlowTransaction (OP-BAL, dual-write, preserved)
///   ✅ POST /expenses          — creates CashFlowTransaction (dual-write, preserved)
///   ✅ DELETE /expenses/{id}   — creates reversal CashFlowTransaction (dual-write, preserved)
///   ✅ POST /expenses/{id}/approve — creates CashFlowTransaction (dual-write, preserved)
///   ✅ POST /payments          — creates CashFlowTransaction (dual-write, preserved)
///   ✅ POST /supplier-bills/pay — creates CashFlowTransaction (dual-write, preserved)
///   ✅ POST cashier-sessions/close — links CashFlowTransactions (backward compat, preserved)
///   ✅ POST treasuries/recalculate — reads CashFlowTransaction for opening balance fallback
///
/// ALL READS NOW USE JournalEntry/JournalLine — CashFlowTransaction is WRITE-ONLY.
///
/// Future phases (not in scope):
///   ⏳ Balance Sheet endpoint
///   ⏳ Remove CashFlowTransaction dual-write once JournalLine is fully verified
/// </summary>
[ApiController]
[Route("api/finance-v3")]
[Authorize(Policy = "ReportsAccess")]
public partial class FinanceV3Controller(
    AppDbContext db,
    ICurrentUserService currentUser,
    IFinanceService financeService,
    IAuditService audit,
    IJournalEntryService journalEntryService,
    ITreasuryResolutionService treasuryResolution,
    ILogger<FinanceV3Controller> logger) : ControllerBase
{
    // FIN-PERM (Group B): the class-level ReportsAccess policy is the coarse gate; the
    // granular per-action gate uses the area-specific finance.* resource key (RolePermissions,
    // owner-configurable from Settings). Admin always bypasses (PermissionGuard). The resource
    // varies by endpoint area (payments/invoices/expenses/reports/treasuries/cashier_session/
    // commissions), so callers pass the resource explicitly. This helper is in the main partial
    // and is visible to all FinanceV3Controller partials.
    private Task<bool> CanAsync(string resource, string action) =>
        PermissionGuard.HasAsync(db, currentUser, resource, action);

    private IActionResult Deny() =>
        StatusCode(403, new { message = "غير مصرح لك بهذا الإجراء المالي" });

    // ─── Write Endpoints (Finance V3) ─────────────────────────────────────

    /// <summary>
    /// POST /api/finance-v3/payments — Register a payment.
    /// Delegates to FinanceService.CreatePaymentAsync (same logic as PaymentsController).
    /// </summary>
    [HttpPost("payments")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest req)
    {
        if (!await CanAsync("finance.payments", "create")) return Deny();
        // Sprint 1: Admin branchId fallback — if admin has no branch assigned,
        // use the first active branch instead of rejecting with BadRequest.
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. يرجى تسجيل الدخول بفرع صالح." });

        // Pass the resolved branch to the service so it uses the same validated branch
        // instead of independently resolving (which could differ for Admin users).
        // This prevents Guid.Empty from being written to financial records.
        req.ResolvedBranchId = branchId;

        // Amount validation: reject zero or negative amounts
        if (req.Amount <= 0)
            return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });

        try
        {
            var result = await financeService.CreatePaymentAsync(req);
            await audit.LogAsync(AuditAction.Create, "Payment", result.Id,
                newData: new { result.Amount, result.PatientId, result.PaymentMethod });
            return Ok(result);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Payment creation validation failed");
            return BadRequest(new { message = "بيانات الدفعة غير صالحة" });
        }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Payment creation operation failed");
            return BadRequest(new { message = "تعذر إنشاء الدفعة — تحقق من البيانات أو الوردية" });
        }
    }

    [HttpGet("exchange-rates")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetExchangeRates()
    {
        if (!await CanAsync("finance", "view")) return Deny();

        var keys = new[] { "finance.exchange_rate.SAR_YER", "finance.exchange_rate.USD_YER" };
        var rows = await db.Settings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        return Ok(new
        {
            baseCurrency = "YER",
            updatedAt = DateTime.UtcNow,
            rates = new[]
            {
                new ExchangeRateDto("SAR", "YER", TryParseRate(rows.GetValueOrDefault("finance.exchange_rate.SAR_YER")), "settings"),
                new ExchangeRateDto("USD", "YER", TryParseRate(rows.GetValueOrDefault("finance.exchange_rate.USD_YER")), "settings")
            }
        });
    }

    [HttpPut("exchange-rates")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> UpdateExchangeRates([FromBody] UpdateExchangeRatesRequest req)
    {
        if (!await CanAsync("finance", "edit")) return Deny();

        if (req.SarToYer <= 0 || req.UsdToYer <= 0)
            return BadRequest(new { message = "سعر الصرف يجب أن يكون أكبر من صفر" });

        await UpsertSettingAsync("finance.exchange_rate.SAR_YER", req.SarToYer.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
        await UpsertSettingAsync("finance.exchange_rate.USD_YER", req.UsdToYer.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));

        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Update, "Settings", null,
            newData: new { req.SarToYer, req.UsdToYer, BaseCurrency = "YER" });

        return Ok(new { message = "تم تحديث أسعار الصرف", baseCurrency = "YER", sarToYer = req.SarToYer, usdToYer = req.UsdToYer });
    }

    private async Task UpsertSettingAsync(string key, string value)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            db.Settings.Add(new Setting
            {
                Key = key,
                Value = value,
                Category = "Finance",
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        setting.Value = value;
        setting.Category ??= "Finance";
        setting.UpdatedAt = DateTime.UtcNow;
    }

    private static decimal? TryParseRate(string? value)
        => decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var rate)
            ? rate
            : null;

    public sealed record ExchangeRateDto(string Currency, string BaseCurrency, decimal? RateToYer, string Source);
    public sealed record UpdateExchangeRatesRequest(decimal SarToYer, decimal UsdToYer);

    /// <summary>
    /// DELETE /api/finance-v3/payments/{id} — Delete a payment (Admin only).
    /// Delegates to FinanceService.DeletePaymentAsync (same logic as PaymentsController).
    /// </summary>
    [HttpDelete("payments/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        if (!await CanAsync("finance.payments", "delete")) return Deny();
        var payment = await financeService.GetPaymentByIdAsync(id);
        var deleted = await financeService.DeletePaymentAsync(id);
        if (deleted && payment != null)
        {
            await audit.LogAsync(AuditAction.Delete, "Payment", id,
                oldData: new { payment.Amount, payment.PatientId });
        }
        return deleted ? Ok(new { message = "تم حذف الدفعة بنجاح" }) : NotFound(new { message = "الدفعة غير موجودة" });
    }

    /// <summary>
    /// PATCH /api/finance-v3/invoices/{id}/cancel — Cancel an invoice.
    /// Reuses the same logic from InvoicesController.Cancel.
    /// </summary>
    [HttpPatch("invoices/{id:guid}/cancel")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CancelInvoice(Guid id, [FromBody] CancelInvoiceRequest? req = null)
    {
        if (!await CanAsync("finance.invoices", "edit")) return Deny();
        var invoice = await db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });
        if (!invoice.IsActive)
            return BadRequest(new { message = "الفاتورة محذوفة" });

        var originalStatus = invoice.Status;

        if (originalStatus == InvoiceStatus.Paid)
            return BadRequest(new { message = "لا يمكن إلغاء فاتورة مدفوعة. يجب استرداد المدفوعات أولاً." });
        if (originalStatus == InvoiceStatus.Cancelled)
            return BadRequest(new { message = "الفاتورة ملغاة بالفعل" });

        if (originalStatus == InvoiceStatus.Issued)
        {
            var hasActivePayments = invoice.Payments.Any(p => p.IsActive);
            if (hasActivePayments)
                return BadRequest(new { message = "لا يمكن إلغاء فاتورة مصدرة بها مدفوعات نشطة. يجب استرداد أو حذف المدفوعات أولاً." });
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedBy = userId;

        if (req?.Notes != null)
            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? $"[إلغاء] {req.Notes}"
                : $"{invoice.Notes}\n[إلغاء] {req.Notes}";

        if (originalStatus == InvoiceStatus.Issued)
        {
            var existingReversal = await db.JournalEntries
                .AnyAsync(e => e.FinancialDocumentId == invoice.Id
                    && e.FinancialDocumentType == FinancialDocumentType.Invoice
                    && e.IsReversal);

            if (!existingReversal)
            {
                var useCancelTx = db.Database.IsRelational();
                var cancelTx = useCancelTx ? await db.Database.BeginTransactionAsync() : null;
                try
                {
                    await financeService.ReverseInvoiceIssuedEntryAsync(invoice.Id);
                    await db.SaveChangesAsync();
                    if (useCancelTx) await cancelTx!.CommitAsync();
                }
                catch
                {
                    if (useCancelTx) await cancelTx!.RollbackAsync();
                    await db.Entry(invoice).ReloadAsync();
                    throw;
                }
            }
            else
            {
                await db.SaveChangesAsync();
            }
        }
        else
        {
            await db.SaveChangesAsync();
        }

        await audit.LogAsync(AuditAction.Update, "Invoice", id, details: "Invoice cancelled via Finance V3");
        return Ok(new { message = "تم إلغاء الفاتورة بنجاح", invoice.Id, Status = invoice.Status.ToString() });
    }
    /// <summary>
    /// POST /api/finance-v3/expenses — Create an operational expense.
    /// Reuses logic from OperationalExpensesController.Create.
    /// </summary>
    [HttpPost("expenses")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest req)
    {
        if (!await CanAsync("finance.expenses", "create")) return Deny();
        // Delegate to the existing OperationalExpensesController logic via service resolution
        // We replicate the core logic here to keep it under V3 authorization policy
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "عنوان المصروف مطلوب" });
        if (!Enum.TryParse<ExpenseCategory>(req.Category, true, out var category))
            return BadRequest(new { message = "صنف المصروف غير صالح" });
        if (req.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ المصروف أكبر من الصفر" });

        var date = ClinicTimeProvider.ClinicToday();
        if (!string.IsNullOrWhiteSpace(req.ExpenseDate) && DateOnly.TryParse(req.ExpenseDate, out var parsedDate))
            date = parsedDate;

        var userId = currentUser.UserId ?? Guid.Empty;
        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، لا توجد فروع نشطة في النظام. لا يمكن تسجيل المصروف." });

        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل مصروف نقدي." });
        }

        Treasury treasury;
        try { treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, req.PaymentMethod, null, activeSession?.Id); }
        catch (ArgumentException) { return BadRequest(new { message = "تعذر تحديد الخزينة — بيانات غير صالحة" }); }

        if (req.SupplierId.HasValue)
        {
            var supplierExists = await db.Suppliers.AnyAsync(s => s.Id == req.SupplierId.Value && s.IsActive);
            if (!supplierExists) return BadRequest(new { message = "المورد المحدد غير موجود" });
        }

        const decimal ApprovalThreshold = 50_000m;
        var needsApproval = req.Amount > ApprovalThreshold;
        var approvalStatus = needsApproval ? ApprovalStatus.Pending : ApprovalStatus.NotRequired;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"EXP-{datePart}-";
            if (db.Database.IsRelational())
            {
                var lockKey = StableLockKeyHelper.ExpenseNumber;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            var lastExpense = await db.OperationalExpenses.IgnoreQueryFilters()
                .Where(e => e.ExpenseNumber.StartsWith(prefix))
                .OrderByDescending(e => e.ExpenseNumber).Select(e => e.ExpenseNumber).FirstOrDefaultAsync();
            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastExpense) && lastExpense.Length > prefix.Length)
            {
                var seqPart = lastExpense[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1;
            }
            var expenseNumber = $"{prefix}{nextSeq:D3}";

            var expense = new OperationalExpense
            {
                ExpenseNumber = expenseNumber, Title = req.Title.Trim(), Category = category,
                Amount = req.Amount, ExpenseDate = date, PaymentMethod = req.PaymentMethod,
                SupplierId = req.SupplierId, LabOrderId = req.LabOrderId,
                Notes = req.Notes?.Trim(), ReceiptAttachmentUrl = req.ReceiptAttachmentUrl,
                PaidBy = userId, BranchId = branchId,
                ApprovalStatus = approvalStatus, IsPostedToLedger = false
            };
            db.OperationalExpenses.Add(expense);

            if (!needsApproval)
            {
                var cashflow = new CashFlowTransaction
                {
                    TransactionNumber = $"TX-{datePart}-OUT-{nextSeq:D3}",
                    Type = TransactionType.Outflow, Category = FinancialCategory.OperationalExpense,
                    Amount = expense.Amount, PaymentMethod = expense.PaymentMethod,
                    TransactionDate = expense.ExpenseDate, ReferenceId = expense.Id,
                    ReferenceNumber = expense.ExpenseNumber,
                    Description = $"قيد مصروف تشغيلي: {expense.Title}",
                    PerformedBy = userId, BranchId = branchId,
                    CashierSessionId = activeSession?.Id, TreasuryId = treasury.Id
                };
                db.CashFlowTransactions.Add(cashflow);
                expense.IsPostedToLedger = true;
                expense.CashFlowTransactionId = cashflow.Id;

                // Create JournalEntry manually with IsPosted = true from the start.
                // Same pattern as POST /treasuries — avoids double SaveChanges from CreateEntryAsync.
                var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
                var je = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    FinancialDocumentId = expense.Id,
                    FinancialDocumentType = FinancialDocumentType.Expense,
                    Description = $"قيد مصروف تشغيلي: {expense.Title}",
                    EntryDate = expense.ExpenseDate,
                    BranchId = branchId,
                    PerformedBy = userId,
                    CashierSessionId = activeSession?.Id,
                    TreasuryId = treasury.Id,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    IsReversal = false,
                };
                db.JournalEntries.Add(je);

                // Debit: Expense (expense increase)
                db.JournalLines.Add(new JournalLine
                {
                    JournalEntryId = je.Id,
                    AccountType = JournalAccountType.Expense,
                    AccountId = expense.Id,
                    Debit = expense.Amount,
                    Credit = 0m,
                    Description = $"مصروف: {expense.Title}",
                    BranchId = branchId,
                });

                // Credit: Treasury (cash outflow)
                db.JournalLines.Add(new JournalLine
                {
                    JournalEntryId = je.Id,
                    AccountType = JournalAccountType.Treasury,
                    AccountId = treasury.Id,
                    Debit = 0m,
                    Credit = expense.Amount,
                    Description = $"سداد من: {treasury.Name}",
                    BranchId = branchId,
                });

                await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, expense.PaymentMethod, expense.Amount, null, activeSession?.Id);
            }

            if (req.LabOrderId.HasValue)
            {
                var labOrder = await db.LabOrders.FindAsync(req.LabOrderId.Value);
                if (labOrder != null) labOrder.Status = "paid";
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            await audit.LogAsync(AuditAction.Create, "OperationalExpense", expense.Id);

            return Created($"/api/finance-v3/expenses/{expense.Id}", new
            {
                expense.Id, expense.ExpenseNumber, expense.Title,
                Category = expense.Category.ToString(), expense.Amount,
                ExpenseDate = expense.ExpenseDate.ToString("yyyy-MM-dd"), expense.PaymentMethod,
                ApprovalStatus = expense.ApprovalStatus.ToString(), expense.IsPostedToLedger,
                message = needsApproval
                    ? $"تم تسجيل المصروف بنجاح. المبلغ ({req.Amount:N0} ريال) يتجاوز حد الاعتماد — في انتظار موافقة الإدارة قبل الترحيل."
                    : "تم تسجيل المصروف والترحيل المالي بنجاح"
            });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// POST /api/finance-v3/expenses/{id}/approve — Approve a pending expense (Admin only).
    /// </summary>
    [HttpPost("expenses/{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApproveExpense(Guid id, [FromBody] ApproveExpenseRequest req)
    {
        if (!await CanAsync("finance.expenses", "approve")) return Deny();
        var userId = currentUser.UserId ?? Guid.Empty;
        var expenseSnapshot = await db.OperationalExpenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
        if (expenseSnapshot == null) return NotFound(new { message = "المصروف غير موجود" });
        if (expenseSnapshot.ApprovalStatus != ApprovalStatus.Pending)
            return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" });

        var branchId = expenseSnapshot.BranchId;
        if (branchId == Guid.Empty) return BadRequest(new { message = "عذراً، الفرع غير محدد لهذا المصروف. تواصل مع الإدارة." });

        CashierSession? activeSession = null;
        if (string.Equals(expenseSnapshot.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null) return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير أولاً قبل اعتماد المصروف النقدي." });
        }

        Treasury treasury;
        try { treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, expenseSnapshot.PaymentMethod, null, activeSession?.Id); }
        catch (ArgumentException) { return BadRequest(new { message = "تعذر تحديد الخزينة — بيانات غير صالحة" }); }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlRawAsync(@"SELECT 1 FROM ""OperationalExpenses"" WHERE ""Id"" = {0} FOR UPDATE", id);

            var expense = await db.OperationalExpenses.FindAsync(id);
            if (expense == null || !expense.IsActive) { await tx.RollbackAsync(); return NotFound(new { message = "المصروف غير موجود" }); }
            if (expense.ApprovalStatus != ApprovalStatus.Pending) { await tx.RollbackAsync(); return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" }); }

            expense.ApprovalStatus = ApprovalStatus.Approved;
            expense.ApprovedById = userId;
            expense.ApprovedAt = DateTime.UtcNow;
            expense.ApprovalNotes = req.Notes?.Trim();

            var datePart = expense.ExpenseDate.ToString("yyyyMMdd");
            var seqSuffix = expense.ExpenseNumber.Split('-').LastOrDefault() ?? "001";
            if (!int.TryParse(seqSuffix, out var seq)) seq = 1;

            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-OUT-{seq:D3}",
                Type = TransactionType.Outflow, Category = FinancialCategory.OperationalExpense,
                Amount = expense.Amount, PaymentMethod = expense.PaymentMethod,
                TransactionDate = expense.ExpenseDate, ReferenceId = expense.Id,
                ReferenceNumber = expense.ExpenseNumber,
                Description = $"قيد مصروف تشغيلي (معتمد): {expense.Title}",
                PerformedBy = userId, BranchId = branchId,
                CashierSessionId = activeSession?.Id, TreasuryId = treasury.Id
            };
            db.CashFlowTransactions.Add(cashflow);
            expense.IsPostedToLedger = true;
            expense.CashFlowTransactionId = cashflow.Id;

            // Create JournalEntry manually with IsPosted = true from the start.
            // Same pattern as POST /treasuries — avoids double SaveChanges from CreateEntryAsync.
            var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
            var je = new JournalEntry
            {
                EntryNumber = entryNumber,
                FinancialDocumentId = expense.Id,
                FinancialDocumentType = FinancialDocumentType.Expense,
                Description = $"قيد مصروف تشغيلي (معتمد): {expense.Title}",
                EntryDate = expense.ExpenseDate,
                BranchId = branchId,
                PerformedBy = userId,
                CashierSessionId = activeSession?.Id,
                TreasuryId = treasury.Id,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                IsReversal = false,
            };
            db.JournalEntries.Add(je);

            // Debit: Expense (expense increase)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Expense,
                AccountId = expense.Id,
                Debit = expense.Amount,
                Credit = 0m,
                Description = $"مصروف معتمد: {expense.Title}",
                BranchId = branchId,
            });

            // Credit: Treasury (cash outflow)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Treasury,
                AccountId = treasury.Id,
                Debit = 0m,
                Credit = expense.Amount,
                Description = $"سداد من: {treasury.Name}",
                BranchId = branchId,
            });

            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, expense.PaymentMethod, expense.Amount, null, activeSession?.Id);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Approve, "OperationalExpense", id);
            return Ok(new { message = "تم اعتماد المصروف وترحيله للأستاذ العام بنجاح", expense.Id, expense.ExpenseNumber, ApprovalStatus = expense.ApprovalStatus.ToString() });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// POST /api/finance-v3/expenses/{id}/reject — Reject a pending expense (Admin only).
    /// </summary>
    [HttpPost("expenses/{id:guid}/reject")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RejectExpense(Guid id, [FromBody] RejectExpenseRequest req)
    {
        if (!await CanAsync("finance.expenses", "approve")) return Deny();
        if (string.IsNullOrWhiteSpace(req.Reason)) return BadRequest(new { message = "سبب الرفض مطلوب" });

        // Same row-lock pattern as ApproveExpense: without it, a concurrent approve
        // could post the expense to the ledger while this request marks it rejected.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlRawAsync(@"SELECT 1 FROM ""OperationalExpenses"" WHERE ""Id"" = {0} FOR UPDATE", id);

            var expense = await db.OperationalExpenses.FindAsync(id);
            if (expense == null || !expense.IsActive) { await tx.RollbackAsync(); return NotFound(new { message = "المصروف غير موجود" }); }
            if (expense.ApprovalStatus != ApprovalStatus.Pending) { await tx.RollbackAsync(); return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" }); }

            var userId = currentUser.UserId ?? Guid.Empty;
            expense.ApprovalStatus = ApprovalStatus.Rejected;
            expense.ApprovedById = userId;
            expense.ApprovedAt = DateTime.UtcNow;
            expense.ApprovalNotes = req.Reason.Trim();
            expense.IsActive = false;
            expense.DeletedAt = DateTime.UtcNow;
            expense.DeletedBy = userId;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Update, "OperationalExpense", id, details: "Expense rejected via V3");
            return Ok(new { message = "تم رفض المصروف وإلغاؤه بنجاح", expense.Id, expense.ExpenseNumber, ApprovalStatus = expense.ApprovalStatus.ToString() });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// DELETE /api/finance-v3/expenses/{id} — Delete/reverse an expense.
    /// Fix: Reads from JournalEntry/JournalLine instead of CashFlowTransaction
    /// for validation checks. CashFlowTransaction reversal (dual-write) is preserved.
    /// </summary>
    [HttpDelete("expenses/{id:guid}")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        if (!await CanAsync("finance.expenses", "delete")) return Deny();
        var expense = await db.OperationalExpenses.FindAsync(id);
        if (expense == null || !expense.IsActive) return NotFound(new { message = "المصروف غير موجود" });

        if (expense.ApprovalStatus == ApprovalStatus.Approved && expense.IsPostedToLedger && !currentUser.IsAdmin)
            return Forbid();

        var userId = currentUser.UserId ?? Guid.Empty;

        if (expense.IsPostedToLedger)
        {
            // Reversal path — replicate OperationalExpensesController.ReversePostedExpenseAsync
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                if (db.Database.IsRelational())
                    await db.Database.ExecuteSqlRawAsync(@"SELECT 1 FROM ""OperationalExpenses"" WHERE ""Id"" = {0} FOR UPDATE", expense.Id);

                var reloaded = await db.OperationalExpenses.FindAsync(expense.Id);
                if (reloaded == null || !reloaded.IsActive) { await tx.RollbackAsync(); return NotFound(new { message = "المصروف غير موجود" }); }

                // Fix: Read from JournalEntry/JournalLine instead of CashFlowTransaction
                // Include Lines so we can mirror them for the reversal
                var originalJe = await db.JournalEntries
                    .Include(je => je.Lines)
                    .FirstOrDefaultAsync(je => je.FinancialDocumentId == reloaded.Id
                        && je.FinancialDocumentType == FinancialDocumentType.Expense
                        && !je.IsReversal && je.IsPosted);

                if (originalJe == null)
                { await tx.RollbackAsync(); return BadRequest(new { message = "لا يوجد قيد محاسبي مرتبط بهذا المصروف" }); }

                // 1. Check if the session is still open (via JournalEntry.CashierSessionId)
                if (originalJe.CashierSessionId.HasValue)
                {
                    var linkedSession = await db.CashierSessions.FindAsync(originalJe.CashierSessionId.Value);
                    if (linkedSession != null && linkedSession.Status != SessionStatus.Open)
                    { await tx.RollbackAsync(); return BadRequest(new { message = "لا يمكن حذف مصروف مرتبط بوردية مقفلة أو مطابقة. تواصل مع المحاسب." }); }
                }

                // 2. Check if a reversal already exists
                var hasReversal = await db.JournalEntries
                    .AnyAsync(je => je.ReversalOfEntryId == originalJe.Id && je.IsReversal);
                if (hasReversal)
                { await tx.RollbackAsync(); return BadRequest(new { message = "هذا المصروف تم عكسه مسبقاً." }); }

                // 3. Get the original TreasuryId from the Treasury JournalLine
                var treasuryId = await db.JournalLines
                    .Where(l => l.JournalEntryId == originalJe.Id
                        && l.AccountType == JournalAccountType.Treasury)
                    .Select(l => l.AccountId)
                    .FirstOrDefaultAsync();

                if (treasuryId == Guid.Empty)
                { await tx.RollbackAsync(); return BadRequest(new { message = "عذراً، لا يمكن عكس القيد — القيد المحاسبي الأصلي غير مرتبط بخزينة. تواصل مع المحاسب." }); }

                var originalTreasury = await db.Treasuries.FindAsync(treasuryId);
                if (originalTreasury == null || !originalTreasury.IsActive)
                { await tx.RollbackAsync(); return BadRequest(new { message = "عذراً، الخزينة الأصلية غير موجودة أو غير مفعلة. لا يمكن عكس القيد المالي — تواصل مع المحاسب." }); }

                // Create reversal JournalEntry manually with IsPosted = true from the start.
                // Same pattern as POST /treasuries — avoids double/triple SaveChanges from
                // CreateReversalEntryAsync (which calls CreateEntryAsync + separate link save).
                // We already have the original entry with Lines loaded, so we can mirror directly.
                var reversalEntryNumber = await journalEntryService.GenerateEntryNumberAsync();
                var reversalJe = new JournalEntry
                {
                    EntryNumber = reversalEntryNumber,
                    FinancialDocumentId = originalJe.FinancialDocumentId,
                    FinancialDocumentType = originalJe.FinancialDocumentType,
                    Description = $"Reversal of {originalJe.EntryNumber}: حذف مصروف: {reloaded.Title}",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = originalJe.BranchId,
                    PerformedBy = userId,
                    CashierSessionId = originalJe.CashierSessionId,
                    TreasuryId = originalJe.TreasuryId,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    IsReversal = true,
                    ReversalOfEntryId = originalJe.Id,
                };
                db.JournalEntries.Add(reversalJe);

                // Mirror the lines: debit → credit, credit → debit
                foreach (var line in originalJe.Lines)
                {
                    db.JournalLines.Add(new JournalLine
                    {
                        JournalEntryId = reversalJe.Id,
                        AccountType = line.AccountType,
                        AccountId = line.AccountId,
                        Debit = line.Credit,   // swap
                        Credit = line.Debit,   // swap
                        Description = $"Reversal: {line.Description}",
                        BranchId = line.BranchId,
                    });
                }

                // Link the original entry to its reversal
                originalJe.ReversedByEntryId = reversalJe.Id;

                // Dual-write: Create reversal CashFlowTransaction (preserved for backward compatibility)
                CashFlowTransaction? originalCashflow = reloaded.CashFlowTransactionId.HasValue
                    ? await db.CashFlowTransactions.FindAsync(reloaded.CashFlowTransactionId.Value)
                    : await db.CashFlowTransactions.FirstOrDefaultAsync(t => t.ReferenceId == reloaded.Id && t.Category == FinancialCategory.OperationalExpense && t.IsActive);

                if (originalCashflow != null)
                {
                    var reversalCashflow = new CashFlowTransaction
                    {
                        TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-OUT-REV-{Guid.NewGuid().ToString()[..8]}",
                        Type = TransactionType.Inflow, Category = FinancialCategory.Reversal,
                        Amount = reloaded.Amount, PaymentMethod = reloaded.PaymentMethod,
                        TransactionDate = ClinicTimeProvider.ClinicToday(),
                        ReferenceId = reloaded.Id, ReferenceNumber = reloaded.ExpenseNumber,
                        Description = $"عكس قيد مصروف تشغيلي: {reloaded.Title}",
                        PerformedBy = userId, BranchId = reloaded.BranchId,
                        IsReversal = true, ReversalOfTransactionId = originalCashflow.Id,
                        CashierSessionId = originalCashflow.CashierSessionId, TreasuryId = treasuryId
                    };
                    db.CashFlowTransactions.Add(reversalCashflow);
                    originalCashflow.ReversedByTransactionId = reversalCashflow.Id;
                }

                await treasuryResolution.IncrementTreasuryBalanceByTreasuryIdAsync(treasuryId, reloaded.Amount);
                reloaded.IsActive = false; reloaded.DeletedAt = DateTime.UtcNow; reloaded.DeletedBy = userId;

                if (reloaded.LabOrderId.HasValue)
                {
                    var labOrder = await db.LabOrders.FindAsync(reloaded.LabOrderId.Value);
                    if (labOrder != null) labOrder.Status = "received";
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();
                await audit.LogAsync(AuditAction.Update, "OperationalExpense", reloaded.Id, details: "Posted expense reversed via V3");
                return Ok(new { message = "تم عكس قيد المصروف وترحيل القيد العكسي بنجاح" });
            }
            catch { await tx.RollbackAsync(); throw; }
        }

        // Unposted expense: safe to soft-delete
        expense.IsActive = false; expense.DeletedAt = DateTime.UtcNow; expense.DeletedBy = userId;
        if (expense.LabOrderId.HasValue)
        {
            var labOrder = await db.LabOrders.FindAsync(expense.LabOrderId.Value);
            if (labOrder != null) labOrder.Status = "received";
        }
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف قيد المصروف بنجاح" });
    }

    /// <summary>
    /// POST /api/finance-v3/supplier-bills — Create a supplier bill.
    /// </summary>
    [HttpPost("supplier-bills")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateSupplierBill([FromBody] CreateSupplierBillRequest req)
    {
        if (!await CanAsync("finance.expenses", "create")) return Deny();
        if (string.IsNullOrWhiteSpace(req.Description)) return BadRequest(new { message = "وصف الفاتورة مطلوب" });
        if (req.TotalAmount <= 0) return BadRequest(new { message = "يجب أن يكون إجمالي الفاتورة أكبر من الصفر" });

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == req.SupplierId && s.IsActive);
        if (supplier == null) return BadRequest(new { message = "المورد المحدد غير موجود" });

        var billDate = ClinicTimeProvider.ClinicToday();
        if (!string.IsNullOrWhiteSpace(req.BillDate) && DateOnly.TryParse(req.BillDate, out var parsedBill)) billDate = parsedBill;
        DateOnly? dueDate = null;
        if (!string.IsNullOrWhiteSpace(req.DueDate) && DateOnly.TryParse(req.DueDate, out var parsedDue)) dueDate = parsedDue;

        var userId = currentUser.UserId ?? Guid.Empty;
        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty) return BadRequest(new { message = "عذراً، لا توجد فروع نشطة في النظام. لا يمكن تسجيل فاتورة المورد." });

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"BILL-{datePart}-";
            if (db.Database.IsRelational())
            {
                var lockKey = StableLockKeyHelper.BillNumber;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            var lastBill = await db.SupplierBills.IgnoreQueryFilters()
                .Where(b => b.BillNumber.StartsWith(prefix))
                .OrderByDescending(b => b.BillNumber).Select(b => b.BillNumber).FirstOrDefaultAsync();
            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastBill) && lastBill.Length > prefix.Length)
            { var seqPart = lastBill[prefix.Length..]; if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1; }
            var billNumber = $"{prefix}{nextSeq:D3}";

            var bill = new SupplierBill
            {
                BillNumber = billNumber, SupplierId = req.SupplierId,
                Description = req.Description.Trim(), TotalAmount = req.TotalAmount,
                PaidAmount = 0, Status = BillStatus.Unpaid, BillDate = billDate,
                DueDate = dueDate, PurchaseOrderId = req.PurchaseOrderId,
                LabOrderId = req.LabOrderId, AttachmentUrl = req.AttachmentUrl,
                Notes = req.Notes?.Trim(), BranchId = branchId, CreatedBy = userId
            };
            db.SupplierBills.Add(bill);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Create, "SupplierBill", bill.Id);
            return Created($"/api/finance-v3/supplier-bills/{bill.Id}", new
            {
                bill.Id, bill.BillNumber, SupplierName = supplier.Name,
                bill.Description, bill.TotalAmount, bill.PaidAmount,
                RemainingAmount = bill.TotalAmount, Status = bill.Status.ToString(),
                BillDate = bill.BillDate.ToString("yyyy-MM-dd"),
                DueDate = bill.DueDate?.ToString("yyyy-MM-dd"),
                message = "تم تسجيل فاتورة المورد بنجاح"
            });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// POST /api/finance-v3/supplier-bills/{id}/pay — Pay a supplier bill installment.
    /// </summary>
    [HttpPost("supplier-bills/{id:guid}/pay")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> PaySupplierBill(Guid id, [FromBody] PayBillInstallmentRequest req)
    {
        if (!await CanAsync("finance.expenses", "approve")) return Deny();
        if (req.Amount <= 0) return BadRequest(new { message = "يجب أن يكون مبلغ الدفعة أكبر من الصفر" });

        var billSnapshot = await db.SupplierBills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
        if (billSnapshot == null) return NotFound(new { message = "الفاتورة غير موجودة" });
        if (billSnapshot.Status == BillStatus.FullyPaid) return BadRequest(new { message = "هذه الفاتورة مدفوعة بالكامل بالفعل" });
        if (billSnapshot.Status == BillStatus.Cancelled) return BadRequest(new { message = "هذه الفاتورة ملغاة" });

        var paymentDate = ClinicTimeProvider.ClinicToday();
        if (!string.IsNullOrWhiteSpace(req.PaymentDate) && DateOnly.TryParse(req.PaymentDate, out var parsedDate)) paymentDate = parsedDate;

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = billSnapshot.BranchId;
        if (branchId == Guid.Empty) return BadRequest(new { message = "عذراً، الفرع غير محدد لفاتورة المورد. تواصل مع الإدارة." });

        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null) return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير أولاً قبل سداد فواتير المورد النقدية." });
        }

        Treasury treasury;
        try { treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, req.PaymentMethod, null, activeSession?.Id); }
        catch (ArgumentException) { return BadRequest(new { message = "تعذر تحديد الخزينة — بيانات غير صالحة" }); }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlRawAsync(@"SELECT 1 FROM ""SupplierBills"" WHERE ""Id"" = {0} FOR UPDATE", id);

            var bill = await db.SupplierBills.Include(b => b.Supplier).FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
            if (bill == null) { await tx.RollbackAsync(); return NotFound(new { message = "الفاتورة غير موجودة" }); }
            if (bill.Status == BillStatus.FullyPaid) { await tx.RollbackAsync(); return BadRequest(new { message = "هذه الفاتورة مدفوعة بالكامل بالفعل" }); }
            if (bill.Status == BillStatus.Cancelled) { await tx.RollbackAsync(); return BadRequest(new { message = "هذه الفاتورة ملغاة" }); }

            var remaining = bill.TotalAmount - bill.PaidAmount;
            if (req.Amount > remaining) { await tx.RollbackAsync(); return BadRequest(new { message = $"مبلغ الدفعة ({req.Amount:N0}) يتجاوز المبلغ المتبقي ({remaining:N0} ريال)" }); }

            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-BILL-{DateTime.UtcNow:HHmmss}",
                Type = TransactionType.Outflow, Category = FinancialCategory.SupplierPayment,
                Amount = req.Amount, PaymentMethod = req.PaymentMethod,
                TransactionDate = paymentDate, ReferenceId = bill.Id,
                ReferenceNumber = bill.BillNumber,
                Description = $"دفعة على فاتورة مورد {bill.BillNumber} — {bill.Supplier?.Name ?? ""}",
                PerformedBy = userId, BranchId = branchId,
                CashierSessionId = activeSession?.Id, TreasuryId = treasury.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            var payment = new SupplierBillPayment
            {
                SupplierBillId = bill.Id, Amount = req.Amount,
                PaymentMethod = req.PaymentMethod, PaymentDate = paymentDate,
                ReferenceNumber = req.ReferenceNumber, Notes = req.Notes?.Trim(),
                PaidBy = userId, CashFlowTransactionId = cashflow.Id
            };
            db.SupplierBillPayments.Add(payment);

            // Create JournalEntry manually with IsPosted = true from the start.
            // Same pattern as POST /treasuries — avoids double SaveChanges from CreateEntryAsync.
            var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
            var je = new JournalEntry
            {
                EntryNumber = entryNumber,
                FinancialDocumentId = payment.Id,
                FinancialDocumentType = FinancialDocumentType.SupplierPayment,
                Description = $"سداد فاتورة مورد: {bill.BillNumber} — {bill.Supplier?.Name ?? ""}",
                EntryDate = paymentDate,
                BranchId = branchId,
                PerformedBy = userId,
                CashierSessionId = activeSession?.Id,
                TreasuryId = treasury.Id,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                IsReversal = false,
            };
            db.JournalEntries.Add(je);

            // Debit: Payable (reduce supplier liability)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Payable,
                AccountId = bill.SupplierId,
                Debit = req.Amount,
                Credit = 0m,
                Description = $"سداد مستحقات: {bill.Supplier?.Name}",
                BranchId = branchId,
            });

            // Credit: Treasury (cash outflow)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = je.Id,
                AccountType = JournalAccountType.Treasury,
                AccountId = treasury.Id,
                Debit = 0m,
                Credit = req.Amount,
                Description = $"سداد من: {treasury.Name}",
                BranchId = branchId,
            });

            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, req.PaymentMethod, req.Amount, null, activeSession?.Id);

            bill.PaidAmount += req.Amount;
            bill.Status = bill.PaidAmount >= bill.TotalAmount ? BillStatus.FullyPaid : BillStatus.PartiallyPaid;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Create, "SupplierBillPayment", payment.Id, details: $"Bill {id} partial payment via V3");

            return Ok(new
            {
                message = bill.Status == BillStatus.FullyPaid
                    ? "تم سداد الفاتورة بالكامل! تم ترحيل القيد للأستاذ العام."
                    : $"تم تسجيل الدفعة بنجاح. المبلغ المتبقي: {bill.TotalAmount - bill.PaidAmount:N0} ريال",
                bill.Id, bill.BillNumber, bill.PaidAmount,
                RemainingAmount = bill.TotalAmount - bill.PaidAmount,
                Status = bill.Status.ToString()
            });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // ─── GET /api/finance-v3/diagnostic/cashflow-columns — Diagnostic: Category & Type column values ──
    /// <summary>
    /// Temporary diagnostic endpoint to inspect the distinct values in
    /// CashFlowTransactions.Category and CashFlowTransactions.Type columns.
    /// This is needed to debug migration failures. Uses raw SQL to bypass EF Core
    /// enum mapping issues.
    /// </summary>
    [HttpGet("diagnostic/cashflow-columns")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetCashFlowColumnDiagnostic()
    {
        try
        {
            var categories = new List<string>();
            var types = new List<string>();
            string? categoryDataType = null;
            string? typeDataType = null;

            // Get column data types
            var colConn = db.Database.GetDbConnection();
            await colConn.OpenAsync();
            try
            {
                using var colCmd = colConn.CreateCommand();
                colCmd.CommandText = @"
                    SELECT column_name, data_type
                    FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name IN ('Category', 'Type')";
                using var colReader = await colCmd.ExecuteReaderAsync();
                while (await colReader.ReadAsync())
                {
                    var colName = colReader.GetString(0);
                    var dtype = colReader.GetString(1);
                    if (colName == "Category") categoryDataType = dtype;
                    if (colName == "Type") typeDataType = dtype;
                }
            }
            finally { await colConn.CloseAsync(); }

            // Get distinct Category values using raw SQL
            var catConn = db.Database.GetDbConnection();
            await catConn.OpenAsync();
            try
            {
                using var catCmd = catConn.CreateCommand();
                catCmd.CommandText = @"SELECT DISTINCT ""Category""::text FROM ""CashFlowTransactions"" ORDER BY ""Category""::text";
                using var catReader = await catCmd.ExecuteReaderAsync();
                while (await catReader.ReadAsync())
                {
                    categories.Add(catReader.GetString(0));
                }
            }
            finally { await catConn.CloseAsync(); }

            // Get distinct Type values using raw SQL
            var typeConn = db.Database.GetDbConnection();
            await typeConn.OpenAsync();
            try
            {
                using var typeCmd = typeConn.CreateCommand();
                typeCmd.CommandText = @"SELECT DISTINCT ""Type""::text FROM ""CashFlowTransactions"" ORDER BY ""Type""::text";
                using var typeReader = await typeCmd.ExecuteReaderAsync();
                while (await typeReader.ReadAsync())
                {
                    types.Add(typeReader.GetString(0));
                }
            }
            finally { await typeConn.CloseAsync(); }

            // Also check migration history
            var appliedMigrations = new List<string>();
            var pendingMigrations = new List<string>();
            try
            {
                var migConn = db.Database.GetDbConnection();
                await migConn.OpenAsync();
                try
                {
                    using var migCmd = migConn.CreateCommand();
                    migCmd.CommandText = @"SELECT ""MigrationId"" FROM ""__EFMigrationsHistory"" ORDER BY ""MigrationId""";
                    using var migReader = await migCmd.ExecuteReaderAsync();
                    while (await migReader.ReadAsync())
                    {
                        appliedMigrations.Add(migReader.GetString(0));
                    }
                }
                finally { await migConn.CloseAsync(); }

                // Check pending migrations
                var allMigrations = db.Database.GetPendingMigrations();
                pendingMigrations = allMigrations.ToList();
            }
            catch (Exception migEx)
            {
                // If we can't read migration history, just note it
                appliedMigrations.Add($"ERROR: {migEx.Message}");
            }

            return Ok(new
            {
                categoryDataType,
                typeDataType,
                distinctCategories = categories,
                distinctTypes = types,
                appliedMigrations,
                pendingMigrations,
            });
        }
        catch (Exception ex)
        {
            // Never leak exception internals to the client — log server-side,
            // return a generic Arabic message (project security rule).
            logger.LogError(ex, "Finance diagnostic (schema inspect) failed");
            return StatusCode(500, new { message = "تعذّر تنفيذ التشخيص حاليًا" });
        }
    }

    // ─── POST /api/finance-v3/diagnostic/apply-cashflow-hotfix — Apply CashFlow Category/Type migration manually ──
    /// <summary>
    /// Manually applies the CashFlow Category/Type varchar-to-integer migration.
    /// This is needed because the EF Core migration chain is blocked by earlier
    /// pending migrations. This endpoint executes the raw SQL directly.
    /// Idempotent — only converts if columns are currently varchar.
    /// </summary>
    [HttpPost("diagnostic/apply-cashflow-hotfix")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApplyCashFlowHotfix()
    {
        try
        {
            var results = new List<string>();

            // Apply Category conversion
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    DO $$
                    DECLARE
                        unknown_cat TEXT;
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'CashFlowTransactions'
                              AND column_name = 'Category'
                              AND data_type IN ('character varying', 'text')
                        ) THEN
                            -- STRICT VALIDATION
                            SELECT ""Category""::text INTO unknown_cat
                            FROM ""CashFlowTransactions""
                            WHERE ""Category""::text NOT IN (
                                'PatientPayment', 'SupplierPayment', 'SalaryPayment',
                                'DoctorCommission', 'OperationalExpense', 'Refund',
                                'GeneralCost', 'InternalTransfer', 'SalaryAdvance', 'Reversal',
                                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                                'Installment'
                            )
                            LIMIT 1;

                            IF unknown_cat IS NOT NULL THEN
                                RAISE EXCEPTION 'Unknown CashFlowTransactions.Category value: %', unknown_cat;
                            END IF;

                            ALTER TABLE ""CashFlowTransactions""
                            ALTER COLUMN ""Category"" TYPE integer USING CASE ""Category""::text
                                WHEN 'PatientPayment'     THEN 0
                                WHEN 'SupplierPayment'    THEN 1
                                WHEN 'SalaryPayment'      THEN 2
                                WHEN 'DoctorCommission'   THEN 3
                                WHEN 'OperationalExpense' THEN 4
                                WHEN 'Refund'             THEN 5
                                WHEN 'GeneralCost'        THEN 6
                                WHEN 'InternalTransfer'   THEN 7
                                WHEN 'SalaryAdvance'      THEN 8
                                WHEN 'Reversal'           THEN 9
                                WHEN 'Installment'        THEN 0
                                WHEN '0' THEN 0
                                WHEN '1' THEN 1
                                WHEN '2' THEN 2
                                WHEN '3' THEN 3
                                WHEN '4' THEN 4
                                WHEN '5' THEN 5
                                WHEN '6' THEN 6
                                WHEN '7' THEN 7
                                WHEN '8' THEN 8
                                WHEN '9' THEN 9
                            END;
                            RAISE NOTICE 'Category converted from varchar to integer';
                        ELSE
                            RAISE NOTICE 'Category is already integer; skipping';
                        END IF;
                    END$$;
                ";
                await cmd.ExecuteNonQueryAsync();
                results.Add("Category column processed");
            }
            finally { await conn.CloseAsync(); }

            // Apply Type conversion
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    DO $$
                    DECLARE
                        unknown_type TEXT;
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'CashFlowTransactions'
                              AND column_name = 'Type'
                              AND data_type IN ('character varying', 'text')
                        ) THEN
                            -- STRICT VALIDATION
                            SELECT ""Type""::text INTO unknown_type
                            FROM ""CashFlowTransactions""
                            WHERE ""Type""::text NOT IN ('Inflow', 'Outflow', '0', '1')
                            LIMIT 1;

                            IF unknown_type IS NOT NULL THEN
                                RAISE EXCEPTION 'Unknown CashFlowTransactions.Type value: %', unknown_type;
                            END IF;

                            ALTER TABLE ""CashFlowTransactions""
                            ALTER COLUMN ""Type"" TYPE integer USING CASE ""Type""::text
                                WHEN 'Inflow'  THEN 0
                                WHEN 'Outflow' THEN 1
                                WHEN '0' THEN 0
                                WHEN '1' THEN 1
                            END;
                            RAISE NOTICE 'Type converted from varchar to integer';
                        ELSE
                            RAISE NOTICE 'Type is already integer; skipping';
                        END IF;
                    END$$;
                ";
                await cmd.ExecuteNonQueryAsync();
                results.Add("Type column processed");
            }
            finally { await conn.CloseAsync(); }

            // Also reconcile the migration history — mark the hotfix as applied
            // This prevents EF Core from trying to re-apply it later
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                    VALUES ('20260621000000_Hotfix_CashFlowCategorySchemaMismatch', '8.0')
                    ON CONFLICT (""MigrationId"") DO NOTHING;
                ";
                await cmd.ExecuteNonQueryAsync();
                results.Add("Migration history updated");
            }
            finally { await conn.CloseAsync(); }

            // Verify
            string? catType = null, typeType = null;
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT column_name, data_type FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions' AND column_name IN ('Category', 'Type')
                ";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(0) == "Category") catType = reader.GetString(1);
                    if (reader.GetString(0) == "Type") typeType = reader.GetString(1);
                }
            }
            finally { await conn.CloseAsync(); }

            return Ok(new
            {
                message = "Hotfix applied successfully",
                steps = results,
                categoryType = catType,
                typeType = typeType,
            });
        }
        catch (Exception ex)
        {
            // Never leak exception internals (message/inner/stack) to the client —
            // log server-side, return a generic Arabic message (project security rule).
            logger.LogError(ex, "Finance diagnostic (cashflow hotfix) failed");
            return StatusCode(500, new { message = "تعذّر تطبيق الإصلاح حاليًا" });
        }
    }
}

/// <summary>
/// DTO for cancelling an invoice via Finance V3 endpoint.
/// </summary>
public sealed class CancelInvoiceRequest { public string? Notes { get; init; } }
